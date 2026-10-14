using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「撷光」：0 费技能（稀有牌），<b>消耗</b>。
///
/// <para>从抽牌堆或弃牌堆中选择 <c>Cards</c> 张卡牌，赋予其<b>连接</b>并移入手牌。
/// 描述未限定「本回合」，故连接持续到本场战斗结束。</para>
/// </summary>
public sealed class HeidemarieRecover : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(1),
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <summary>0 费、技能、稀有牌、目标为自身、消耗。</summary>
	public HeidemarieRecover()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> candidates = PileType.Draw.GetPile(Owner).Cards
			.Concat(PileType.Discard.GetPile(Owner).Cards)
			.ToList();

		if (candidates.Count == 0)
		{
			return;
		}

		IEnumerable<CardModel> chosen = await CardSelectCmd.FromSimpleGrid(
			choiceContext,
			candidates,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue));

		foreach (CardModel card in chosen.ToList())
		{
			if (card is HeidemarieCard holder)
			{
				holder.GrantRuntimeLinkThisCombat();
			}

			await CardPileCmd.Add(card, PileType.Hand);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
