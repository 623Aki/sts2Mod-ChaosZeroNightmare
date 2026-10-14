using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「冥渡」：0 费技能（公共牌）。
///
/// <para>本回合内赋予手牌中带<b>安息</b>的卡牌（<see cref="IRestCard"/>）<b>连接</b>。
/// 升级后再额外赋予它们<b>保留</b>。</para>
/// </summary>
public sealed class HeidemarieRequiem : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>0 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieRequiem()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.ToList())
		{
			if (card is not HeidemarieCard holder || !holder.IsRestCard)
			{
				continue;
			}

			holder.GrantRuntimeLinkThisTurn();

			if (IsUpgraded)
			{
				CardCmd.ApplyKeyword(card, CardKeyword.Retain);
			}
		}

		return Task.CompletedTask;
	}

	protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
