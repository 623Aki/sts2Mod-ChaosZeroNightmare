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
/// 「窥星择光」：1 费技能（公共牌）。
///
/// <para>查看抽牌堆顶 <c>View</c> 张卡牌，选择其中 1 张加入手牌，其余丢弃。
/// 被丢弃的牌会触发「安息」等丢弃类效果。</para>
/// </summary>
public sealed class HeidemarieForesee : HeidemarieCard
{
	/// <summary>查看的张数（升级 +1）。</summary>
	private readonly DynamicVar _view = new("View", 3m);

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		_view,
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieForesee()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> top = PileType.Draw.GetPile(Owner).Cards
			.Take(_view.IntValue)
			.ToList();

		if (top.Count == 0)
		{
			return;
		}

		IEnumerable<CardModel> chosen = await CardSelectCmd.FromSimpleGrid(
			choiceContext,
			top,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 1));

		CardModel? pick = chosen.FirstOrDefault();

		foreach (CardModel card in top)
		{
			if (card == pick)
			{
				await CardPileCmd.Add(card, PileType.Hand);
			}
			else
			{
				await CardCmd.Discard(choiceContext, card);
			}
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => _view.UpgradeValueBy(1m);
}
