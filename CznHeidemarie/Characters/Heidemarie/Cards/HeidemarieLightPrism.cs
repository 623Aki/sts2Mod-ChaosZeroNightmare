using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「光棱」：1 费攻击（罕见牌）。
///
/// <para>造成等同于你当前<b>格挡值</b>的伤害（不消耗格挡）。</para>
///
/// <para>「格挡 → 输出」的桥梁：照抄官方 <c>BodySlam</c> 的写法 ——
/// 用 <see cref="CalculatedDamageVar"/> 的乘子读 <c>Owner.Creature.Block</c>，
/// 因此吃格挡的一切变化（含战斗中途涨落）都会自动反映到伤害上。</para>
/// </summary>
public sealed class HeidemarieLightPrism : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	/// <remarks>
	/// 三个变量是官方「计算型伤害」的固定组合（见 <c>BodySlam</c>）：
	/// 基础值 0 + 每点乘子 1 + 乘子来源 = 当前格挡。
	/// </remarks>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(3)
	{
		new CalculationBaseVar(0m),
		new ExtraDamageVar(1m),
		new CalculatedDamageVar(ValueProp.Move)
			.WithMultiplier((CardModel card, Creature? _) => card.Owner.Creature.Block),
	};

	/// <summary>1 费、攻击、罕见牌、目标为任意敌人。</summary>
	public HeidemarieLightPrism()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.CalculatedDamage)
			.FromCard(this, cardPlay)
			// Uncommon 1 费：中·纵劈档（Attack2）
			.WithAttackerAnim(HeidemarieAttackPlan.Heavy.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Heavy))
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
