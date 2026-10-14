using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「万众英雄」：1 费技能（公共牌）。
///
/// <para>关键字<b>安息</b>：非回合结束被丢弃时发动本效果。</para>
/// <para>正常抽 <c>Cards</c> 张牌，并在本回合内赋予抽到的牌<b>连接</b>。</para>
/// </summary>
public sealed class HeidemarieAllHeroes : HeidemarieCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(3),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieAllHeroes()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => DrawAndLink(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> DrawAndLink(choiceContext);

	/// <summary>抽牌并给抽到的牌挂本回合的「连接」。</summary>
	private async Task DrawAndLink(PlayerChoiceContext choiceContext)
	{
		IEnumerable<CardModel> drawn = await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
		foreach (CardModel card in drawn.ToList())
		{
			if (card is HeidemarieCard holder)
			{
				holder.GrantRuntimeLinkThisTurn();
			}
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
