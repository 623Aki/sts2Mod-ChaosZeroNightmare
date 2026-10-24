using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「砺锋」：1 费技能（公共牌）。
///
/// <para>丢弃最多 2 张牌，每丢弃 1 张，获得 1 层<b>极夜</b>
/// （每层使下一张极光剑伤害 +100%）。</para>
///
/// <para><b>增伤通道复用「极夜」</b>：给
/// <see cref="HeidemarieNightPower"/> 叠加「张数」层。
/// 层数语义统一后不会出现「两个图标描述同一件事」的歧义，
/// 也自动共享「极夜按张数计费 + 单出口加算」的既有逻辑
/// （见 <see cref="HeidemarieAuroraDamagePoolPower"/>）。</para>
/// </summary>
public sealed class HeidemarieWhetEdge : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 最多可弃的张数
		new CardsVar(2),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜+1」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieWhetEdge()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
		{
			return;
		}

		// MinSelect = 0：可以一张都不弃，此时不加增伤
		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
			null,
			this)).ToList();

		if (discarded.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, discarded);

		// Common·1 费 ⇒ 每弃 1 张给 1 层极夜
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			discarded.Count,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
