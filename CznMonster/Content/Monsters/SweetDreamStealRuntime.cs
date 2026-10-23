using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 美梦的偷牌运行时（美梦设计文档 §1.2，CZN 证据链 cs02_0250_01/02）：
/// <list type="bullet">
/// <item>偷取：该玩家回合抽牌完成后，随机偷走 <see cref="StealPerTurn"/> 张手牌（阶段 1 = 1 /
/// 阶段 2 = 2，v1.4 裁定），移出战斗（持有区 = <see cref="Stash"/> 列表）；</item>
/// <item>归还：梦魇计数归零时全部放回该玩家手牌（满手自动落弃牌堆）；</item>
/// <item>兜底：战斗结束仍未归还的持有牌不做任何补偿——战斗手牌是卡组版本的克隆
/// （<c>PopulateCombatState</c>），卡组本体始终安全，无需归还。</item>
/// </list>
///
/// <para><b>联机约束（2026-10 修正）</b>：持有区按 <c>Player.NetId</c> 分桶，
/// 不能所有玩家混在一个 List 里——两端桶内容/顺序一旦不同，
/// 归还就会把牌还错人，且 <see cref="Count"/> 会虚报。
/// 另外 <see cref="StealPerTurn"/> 是进程级状态，必须在每场战斗开始时无条件重置，
/// 不能只依赖 Power 的 <c>AfterCombatEnd</c>（Boss 死亡时 Power 会先被摘掉，
/// 那条钩子不会执行，残留的「每回合偷 2 张」会让下一场战斗多消耗
/// <c>CombatCardSelection</c> 随机数，进而与对端劈叉）。</para>
///
/// <para>零 Harmony：偷取（<c>AfterPlayerTurnStartLate</c>）与归还（<c>AfterDamageReceived</c>
/// 归零分支）两个钩子全部由 <see cref="CznMonster.Content.Powers.SweetDreamCountdownPower"/>
/// 的 PowerModel 钩子驱动（铁律 6 零侵入口径）。</para>
/// </summary>
internal static class SweetDreamStealRuntime
{
	/// <summary>持有区：按玩家 NetId 分桶，存放被偷走、当前不在任何战斗牌堆里的手牌（战斗克隆体）。</summary>
	private static readonly Dictionary<ulong, List<CardModel>> Stash = new Dictionary<ulong, List<CardModel>>();

	/// <summary>战斗生命周期是否已订阅。</summary>
	private static bool _subscribed;

	/// <summary>持有区当前牌数（所有玩家合计，能力动态描述用）。</summary>
	public static int Count => Stash.Values.Sum(list => list.Count);

	/// <summary>
	/// 每回合偷牌张数（能力动态描述与实际偷取共用同一真值）：阶段 1 = 1，
	/// 阶段 2 入场（梦魇碾压）后 = 2。
	///
	/// <para>每场战斗开始时由 <see cref="Initialize"/> 订阅的 <c>CombatSetUp</c> 无条件复位，
	/// 不依赖 Power 的 <c>AfterCombatEnd</c>（Boss 被击杀时那条钩子不会执行）。</para>
	/// </summary>
	public static int StealPerTurn { get; private set; } = 1;

	/// <summary>
	/// 订阅战斗开始事件，用于无条件复位持有区与偷牌量。
	/// 由 <c>MainFile.Initialize</c> 调用；幂等。
	/// </summary>
	public static void Initialize()
	{
		if (_subscribed)
		{
			return;
		}
		_subscribed = true;
		CombatManager.Instance.CombatSetUp += OnCombatSetUp;
	}

	private static void OnCombatSetUp(CombatState state)
	{
		// 战斗开始一定没有「上一场被偷的牌」，把残留（含 StealPerTurn）一并清掉。
		Clear();
	}

	/// <summary>
	/// 切换到阶段 2 的偷牌量（由 <see cref="SweetDream"/> 的梦魇碾压行动调用，幂等；
	/// 阶段切换不吞牌，已持有的偷牌跨阶段保留到归零）。
	/// </summary>
	public static void EnterPhase2()
	{
		StealPerTurn = 2;
	}

