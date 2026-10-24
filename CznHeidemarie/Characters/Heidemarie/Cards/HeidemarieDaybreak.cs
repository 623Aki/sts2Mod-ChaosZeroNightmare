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
/// <para>获得 <c>Energy</c> 点能量；关键字<b>感应</b>：本牌从抽牌堆进入手牌时，
/// 在手牌生成 <c>Cards</c> 张极光剑。</para>
///
/// <para>官方 <c>Adrenaline</c> 的角色化改写，补上本角色的能量缺口。</para>
///
/// <para><b>2026-10-24 机制调整</b>：原「焚烧（本牌被消耗时生成极光剑）」改为「感应」——
/// 触发点从「打出后的消耗」提前到「抽到手牌」，与本角色「弃掉的牌洗回再抽到」的循环天然咬合；
/// 「焚烧」仍保留给「解放极光」使用（派发点 <c>AfterCardExhausted</c> 不变）。</para>
/// </summary>
public sealed class HeidemarieDaybreak : HeidemarieCard, ISenseCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new EnergyVar(2),
		// 感应生成的极光剑张数（升级不改变，升级只降费）
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
	/// <remarks>关键字「感应」：从抽牌堆进入手牌时，在手牌生成极光剑（由基类统一分派）。</remarks>
	public Task OnSense(PlayerChoiceContext choiceContext)
		=> HeidemarieCardActions.GenerateAuroraSwords(
			this,
			choiceContext,
			DynamicVars.Cards.IntValue,
			PileType.Hand);

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
