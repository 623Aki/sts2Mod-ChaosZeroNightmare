using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「破晓」：1 费技能（稀有牌），<b>消耗</b>。
///
/// <para>获得 <c>Energy</c> 点能量；关键字<b>焚烧</b>：本牌被消耗时，
/// 在手牌生成 <c>Cards</c> 张极光剑。</para>
///
/// <para>官方 <c>Adrenaline</c> 的角色化改写，补上本角色的能量缺口；
/// 「焚烧」与本角色「解放极光」走同一个派发点（<c>AfterCardExhausted</c> → <see cref="IBurnCard"/>）。</para>
///
/// <para>注意本题的消耗是<b>打出后</b>由 <c>CardKeyword.Exhaust</c> 触发的，
/// 与「焚烧」的触发点天然衔接：牌先离场、再消耗、再发动焚烧。</para>
/// </summary>
public sealed class HeidemarieDaybreak : HeidemarieCard, IBurnCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new EnergyVar(2),
		// 焚烧生成的极光剑张数（升级不改变，升级只降费）
		new CardsVar(2),
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <summary>1 费、技能、稀有牌、目标为自身、消耗。</summary>
	public HeidemarieDaybreak()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

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
	/// <remarks>关键字「焚烧」：本牌被消耗时在手牌生成极光剑。</remarks>
	public async Task OnBurn(PlayerChoiceContext choiceContext, bool causedByEthereal)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(
			this,
			choiceContext,
			DynamicVars.Cards.IntValue,
			PileType.Hand);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
