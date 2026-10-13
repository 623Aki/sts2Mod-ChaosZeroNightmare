using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 卡牌之间共用的动作：链接弃牌、生成极光剑、从墓地打出极光剑。
///
/// <para>抽出来集中维护，避免每张链接/生成牌各写一遍。</para>
/// </summary>
internal static class HeidemarieCardActions
{
	/// <summary>
	/// 「链接」：丢弃手牌中除 <paramref name="self"/> 外所有带「链接」关键字的牌。
	/// </summary>
	/// <param name="self">正在打出的这张链接牌（不丢自己）。</param>
	/// <param name="choiceContext">上下文。</param>
	public static async Task DiscardOtherLinked(CardModel self, PlayerChoiceContext choiceContext)
	{
		List<CardModel> linked = PileType.Hand.GetPile(self.Owner).Cards
			.Where(c => c != self && c is ILinkCard)
			.ToList();

		if (linked.Count > 0)
		{
			// 一次性交给 DiscardAndDraw，时序上先建好整批再触发挂钩（弃牌类效果的推荐写法）
			await CardCmd.Discard(choiceContext, linked);
		}
	}

	/// <summary>
	/// 生成若干张「极光剑」并放入指定牌堆（手牌 / 抽牌堆），返回生成的卡实例。
	/// </summary>
	/// <param name="source">发起生成的牌（用于取所属玩家与卡池）。</param>
	/// <param name="choiceContext">上下文。</param>
	/// <param name="count">生成张数。</param>
	/// <param name="pileType">放入哪个战斗牌堆。</param>
	/// <param name="grantedRecycle">
	/// 赋予这些极光剑的「回收」层数（0 = 不赋予，见 <see cref="HeidemarieCard.RecycleRemaining"/>）。
	///
	/// <para><b>必须在入堆之前赋值</b>：卡面文字是卡片节点建立时
	/// （<c>NCard.UpdateVisuals</c> → <c>CardModel.GetDescriptionForPile</c>）读一次词条状态的，
	/// 先入堆再补赋值会让卡面停在没有「回收」的旧文本上；而悬停提示是每次悬停即时算的，
	/// 于是出现「提示里有、卡面上没有」的错位。</para>
	/// </param>
	public static async Task<List<CardModel>> GenerateAuroraSwords(
		CardModel source,
		PlayerChoiceContext choiceContext,
		int count,
		PileType pileType,
		int grantedRecycle = 0)
	{
		List<CardModel> swords = new();
		if (count <= 0)
		{
			return swords;
		}

		ICombatState? combatState = source.Owner.Creature.CombatState;
		if (combatState == null)
		{
			return swords;
		}

		for (int i = 0; i < count; i++)
		{
			HeidemarieAuroraSword sword = combatState.CreateCard<HeidemarieAuroraSword>(source.Owner);
			if (grantedRecycle > 0 && sword.RecycleRemaining < grantedRecycle)
			{
				sword.RecycleRemaining = grantedRecycle;
			}

			swords.Add(sword);
		}

		await CardPileCmd.AddGeneratedCardsToCombat(swords, pileType, source.Owner);
		return swords;
	}

	/// <summary>
	/// 「剑冢」：从墓地打出最多 <paramref name="max"/> 张极光剑，<strong>不指定目标</strong>。
	///
	/// <para>极光剑本身是 <c>TargetType.RandomEnemy</c>（非指向），因此这里给
	/// <see cref="CardCmd.AutoPlay"/> 传 <c>null</c> 目标：<c>AutoPlay</c> 只对
	/// <c>AnyEnemy</c> 做随机补目标，<c>RandomEnemy</c> 保持 null，
	/// 由极光剑自己的 <c>TargetingRandomOpponents</c> 随机命中 ——
	/// 即每张剑各打一个随机敌人，可以重复命中同一个。</para>
	///
	/// <para>踢掉「本回合被安息锁定」的极光剑（当前没有这种组合，留作接口）。</para>
	/// </summary>
	public static async Task PlayAuroraSwordsFromDiscard(
		CardModel source,
		PlayerChoiceContext choiceContext,
		int max)
	{
		List<CardModel> swords = PileType.Discard.GetPile(source.Owner).Cards
			.Where(c => c is HeidemarieAuroraSword && !(c is HeidemarieCard h && h.RestLockedThisTurn))
			.Take(max)
			.ToList();

		foreach (CardModel sword in swords)
		{
			// 从墓地直接打出：AutoPlay 的 OnPlayWrapper 会先把它移进 Play 堆，
			// 结算完再按结果堆（守墓 → 回到墓地）。
			await CardCmd.AutoPlay(choiceContext, sword, null);
		}
	}
}
