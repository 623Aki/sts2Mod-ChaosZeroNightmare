using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「缝缝补补」：0 费技能（罕见牌）。
///
/// <para>抽 <c>Cards</c> 张牌，然后丢弃手牌中的 <c>Discard</c> 张牌。</para>
///
/// <para>与「落刃」（攻击 + 自动弃牌）互补：这是<strong>纯手牌整理</strong>，
/// 玩家自己挑要丢什么 —— 正好喂给「壁仞千里」（弃牌→格挡）、
/// 「情绪爆发」（弃牌→能量）与「守望 / 葬光」这类弃牌收益端。</para>
///
/// <para>⚠️ 顺序不能反：<b>必须先等抽牌完成再弹选择界面</b>，
/// 否则玩家看到的是抽牌前的手牌，新抽到的牌不在选项里。</para>
/// </summary>
public sealed class HeidemariePatchwork : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new CardsVar(2),
		new DynamicVar("Discard", 2m),
	};

	/// <summary>0 费、技能、罕见牌、目标为自身。</summary>
	public HeidemariePatchwork()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

		// 本牌打出时已离开手牌，这里数到的就是「其余手牌」
		int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
		int want = DynamicVars["Discard"].IntValue;
		if (handCount == 0 || want <= 0)
		{
			return;
		}

		// 手牌不足时按实际张数要（MinSelect 不能大于可选数）
		int max = Math.Min(want, handCount);

		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, max, max),
			null,
			this)).ToList();

		if (discarded.Count > 0)
		{
			// 批量弃牌：禁止循环单张（Sly 时机）
			await CardCmd.Discard(choiceContext, discarded);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
