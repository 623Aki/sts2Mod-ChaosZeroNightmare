using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「葬光」：1 费技能（公共牌）。
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

	/// <summary>1 费、技能、公共牌（Common）、目标为自身。</summary>
	/// <remarks>
	/// 稀有度由 Uncommon 降为 Common：手牌 → 剑的转化器属于核心循环的一环，
	/// 需要出现在前期奖励里，而不是只在中后期才能摸到。
	/// </remarks>
	public HeidemarieBuryLight()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <summary>
	/// 演出档位显式锁「结阵」（u2_buff_ready → u2_buff_play）。
	///
	/// <para><b>为什么要覆盖</b>：默认档位规则是「Rare / ≥3 费 → 蓄势，
	/// Uncommon / ≥2 费 → 结阵，其余 → 施法」。本牌降为 Common 且只有 1 费后，
	/// 默认会掉到施法档（<c>buff_*</c>），与动画对照表登记的 u2_buff 不一致。
	/// 本次只调稀有度、不动演出，因此在卡上显式钉住档位。</para>
	/// </summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.Guard;

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
