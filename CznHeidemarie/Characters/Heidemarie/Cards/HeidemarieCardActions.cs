using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 卡牌之间共用的动作：连接弃牌、生成极光剑、从墓地打出极光剑。
///
/// <para>抽出来集中维护，避免每张连接/生成牌各写一遍。</para>
/// </summary>
internal static class HeidemarieCardActions
{
	/// <summary>
	/// 置为 <c>true</c> 时，<see cref="DiscardOtherLinked"/> 直接跳过 —— 供两类
	/// 「不希望触发「连接」连带丢弃」的效果临时抑制：
	///
	/// <list type="bullet">
	///   <item>「万剑齐鸣」：连续打出多张连接牌，否则每打出一张就会把尚未打出的连接牌全丢掉；</item>
	///   <item>「剑冢」：从墓地重放的极光剑<strong>不算</strong>「直接使用此卡」，
	///   因此不应丢弃手牌里的连接牌（只结算自己的伤害）。</item>
	/// </list>
	/// </summary>
	internal static bool SuppressLinkDiscard { get; set; }

	/// <summary>
	/// 统计手牌中带「连接」的卡牌数量（含运行期临时赋予的）。
	/// </summary>
	/// <param name="self">当前牌（用于排除自身 / 把自身补回计数）。</param>
	/// <param name="includeSelf">
	/// 是否把自身计入。描述为「打出前」的效果计入自身。
	///
	/// <para><b>注意</b>：本方法在「出牌结算中」被调用时，<paramref name="self"/> 已经离开手牌
	/// （<c>OnPlayWrapper</c> 会先把卡移进 Play 堆），所以不能指望在 <c>PileType.Hand</c> 里找到它，
	/// 必须显式补 1。</para>
	/// </param>
	public static int CountLinkedInHand(CardModel self, bool includeSelf)
	{
		int count = PileType.Hand.GetPile(self.Owner).Cards
			.Count(c => c != self && c is HeidemarieCard holder && holder.IsLinked);

		if (includeSelf && self is HeidemarieCard selfHolder && selfHolder.IsLinked)
		{
			count++;
		}

		return count;
	}

	/// <summary>
	/// 「连接」：丢弃手牌中除 <paramref name="self"/> 外所有带「连接」关键字的牌。
	///
	/// <para><b>统一由 <see cref="HeidemarieCard.BeforeCardPlayed"/> 调用</b>：
	/// 这样「原生连接」和「运行期临时连接」的牌被打出时走同一条路
	/// （自动打出、从墓地重放同理），各卡不必再自己写一遍。</para>
	/// </summary>
	/// <param name="self">正在打出的这张连接牌（不丢自己）。</param>
	/// <param name="choiceContext">上下文。</param>
	public static async Task DiscardOtherLinked(CardModel self, PlayerChoiceContext choiceContext)
	{
		// 「万剑齐鸣」等效果会连续打出多张连接牌，期间必须抑制连接自身的连带丢弃，
		// 否则每打出一张就会把尚未打出的连接牌全部丢掉。
		if (SuppressLinkDiscard)
		{
			return;
		}

		List<CardModel> linked = PileType.Hand.GetPile(self.Owner).Cards
			.Where(c => c != self && c is HeidemarieCard holder && holder.IsLinked)
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

		IReadOnlyList<CardPileAddResult> results =
			await CardPileCmd.AddGeneratedCardsToCombat(swords, pileType, source.Owner);

		// 抽牌堆 / 弃牌堆界面上的张数是靠 CardPile.CardAddFinished 事件驱动的
		// （见 NCombatCardPile.AddCard）。而 AddGeneratedCardsToCombat 走的是
		// 「这批牌还没有场景节点」的分支：它既不会建节点，也不会触发 CardAddFinished，
		// 于是生成极光剑后张数不刷新。
		// 这里用官方提供的「预览入堆」补齐这一事件 —— NCardFlyVfx 飞到牌堆时会调用
		// Pile.InvokeCardAddFinished()（见 NCardFlyVfx.PlayAnim）。
		// 手牌不显示张数，且预览飞行会和手牌动画打架，因此只在抽牌堆 / 弃牌堆补。
		if (pileType != PileType.Hand && NCombatRoom.Instance != null)
		{
			CardCmd.PreviewCardPileAdd(
				results,
				style: results.Count > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout);
		}

		return swords;
	}

	/// <summary>
	/// 「剑冢」：从墓地重放最多 <paramref name="max"/> 张极光剑。
	///
	/// <para><b>重放不触发「连接」</b>：极光剑的「连接」条件是「<em>直接使用</em>此卡」，
	/// 由剑冢从墓地重放不算直接使用，因此<strong>不会</strong>丢弃手牌里的连接牌 ——
	/// 靠 <see cref="SuppressLinkDiscard"/> 在重放期间抑制（与「万剑齐鸣」同一机制）。
	/// 剑自己的主动伤害与「被丢弃时」的伤害照常结算。</para>
	///
	/// <para>极光剑主动打出是<strong>指向</strong>伤害（<c>TargetType.AnyEnemy</c>），
	/// 这里给 <see cref="CardCmd.AutoPlay"/> 传 <c>null</c> 目标 ——
	/// <c>AutoPlay</c> 会对 <c>AnyEnemy</c> 自动补一个随机敌人，
	/// 即每张剑各打一个随机敌人，可以重复命中同一个。</para>
	///
	/// <para>踢掉「本回合被安息锁定」的极光剑：锁墓兜底补丁同样会拦住它们，
	/// 这里提前过滤只是少走一次失败分支。当前没有给剑挂运行期安息的来源，
	/// 属防御性写法（判定见 <see cref="HeidemarieRestLock"/>）。</para>
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

		if (swords.Count == 0)
		{
			return;
		}

		// 重放期间抑制「连接」的连带丢弃。
		// 用「进/出各存一次原值」而不是直接写 false，是为了重入安全：
		// 剑冢本身可能是被「万剑齐鸣」自动打出的，此时退出后必须把抑制态还原成 true。
		bool previousSuppress = SuppressLinkDiscard;
		SuppressLinkDiscard = true;
		try
		{
			foreach (CardModel sword in swords)
			{
				// 从墓地直接打出：AutoPlay 的 OnPlayWrapper 会先把它移进 Play 堆，
				// 结算完再按结果堆（守墓 → 回到墓地）。
				await CardCmd.AutoPlay(choiceContext, sword, null);
			}
		}
		finally
		{
			SuppressLinkDiscard = previousSuppress;
		}
	}
}
