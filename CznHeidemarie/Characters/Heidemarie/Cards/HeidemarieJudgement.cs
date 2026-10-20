using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「审判」：1 费攻击（稀有牌），目标为任意敌人。<b>无视格挡</b>。
///
/// <para><b>费用定为 2</b>：原设计的「0 费 20 无视格挡」≈ 官方 3 费强度
/// （官方 0 费直伤基准约 6 点）。提到 2 费后，「20 点 + 穿透」按官方曲线约值 2\~3 费，
/// 仍是稀有牌里最强的一档单体解，但需要付出实打实的能量代价。</para>
///
/// <para><b>无视格挡的源码口径</b>：<see cref="ValueProp.Unblockable"/> 会让
/// <c>Creature.DamageBlockInternal</c> 直接跳过扣格挡；再叠加
/// <see cref="ValueProp.Unpowered"/>（来自卡牌而非生物动作的力量加成不计）
/// 与 <see cref="ValueProp.Move"/>（仍算一次攻击动作）——
/// 三者组合是官方「穿透格挡」类伤害的标准写法。</para>
///
/// <para>因为要带着这套 <see cref="ValueProp"/> 结算，伤害走
/// <c>CreatureCmd.Damage(..., DamageVar, ...)</c> 而不是 <c>DamageCmd.Attack(decimal)</c>
/// （后者只收数值、不带属性），动作由 <c>CreatureCmd.TriggerAnim</c> 单独起。</para>
/// </summary>
public sealed class HeidemarieJudgement : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(20m, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move),
	};

	/// <summary>2 费、攻击、稀有牌、指向任意敌人。</summary>
	public HeidemarieJudgement()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：重击（稀有攻击，走 u1 分支）。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackHeavy;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 先起动作，再结算伤害（命中延迟与档位表一致）
		await CreatureCmd.TriggerAnim(
			Owner.Creature,
			HeidemarieAttackPlan.Heavy.Trigger,
			HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Heavy));

		await CreatureCmd.Damage(
			choiceContext,
			cardPlay.Target,
			DynamicVars.Damage,
			Owner.Creature,
			this,
			cardPlay);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(10m);
}
