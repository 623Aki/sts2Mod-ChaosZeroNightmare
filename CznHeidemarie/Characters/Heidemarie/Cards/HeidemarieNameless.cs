using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「无名之辈」：1 费技能（罕见牌）。
///
/// <para>丢弃 <c>Discard</c> 张牌，然后从<strong>弃牌堆</strong>选择 1 张牌加入手牌，
/// 并使其获得<b>连接</b>。</para>
///
/// <para>「弃牌 → 捞回一张并变成连接牌」的闭环：把「壁仞千里 / 情绪爆发」吃剩的弃牌
/// 重新组装成下一波连接的弹药。</para>
///
/// <para><b>赋予的连接持续到本场战斗结束</b>：描述没有「本回合内」字样，
/// 按 §2.9 的口径走 <c>GrantRuntimeLinkThisCombat</c>。</para>
///
/// <para><b>候选过滤</b>：本回合被「安息」锁在墓地的牌拿不出来，
/// 必须在传进选择界面之前剔除（选择面板没有「不可选」谓词）——
/// 与「撷光」同一做法，判定统一走 <see cref="HeidemarieRestLock"/>。</para>
/// </summary>
public sealed class HeidemarieNameless : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 先丢弃的张数（升级改为费用 -1，张数不变）
		new DynamicVar("Discard", 2m),
	};

	/// <summary>1 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieNameless()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await DiscardFromHand(choiceContext);
		await RecoverLinkedCard(choiceContext);
	}

	/// <summary>从手牌选 <c>Discard</c> 张丢弃（手牌不足时按实际张数要）。</summary>
	private async Task DiscardFromHand(PlayerChoiceContext choiceContext)
	{
		// 本牌打出时已离开手牌，这里数到的就是「其余手牌」
		int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
		int want = DynamicVars["Discard"].IntValue;
		if (handCount == 0 || want <= 0)
		{
			return;
		}

		int count = Math.Min(want, handCount);
		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, count, count),
			null,
			this)).ToList();

		if (discarded.Count > 0)
		{
			// 批量弃牌：禁止循环单张（Sly 时机）
			await CardCmd.Discard(choiceContext, discarded);
		}
	}

	/// <summary>从弃牌堆选 1 张（未被安息锁住的）牌，赋予连接后移入手牌。</summary>
	private async Task RecoverLinkedCard(PlayerChoiceContext choiceContext)
	{
		// 先取快照再过滤：下面会改牌堆，不能遍历活集合。
		List<CardModel> candidates = PileType.Discard.GetPile(Owner).Cards
			.Where(card => !HeidemarieRestLock.IsLocked(card))
			.ToList();

		if (candidates.Count == 0)
		{
			return;
		}

		// 本牌有两次选择：这里的提示要说明「这一张会被加入手牌」，
		// 与第一步的「选择要丢弃的牌」区分开，因此用专属 key。
		CardSelectorPrefs prefs = new CardSelectorPrefs(
			new LocString("cards", Id.Entry + ".recoverPrompt"),
			1);

		CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
			choiceContext,
			candidates,
			Owner,
			prefs)).FirstOrDefault();

		if (chosen == null)
		{
			return;
		}

		if (chosen is HeidemarieCard holder)
		{
			// 描述未限定「本回合」→ 连接持续到本场战斗结束
			holder.GrantRuntimeLinkThisCombat();
		}

		await CardPileCmd.Add(chosen, PileType.Hand);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		// 升级：费用 -1（1 费 → 0 费）
		EnergyCost.UpgradeBy(-1);
	}
}
