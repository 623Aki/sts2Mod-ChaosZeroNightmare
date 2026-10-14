using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「陨锋·坠月」：2 费攻击（稀有牌）。
///
/// <para>关键字<b>连接</b>：直接使用本牌时，丢弃手牌中其他所有带「连接」的牌
/// （由 <see cref="HeidemarieCard.BeforeCardPlayed"/> 统一派发）。</para>
///
/// <para><b>主动打出</b>：对指向敌人造成 15 点伤害 2 次。</para>
///
/// <para><b>被丢弃时</b>：对全体敌人造成 20 点伤害。
/// 这段效果<strong>自己写在 <see cref="AfterCardDiscarded"/> 里</strong>，
/// 不使用「安息」关键字 —— 安息附带「本回合不能移出墓地」的副作用，
/// 而本牌并不需要被锁在墓地里。</para>
/// </summary>
public sealed class HeidemarieSkyfall : HeidemarieCard, ILinkCard
{
	/// <summary>被丢弃时的对群伤害。</summary>
	private readonly DamageVar _discardDamage = new("DiscardDamage", 20m, ValueProp.Move);

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(15m, ValueProp.Move),
		_discardDamage,
	};

	/// <summary>2 费、攻击、稀有牌、目标为任意敌人。</summary>
	public HeidemarieSkyfall()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 「被丢弃时」的效果自己实现：对全体敌人结算伤害。
	///
	/// <para>触发点与「安息」一致（只有显式丢弃才会派发 <c>AfterCardDiscarded</c>），
	/// 但不挂「安息」关键字，因此不会被安息的锁墓语义 / 安息相关判定（如「冥渡」）牵连。</para>
	/// </remarks>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (card == this && Owner.Creature.CombatState is { } combatState)
		{
			await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies, _discardDamage, Owner.Creature, this, null);
		}

		await base.AfterCardDiscarded(choiceContext, card);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 稀有双段重击 → 走 u3 裂空斩链，特效是全卡池单体最华丽的一档
		// 时间轴见 effects/heidemarie_moves.json 的 HeidemarieSkyfall 键（20 层）
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.U3.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.U3))
			.WithHitCount(2)
			.Targeting(cardPlay.Target)
			// 「坠月」用碎岩命中特效，与同源的「解放极光」保持一套视觉语言
			.WithHitFx("vfx/vfx_rock_shatter")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(8m);
		_discardDamage.UpgradeValueBy(10m);
	}
}
