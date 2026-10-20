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
/// 「坠星」：1 费技能（稀有牌），<b>消耗</b>。
///
/// <para>把手牌与弃牌堆的**全部**卡牌放入抽牌堆并洗牌，再抽 <c>Draw</c> 张牌。
/// 含「守墓」关键字的卡牌也会被一并洗入（强制绕过守墓）；
/// 本回合被「安息」锁在墓地的牌同样会被洗入（设计口径：连安息锁一起绕）。</para>
/// </summary>
public sealed class HeidemarieFallenStar : HeidemarieCard
{
	/// <summary>洗牌后抽取的张数（升级不变，升级只减费）。</summary>
	private readonly DynamicVar _draw = new("Draw", 5m);

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		_draw,
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <summary>1 费、技能、稀有牌、目标为自身、消耗。</summary>
	public HeidemarieFallenStar()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 手牌 + 弃牌堆的全部卡（含守墓牌与被安息锁定在墓地的牌）逐张移入抽牌堆。
		// 本牌按设计强制绕过墓地锁，因此开一个绕过作用域；
		// 作用域只包住移堆阶段，后面那次 Shuffle 仍按正常规则走
		// （此刻锁墓牌已经在抽牌堆，HeidemarieRestLock.IsLocked 不再拦它）。
		List<CardModel> cards = PileType.Hand.GetPile(Owner).Cards
			.Concat(PileType.Discard.GetPile(Owner).Cards)
			.ToList();

		using (HeidemarieRestLock.Bypass(Owner))
		{
			foreach (CardModel card in cards)
			{
				await CardPileCmd.Add(card, PileType.Draw);
			}
		}

		await CardPileCmd.Shuffle(choiceContext, Owner);
		await CardPileCmd.Draw(choiceContext, _draw.IntValue, Owner);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