	/// <summary>战斗结束（胜利/失败/逃遁）清空缓存：战斗克隆体已随战斗态销毁，卡组本体从未离开牌堆，无需归还。</summary>
	public static void Clear()
	{
		Stash.Clear();
		StealPerTurn = 1;
	}

	private static List<CardModel> Bucket(Player player)
	{
		if (!Stash.TryGetValue(player.NetId, out List<CardModel>? list))
		{
			list = new List<CardModel>();
			Stash[player.NetId] = list;
		}
		return list;
	}

	/// <summary>
	/// 偷走 <paramref name="player"/> 的 1 张随机手牌；手牌为空则跳过（调用方按
	/// <see cref="StealPerTurn"/> 循环，阶段 2 连偷 2 张）。
	/// 随机取牌使用 <see cref="MegaCrit.Sts2.Core.Random.RunRngSet.CombatCardSelection"/> 桶。
	/// </summary>
	public static async Task StealOne(Player player)
	{
		if (player.PlayerCombatState == null)
		{
			return;
		}
		CardPile hand = PileType.Hand.GetPile(player);
		if (hand.Cards.Count == 0)
		{
			return;
		}
		int index = player.RunState.Rng.CombatCardSelection.NextInt(hand.Cards.Count);
		CardModel card = hand.Cards[index];
		if (card.Pile?.Type != PileType.Hand)
		{
			// 随机到手后差的这拍牌已被其他流转走（弃置/打出队列），本回合不补选
			return;
		}
		Bucket(player).Add(card);
		await CardPileCmd.RemoveFromCombat(card);
	}

	/// <summary>
	/// 把持有区全部归还 <paramref name="targetPile"/>（归零 → 手牌，满手自动落弃牌堆）。
	/// 归还前复活 <see cref="CardModel.HasBeenRemovedFromState"/> 登记，否则会被
	/// <c>CardPileCmd.Add</c> 的守卫拒绝。已因其他流转回牌堆的牌幂等跳过。
	///
	/// <para>每个桶里的牌都属于固定的 <c>Owner</c>，<c>CardPileCmd.Add</c> 会按
	/// <c>Owner</c> 决定去哪个牌堆，所以「一次性全还」在多人局也是正确的；
	/// 分桶的意义是让计数与顺序在两端一致，而不是归还目标不同。</para>
	/// </summary>
	public static async Task ReturnAll(PileType targetPile)
	{
		if (Stash.Count == 0)
		{
			return;
		}
		List<CardModel> cards = Stash.Values.SelectMany(list => list).ToList();
		Stash.Clear();
		foreach (CardModel card in cards)
		{
			if (card.Owner == null)
			{
				continue;
			}
			if (card.HasBeenRemovedFromState)
			{
				card.HasBeenRemovedFromState = false;
			}
			if (card.Pile != null)
			{
				// 已在某个牌堆（防御性幂等），不再动它
				continue;
			}
			await CardPileCmd.Add(card, targetPile);
		}
	}

	/// <summary>
	/// 生成悬浮文本（牌只显示名称）。
	///
	/// <para><b>只列本地玩家自己的牌</b>：多人局里把队友被偷的牌名也列出来，
	/// 等于泄露队友手牌信息（手牌是隐藏信息）。</para>
	/// </summary>
	public static string BuildStolen()
	{
		List<CardModel>? mine = null;
		if (LocalContext.NetId.HasValue)
		{
			Stash.TryGetValue(LocalContext.NetId.Value, out mine);
		}
		if (mine == null || mine.Count == 0)
		{
			return new LocString("powers", "SWEET_DREAM_COUNTDOWN_POWER.stolen_none").GetFormattedText();
		}
		string sep = new LocString("powers", "SWEET_DREAM_COUNTDOWN_POWER.stolen_sep").GetFormattedText();
		return string.Join(
			"\n",
			mine
				.Where(c => c.Owner?.Creature != null)
				.GroupBy(c => c.Owner!.Creature!.Name)
				.Select(group =>
				{
					LocString line = new LocString("powers", "SWEET_DREAM_COUNTDOWN_POWER.stolen_line");
					line.Add("Owner", group.Key);
					line.Add("Cards", string.Join(sep, group.Select(c => c.Title)));
					return line.GetFormattedText();
				}));
	}
}
