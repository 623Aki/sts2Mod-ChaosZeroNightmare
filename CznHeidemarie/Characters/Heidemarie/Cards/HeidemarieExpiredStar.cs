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
/// 「过期的星星」：8 费技能（稀有牌），关键字<b>保留</b>。
///
/// <para>获得 <c>Energy</c> 点能量。<b>减费机制（定稿口径）</b>：当本牌在你的
/// 抽牌堆或手牌中时，你每造成 1 次伤害，本牌费用 -1；
/// 直到打出本牌，费用才恢复原值（打出时按已减费用支付）；
/// 打出后再重新减费。</para>
///
/// <para><b>实现口径</b>：每次伤害加一层
/// <c>EnergyCost.AddUntilPlayed(-1, reduceOnly: true)</c> ——
/// 相对修正、只在能降低当前费用时生效（0 封底）、打出时由引擎自动清空
/// （<c>CardModel.OnPlayWrapper</c> → <c>CardEnergyCost.AfterCardPlayedCleanup</c>）。
/// 「打出 → 恢复 → 重新减费」整条链不需要本牌自己记账；
/// 出牌瞬间本牌已在 Play 堆，跳过记账，天然不会吃打出当次的伤害。</para>
///
/// <para><b>作用域（本次修复的关键）</b>：只在抽牌堆 / 手牌里累计 ——
/// 旧实现用 <c>EnergyCost.AddThisCombat</c> 全战斗记账，躺进弃牌堆也照减、
/// 且减费跨「打出」永久保留，均不符合定稿机制，已废弃。</para>
/// </summary>
public sealed class HeidemarieExpiredStar : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new EnergyVar(3),
	};

	/// <summary>8 费、技能、稀有牌、目标为自身、保留。</summary>
	public HeidemarieExpiredStar()
		: base(8, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Retain,
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		EnergyHoverTip,
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 本牌在抽牌堆 / 手牌中时，你每造成 1 次伤害，本牌费用 -1。
	/// 「打出后恢复原值、再重新减费」全部由引擎的
	/// <c>AfterCardPlayedCleanup</c>（清 UntilPlayed 层）兜底，本牌不记状态。
	/// </remarks>
	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (dealer == Owner.Creature
			&& Pile is { Type: PileType.Draw or PileType.Hand }
			&& EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
		{
			// 相对 -1、仅减不增、打出自动清空（AfterCardPlayedCleanup）
			EnergyCost.AddUntilPlayed(-1, reduceOnly: true);
		}

		await base.AfterDamageGiven(choiceContext, dealer, result, props, target, cardSource);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1m);
}
