using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「万剑齐鸣」：3 费技能（稀有牌），<b>消耗</b>。
///
/// <para>按手牌顺序打出手中所有<b>连接</b>卡牌（不含自身）。
/// 期间抑制「连接」自身的连带丢弃，避免尚未打出的连接牌被前一张丢掉。</para>
/// </summary>
public sealed class HeidemarieResonance : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <summary>3 费、技能、稀有牌、目标为自身、消耗。</summary>
	public HeidemarieResonance()
		: base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> linked = PileType.Hand.GetPile(Owner).Cards
			.Where(c => c != this && c is HeidemarieCard holder && holder.IsLinked)
			.ToList();

		// 进/出各存一次原值：这些被自动打出的牌里可能有「剑冢」，
		// 它在重放极光剑时也会置位抑制标记，退出时必须还原而不是硬写 false。
		bool previousSuppress = HeidemarieCardActions.SuppressLinkDiscard;
		HeidemarieCardActions.SuppressLinkDiscard = true;
		try
		{
			foreach (CardModel card in linked)
			{
				await CardCmd.AutoPlay(choiceContext, card, null);
			}
		}
		finally
		{
			HeidemarieCardActions.SuppressLinkDiscard = previousSuppress;
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
