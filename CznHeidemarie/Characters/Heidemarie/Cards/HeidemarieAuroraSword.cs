using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「极光剑」：1 费攻击（衍生牌 Token）。
///
/// <para>关键字<b>连接</b>：直接使用本牌时，丢弃手牌中其他所有带「连接」的牌
/// （由 <see cref="HeidemarieCard.BeforeCardPlayed"/> 统一派发）。</para>
///
/// <para><b>主动打出</b>：对<strong>指向</strong>敌人造成 3 点伤害
/// （<c>TargetType.AnyEnemy</c>，出牌时选目标）。</para>
///
/// <para><b>被丢弃时</b>：对<strong>随机</strong>敌人造成 3 点伤害。
/// 这段效果<strong>自己写在 <see cref="AfterCardDiscarded"/> 里</strong>，
/// 不依赖「安息」关键字 —— 安息还附带「本回合不能移出墓地」的副作用，
/// 而极光剑被「剑冢」从墓地重放是常规玩法，不能被自己的效果锁住。
/// （「北极圈」升级只让<strong>卡牌本体</strong>获得安息，生成的剑不带任何关键字；
/// 将来若出现给剑挂运行期安息的来源，锁墓统一由
/// <see cref="HeidemarieRestLock"/> 处理。）</para>
///
/// <para>关键字<b>守墓</b>：洗牌时不会移动至抽牌堆，会一直留在墓地。</para>
///
/// <para>不可升级、不可被战斗内随机生成（只能由本角色的卡牌 / 能力生成）。</para>
/// </summary>
public sealed class HeidemarieAuroraSword : HeidemarieCard, ILinkCard, IGraveKeepCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(3m, ValueProp.Move),
	};

	/// <inheritdoc/>
	/// <remarks>衍生牌不可升级。</remarks>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>避免被「攻击药水 / Discovery」之类的战斗内随机生成效果抽到。</remarks>
	public override bool CanBeGeneratedInCombat => false;

	/// <summary>1 费、攻击、衍生牌、指向任意敌人。</summary>
	public HeidemarieAuroraSword()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：轻击（attack_ready → attack_play1）。动作与特效档位必须一致。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackLight;

	/// <summary>
	/// 特效变体：<b>极夜层数 &gt; 0 时，特效升格为 u5 终结演出</b>
	///（演出键 <c>AttackLight/u5</c>，见 <c>heidemarie_moves.json</c>）。
	///
	/// <para><b>为什么不换档位而用变体</b>：源数据里 <c>u5_attack_play1</c> 的
	/// <b>身体 clip 就是 <c>attack_play1</c></b>（见 <c>源数据组库与映射.md</c> 的 command 表）——
	/// u5 与普通攻击共用同一套身体动画，只有特效组不同。变体机制
	/// （<see cref="HeidemarieCard.PresentationVariant"/>）正是为「同一套动作换一套特效强度」
	/// 设计的：身体动作、触发器、命中延迟全部不变，只把特效组从「轻击_横挥」
	/// 换成「终结_u5_play1」（target 多一层 <c>f2</c>）。</para>
	///
	/// <para><b>判定口径与增伤区保持一致</b>：判据就是「极夜层数 &gt; 0」，
	/// 它正是 <see cref="HeidemarieAuroraDamagePoolPower.TotalPercent"/> 的全部输入 ——
	/// 只要增伤在生效，特效就同步升格，不会出现「吃着增伤却播轻击特效」的错位。</para>
	///
	/// <para><b>求值时机</b>：<see cref="HeidemariePresentationCatalog.ResolveKey"/>
	/// 在 <c>CardPlayEnqueuingEvent</c>（出牌入队）时读取，早于 <see cref="OnPlay"/>
	/// 的伤害结算，此时极夜尚未被消耗，判定成立。</para>
	/// </summary>
	protected override string? PresentationVariant
	{
		get
		{
			Creature? creature = Owner?.Creature;
			if (creature is null)
			{
				return null;
			}

			return creature.GetPowerAmount<HeidemarieNightPower>() > 0 ? "u5" : null;
		}
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 主动打出：指向伤害。
		// 演出最轻的一档：只播打击动作（Attack）+ 3 层单体特效，
		// 时间轴见 effects/heidemarie_moves.json 的 HeidemarieAuroraSword 键
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Basic))
			.Targeting(cardPlay.Target)
			.WithHitFx(VfxCmd.slashPath)
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 「被丢弃时」的效果自己实现：对随机敌人造成一次伤害。
	///
	/// <para>触发点与「安息」一致（<c>AfterCardDiscarded</c> 只在显式丢弃时派发，
	/// 回合末清手牌不经过这里），但<strong>不使用「安息」关键字</strong>，
	/// 因此不会把本牌锁在墓地里、也不会被「冥渡」之类依赖安息的判定牵连。</para>
	/// </remarks>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (card == this)
		{
			await DealDiscardDamage(choiceContext);
		}

		// 基类仍负责「运行期安息」的锁墓标记（北极圈升级时赋予）
		await base.AfterCardDiscarded(choiceContext, card);
	}

	/// <inheritdoc/>
	/// <remarks>丢弃伤害已在 <see cref="AfterCardDiscarded"/> 里自行结算，这里不再重复。</remarks>
	protected override Task OnRestEffect(PlayerChoiceContext choiceContext) => Task.CompletedTask;

	/// <summary>
	/// 被丢弃：对随机敌人造成一次伤害。
	///
	/// <para><b>这次伤害「视为一次极光剑结算」</b>：因此
	/// <list type="bullet">
	///   <item>照常吃「极夜」的极光剑增伤区（因为 <c>cardSource</c> 就是本牌、
	///   <c>ValueProp.Move</c> 属于强化攻击，见 <see cref="HeidemarieAuroraDamagePoolPower"/>）；</item>
	///   <item>结算后<strong>消耗 1 层极夜</strong>。主动打出时这一步由
	///   <see cref="HeidemarieNightPower.AfterAttack"/> 完成，
	///   而丢弃伤害走的是 <c>CreatureCmd.Damage</c>（不是 AttackCommand），
	///   所以必须在这里自己清 —— 否则会出现「丢一把剑白嫖增伤、再打一把剑再吃一次」。</item>
	/// </list></para>
	/// </summary>
	private async Task DealDiscardDamage(PlayerChoiceContext choiceContext)
	{
		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		Creature? target = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
		if (target == null)
		{
			// 没有可命中的敌人 = 这次结算没发生，不消耗层数
			return;
		}

		await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, Owner.Creature, this, null);

		// 丢弃伤害也是一次「极光剑结算」→ 极夜消耗 1 层
		await HeidemarieNightPower.ConsumeAsync(Owner.Creature);
	}
}
