using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「引锋召刃」：1 费技能（稀有牌）。
///
/// <para>关键字<b>保留</b>（原生）。从抽牌堆随机抽 2 张攻击牌，
/// 并在本回合内赋予手牌中所有攻击牌<b>连接</b>。</para>
/// </summary>
public sealed class HeidemarieCallBlade : HeidemarieCard
{
	/// <summary>抽取的攻击牌张数。</summary>
	private const int DrawCount = 2;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Retain,
	};

	/// <summary>1 费、技能、稀有牌、目标为自身、保留。</summary>
	public HeidemarieCallBlade()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 从抽牌堆随机取 2 张攻击牌（抽取类效果不选目标，直接入手）
		List<CardModel> attacks = PileType.Draw.GetPile(Owner).Cards
			.Where(c => c.Type == CardType.Attack)
			.ToList();

		List<CardModel> picked = new();
		for (int i = 0; i < DrawCount && attacks.Count > 0; i++)
		{
			CardModel? card = Owner.RunState.Rng.CombatCardSelection.NextItem(attacks);
			if (card == null)
			{
				break;
			}

			picked.Add(card);
			attacks.Remove(card);
		}

		foreach (CardModel card in picked)
		{
			await CardPileCmd.Add(card, PileType.Hand);
		}

		// 本回合内赋予手牌中攻击牌「连接」
		foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.ToList())
		{
			if (card is HeidemarieCard holder && card.Type == CardType.Attack)
			{
				holder.GrantRuntimeLinkThisTurn();
			}
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
