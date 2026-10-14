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
/// 「缀星」：0 费技能（公共牌）。
///
/// <para>复用原版选牌界面，选择手牌中的 <c>Cards</c> 张牌，赋予其<b>连接</b>。
/// 描述未限定「本回合」，故连接持续到本场战斗结束。</para>
/// </summary>
public sealed class HeidemarieBindStar : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(1),
	};

	/// <summary>0 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieBindStar()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<CardModel> chosen = await CardSelectCmd.FromHand(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue),
			filter: null,
			source: this);

		foreach (CardModel card in chosen.ToList())
		{
			if (card is HeidemarieCard holder)
			{
				holder.GrantRuntimeLinkThisCombat();
			}
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
