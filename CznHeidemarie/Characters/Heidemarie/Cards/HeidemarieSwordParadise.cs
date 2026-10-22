using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using CznHeidemarie.Integration;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑之乐园」：初始遗物 → 能力「希望」→ 本牌，这条链的终点。
///
/// <para>0 费、<b>古代牌</b>（全幅动态立绘，见 <see cref="HeidemarieCard.PortraitPath"/> 的说明）、
/// 目标为全体敌人。对所有敌人造成其<b>各自最大生命值 30%</b> 的伤害，
/// 再按造成的伤害回复自身；回复量溢出时把溢出的部分继续分给队友（联机）。</para>
///
/// <para><b>保留 + 只在打出后离场</b>（2026-10-22 改）：本牌带原生关键字 <b>保留</b>，
/// 回合结束不会自动弃掉；它<b>可以</b>进弃牌堆 / 抽牌堆正常循环
/// （被弃牌效果丢掉、被洗牌带走都没问题，之后还能再抽到）。
/// 唯一的离场时机是<b>打出之后</b> —— 由 <see cref="OnPlay"/> 末尾的
/// <c>CardPileCmd.RemoveFromCombat</c> 移出战斗，不会二次循环。</para>
///
/// <para><b>唯一获取渠道</b> = 能力「希望」：禁止战斗内随机生成，也禁止进 modifier 池
/// （见 <see cref="CanBeGeneratedInCombat"/> / <see cref="CanBeGeneratedByModifiers"/>），
/// 能力的 <c>CreateCard</c> 不受这两个开关限制。
/// 本牌不登记在角色卡池里，只登记在 <c>HeidemarieTokenPool</c>（控制台 / 图鉴可见，抽不到）。</para>
///
/// <para><b>伤害为什么逐敌结算</b>：「最大生命值 30%」是<b>每个敌人各自的</b>数值，
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
	/// <remarks>设计文档口径为 30%（曾按 50% 实现，2026-10-16 依文档回改）。</remarks>
	private const decimal MaxHpPercent = 30m;

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
	/// <remarks>
	/// 原生关键字<b>保留</b>：回合结束留在手里，不会被回合末清手牌弃掉 ——
	/// 玩家可以把它留到最合适的时机再打。同时也<b>不</b>开启
	/// <c>HasTurnEndInHandEffect</c>（改造前那套「回合末仍在手里就移出战斗」的机制已删）。
	/// </remarks>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Retain,
	};

	/// <inheritdoc/>
	/// <summary>
	/// 演出档位：顶击（u3_attack_ready → u3_attack_play）。身体动作与全屏演出的
	/// ux 路线共用这一档，关掉全屏时的回退特效也走它。
	/// </summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackTop;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (CombatState is not { } combatState)
		{
			return;
		}

		// 语音 / 终结挥击音的去重锚点：本牌逐敌循环会发多条攻击指令，
		// 每个敌人都会触发一次 U3Attack。把「本次出牌是哪张牌」钉在结算起点，
		// 音频层就不必依赖 UI 的出牌事件是否走到 —— 关掉全屏演出时正是靠它，
		// 保证 u3 台词与 4 秒长的 u3 挥击音都只响一次，不会按敌人数重读。
		HeidemarieCombatAudio.BeginCardPlay(this);

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
				.WithHitFx(VfxCmd.rockShatterPath)
				.Execute(choiceContext);

			totalDamage += attack.Results
				.SelectMany(results => results)
				.Sum(result => (decimal)result.UnblockedDamage);
		}

		if (totalDamage > 0m)
		{
			await HealWithOverflow(totalDamage);
		}

		// 等全屏演出（ux 破军）的**画面**收尾再结束这次出牌。
		// 逐敌攻击的伤害已在各自命中帧落地（见 HeidemarieCinematicHitGatePatch），
		// 这里只等到 stage_finished；duration 之后的视觉余韵由演出自己的 _tail 放完。
		// 注意：这个等待必须挂在牌上而不是 Hook.AfterAttack —— 挂在钩子上会把
		// 每个敌人的 AttackCommand 都阻塞到演出结束，后续指令晚到，导致
		// 同一段演出按敌人数重演（2026-10-16 修复，详见 HeidemarieCinematicRuntime）。
		// 演出不存在 / 已降级 / 被取消时该方法立即返回，不会卡住出牌流程。
		await HeidemarieCinematicRuntime.WaitForPresentationEnd(this);

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
