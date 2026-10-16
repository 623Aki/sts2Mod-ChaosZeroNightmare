using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「葬光」：1 费技能（罕见牌）。
///
/// <para>丢弃手牌中任意数量的牌（最多 <c>Cards</c> 张），每弃 1 张，
/// 在手牌生成 1 张「极光剑」。</para>
///
/// <para>手牌 → 剑的转化器：把废牌（连极光剑本身在内）换成等量新剑；
/// 被弃的剑仍然会触发它自己的「被丢弃时」随机伤害。</para>
/// </summary>
public sealed class HeidemarieBuryLight : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 最多可弃的张数（升级 +1）
		new CardsVar(3),
	};

	/// <summary>1 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieBuryLight()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
		{
			return;
		}

		// MinSelect = 0：可以一张都不弃（此时不生成剑）。
		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
			null,
			this)).ToList();

		if (discarded.Count == 0)
		{
			return;
		}

		// 先弃后生：若先生成，新剑会混进本次的可选范围里被自己弃掉。
		await CardCmd.Discard(choiceContext, discarded);
		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, discarded.Count, PileType.Hand);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
