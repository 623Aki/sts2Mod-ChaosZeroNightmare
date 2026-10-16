using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑之乐园」：初始遗物 → 能力「希望」→ 本牌，这条链的终点。
///
/// <para>0 费、<b>古代牌</b>（全幅动态立绘，见 <see cref="HeidemarieCard.PortraitPath"/> 的说明）、
/// 目标为全体敌人。对所有敌人造成其<b>各自最大生命值 50%</b> 的伤害，
/// 再按造成的伤害回复自身；回复量溢出时把溢出的部分继续分给队友（联机）。
/// 打出后本牌<b>移出战斗</b>，不会因洗牌回到手里。</para>
///
/// <para><b>唯一获取渠道</b> = 能力「希望」：禁止战斗内随机生成，也禁止进 modifier 池
/// （见 <see cref="CanBeGeneratedInCombat"/> / <see cref="CanBeGeneratedByModifiers"/>），
/// 能力的 <c>CreateCard</c> 不受这两个开关限制。
/// 本牌不登记在角色卡池里，只登记在 <c>HeidemarieTokenPool</c>（控制台 / 图鉴可见，抽不到）。</para>
///
/// <para><b>伤害为什么逐敌结算</b>：「最大生命值 50%」是<b>每个敌人各自的</b>数值，
/// 而 <c>DamageCmd.Attack(CalculatedDamageVar)</c> 在多目标下只会按
/// <c>Calculate(null)</c> 求一个值（引擎实现：<c>AttackCommand</c> 里仅单目标时才把目标传进计算），
/// 拿不到「当前这个敌人的 MaxHp」。因此这里对每个敌人各起一次攻击指令。</para>
///
/// <para><b>回复口径</b>：跟「吸血鬼之触 / 收割」一致，按<b>实际掉血</b>（<c>UnblockedDamage</c>）回血，
/// 被格挡掉的部分不算。</para>
/// </summary>
public sealed class HeidemarieSwordParadise : HeidemarieCard
{
	/// <summary>伤害 / 回复的基准：目标最大生命值的百分比。</summary>
	private const decimal MaxHpPercent = 50m;

	/// <inheritdoc/>
	/// <remarks>
	/// 本卡走「古代」全幅布局，实际显示的是<b>动态卡面</b>：由
	/// <c>Registration/HeidemarieAnimatedCardArt.cs</c> +
	/// <c>CznHeidemarie/scenes/cards/heidemarie_card_art.tscn</c> 把
	/// 素材区「破军(ux)」的<b>三张插画骨架</b>（<c>ux_illust_3 → _2 → _1</c>，后→前）
	/// 组装进一个 SubViewport，再把实时贴图覆盖到 <c>%AncientPortrait</c> 上。
	///
	/// <para>这里返回的是<b>兜底</b>：渲染器没就绪或加载失败时，用引擎自己的
	/// 「卡面缺失」占位图（与 <c>CardModel.MissingPortraitPath</c> 同一张），
	/// 至少不会出现空白或崩溃。</para>
	/// </remarks>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/beta.tres";

	/// <summary>古代牌、攻击、0 费、全体敌人。</summary>
	public HeidemarieSwordParadise()
		: base(0, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
	{
	}

	/// <inheritdoc/>
	/// <remarks>不可升级（与设计表一致）。</remarks>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>只能由能力「希望」生成。</remarks>
	public override bool CanBeGeneratedInCombat => false;

	/// <inheritdoc/>
	/// <remarks>只能由能力「希望」生成。</remarks>
	public override bool CanBeGeneratedByModifiers => false;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (CombatState is not { } combatState)
		{
			return;
		}

		// 先取快照：攻击过程中敌人会死，边打边遍历会漏。
		List<Creature> enemies = combatState.HittableEnemies.ToList();

		decimal totalDamage = 0m;
		foreach (Creature enemy in enemies)
		{
			if (enemy.IsDead)
			{
				continue;
			}

			decimal amount = Math.Floor(enemy.MaxHp * MaxHpPercent / 100m);
			if (amount <= 0m)
			{
				continue;
			}

			AttackCommand attack = await DamageCmd.Attack(amount)
				.FromCard(this, cardPlay)
				.WithAttackerAnim(HeidemarieAttackPlan.U3.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.U3))
				.Targeting(enemy)
				.WithHitFx("vfx/vfx_rock_shatter")
				.Execute(choiceContext);

			totalDamage += attack.Results
				.SelectMany(results => results)
				.Sum(result => (decimal)result.UnblockedDamage);
		}

		if (totalDamage > 0m)
		{
			await HealWithOverflow(totalDamage);
		}

		// 打出后移除此牌：不进弃牌堆，因此不会被洗回抽牌堆。
		// 打出时本牌正处于「已使用牌堆（Play）」，是合法的战斗牌堆（CardPileCmd 会校验）。
		await CardPileCmd.RemoveFromCombat(this);
	}

	/// <summary>
	/// 按伤害量回复自身；溢出量转给队友（联机）。
	///
	/// <para>单机模式下 <c>RunState.Players</c> 只有自己一人，循环天然空转，
	/// 溢出量直接丢弃 —— 与设计说明「单机模式下不处理」一致。</para>
	/// </summary>
	/// <param name="amount">本次造成的实际伤害总量（治疗量）。</param>
	private async Task HealWithOverflow(decimal amount)
	{
		Creature self = Owner.Creature;

		int missingSelf = Math.Max(0, self.MaxHp - self.CurrentHp);
		decimal selfHeal = Math.Min(amount, missingSelf);
		if (selfHeal > 0m)
		{
			await CreatureCmd.Heal(self, selfHeal);
		}

		decimal overflow = amount - selfHeal;
		if (overflow <= 0m)
		{
			return;
		}

		foreach (Player ally in Owner.RunState.Players)
		{
			if (ally == Owner)
			{
				continue;
			}

			Creature creature = ally.Creature;
			if (creature.IsDead)
			{
				continue;
			}

			int missingAlly = Math.Max(0, creature.MaxHp - creature.CurrentHp);
			if (missingAlly <= 0)
			{
				continue;
			}

			decimal heal = Math.Min(overflow, missingAlly);
			if (heal <= 0m)
			{
				continue;
			}

			await CreatureCmd.Heal(creature, heal);
			overflow -= heal;
			if (overflow <= 0m)
			{
				break;
			}
		}
	}
}
