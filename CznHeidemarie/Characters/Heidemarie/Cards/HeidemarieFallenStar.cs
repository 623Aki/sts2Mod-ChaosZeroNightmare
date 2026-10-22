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
		//
		// 「安息」的锁墓判定只看牌是否还在墓地（HeidemarieRestLock.IsLocked 判 Pile.Type），
		// 牌一旦被移到抽牌堆就自然不再被拦，所以这个作用域只需要包住移堆阶段。
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

		// 「守墓」的剔除发生在 Shuffle 内部的 ModifyShuffleOrder，光把牌塞进抽牌堆不够：
		// 守墓牌会被从洗牌列表里剔除，而 Shuffle 结尾的批量 Add 对「不在列表里、却仍在堆里」的牌
		// 既不删除也不重排 —— 整堆重排完后它反而停在堆顶，于是一洗必被先抽到。
		// 因此这里另开一个「忽略守墓」的豁免作用域，让这次洗牌的列表保留守墓牌。
		// （与上面的安息绕过分开：守墓豁免要跨 Shuffle 内部的 await，安息锁不需要。）
		using (HeidemarieRestLock.BypassGraveKeep(Owner))
		{
			await CardPileCmd.Shuffle(choiceContext, Owner);
		}

		await CardPileCmd.Draw(choiceContext, _draw.IntValue, Owner);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
