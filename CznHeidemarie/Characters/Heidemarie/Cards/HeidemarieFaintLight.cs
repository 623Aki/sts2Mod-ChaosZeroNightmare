using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「微光」：1 费技能（稀有牌）。
///
/// <para>本回合内赋予手牌中<b>当前费用 ≤1</b>（含 0 费）的卡牌<b>连接</b>。</para>
/// </summary>
public sealed class HeidemarieFaintLight : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>1 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieFaintLight()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.ToList())
		{
			// 按「当前费用」判定（已计入升级与各类临时费用修正）
			if (card is HeidemarieCard holder && card.EnergyCost.GetResolved() <= 1)
			{
				holder.GrantRuntimeLinkThisTurn();
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
