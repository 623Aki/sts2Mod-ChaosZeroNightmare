using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「献锋」：1 费技能（公共牌），<b>消耗</b>。
///
/// <para>丢弃手牌中当前费用最高的一张，获得 <c>Strength</c> 点力量。</para>
///
/// <para><b>不弹选择界面</b>：卡面写死了「费用最高的 1 张」，让它自己算即可 ——
/// 少一次点击，也让「本回合要不要留高费牌」变成一个真实的决策点。</para>
///
/// <para>费用比较用 <c>EnergyCost.GetWithModifiers(CostModifiers.All)</c>（含本回合的临时减费），
/// 这样「破晓」之类临时降费的牌不会被误判成最贵的那张。</para>
///
/// <para>这是本角色<strong>第一张非 Power 的永续力量来源</strong>：把「攒着的高费废牌」
/// 换成成长，与「破晓」（消耗→返还能量）形成两条不同的消耗曲线。</para>
/// </summary>
public sealed class HeidemarieOfferEdge : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DynamicVar("Strength", 2m),
	};

	/// <summary>1 费、技能、公共牌、目标为自身、消耗。</summary>
	public HeidemarieOfferEdge()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 手牌快照：本牌已离开手牌
		List<CardModel> hand = PileType.Hand.GetPile(Owner).Cards.ToList();

		if (hand.Count > 0)
		{
			// 取费用最高的一张；并列时用 Rng 取一，避免总丢同一张
			int highest = hand.Max(c => c.EnergyCost.GetWithModifiers(CostModifiers.All));
			List<CardModel> candidates = hand
				.Where(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) == highest)
				.ToList();

			CardModel? victim = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
			if (victim != null)
			{
				await CardCmd.Discard(choiceContext, victim);
			}
		}

		await PowerCmd.Apply<StrengthPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Strength"].BaseValue,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars["Strength"].UpgradeValueBy(2m);
}
