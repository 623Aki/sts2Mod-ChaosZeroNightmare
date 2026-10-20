using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace CznMonster.Registration;

/// <summary>
/// 行动计数的怪物分级（设计文档 §7.3）。
/// </summary>
internal enum MonsterTier
{
	Summon,
	Normal,
	Elite,
	Boss
}

/// <summary>
/// P4 分配计数器（RollMove postfix，顺带覆盖战斗开始与召唤）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(MonsterModel), "RollMove")]
internal static class ActionCountRollMovePatch
{
	private static bool _loggedFailure;

	private static void Postfix(MonsterModel __instance)
	{
		try
		{
			if (!MonsterSettings.ActionCountEnabled)
			{
				return;
			}
			Creature? creature = __instance.Creature;
			if (creature == null)
			{
				return;
			}
			if (__instance.CombatState is not CombatState cs)
			{
				return;
			}
			if (!ActionCountTables.IsCountedCreature(__instance, cs, out MonsterTier tier))
			{
				return;
			}
			int? baseCount = ActionCountTables.CounterFor(__instance, cs, tier, __instance.NextMove);
			if (baseCount == null)
			{
				// 不可计数行动：清除计数器与显示（§7.4）
				ActionCountRuntime.Clear(creature);
				return;
			}
			// 多人缩放：×玩家数（§7.9）
			int playerCount = Math.Max(1, cs.Players.Count);
			ActionCountRuntime.Assign(creature, baseCount.Value * playerCount);
		}
		catch (System.Exception ex)
		{
			if (!_loggedFailure)
			{
				_loggedFailure = true;
				MainFile.Log.Warn($"CznMonster 行动计数分配失败：{ex}");
			}
			else
			{
				MainFile.Log.Warn($"CznMonster 行动计数分配失败：{ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}

/// <summary>
/// P5 玩家出牌后统一减询。
/// <para>
/// 不能直接在 <see cref="CardModel.OnPlayWrapper"/> 的 postfix 里减：那是 async 方法，
/// postfix 在第一次 await 就返回，牌其实还没结算完。这里拿 <c>__result</c> 的 Task，
/// 等整张牌结算完成（包括消耗/丢弃/奇巧等后续触发）后再减询。
/// </para>
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(CardModel), "OnPlayWrapper")]
internal static class ActionCountCardPlayPatch
{
	private static void Postfix(CardModel __instance, bool isAutoPlay, Task __result)
	{
		// 自动打出的牌不减询（§7.6）
		if (isAutoPlay || __result == null)
		{
			return;
		}

		TaskHelper.RunSafely(AfterCardPlaySettledAsync(__result, __instance));
	}

	private static async Task AfterCardPlaySettledAsync(Task playTask, CardModel card)
	{
		try
		{
			// 等这张牌整体结算完：OnPlay / 附魔 / 苦难 / AfterCardPlayed /
			// 结果堆移动（消耗/丢弃）/ 空手检查都在这个 Task 里。
			await playTask;

			CombatManager cm = CombatManager.Instance;
			if (!cm.IsInProgress || cm.IsOverOrEnding)
			{
				return;
			}
			if (!MonsterSettings.ActionCountEnabled)
			{
				return;
			}
			// 不能用 card.CombatState：能力牌（CardType.Power）与复制体（IsDupe）打完后
			// 结果堆是 PileType.None，会被 CardPileCmd.RemoveFromCombat 移出战斗，
			// Pile 变 null，CardModel.CombatState 随之返回 null（CardModel.cs:1018-1029），
			// 导致这类牌打出后计数器不递减。
			// CardScope 是游戏自带的兜底链：CombatState ?? Owner.Creature.CombatState ?? RunState
			// （CardModel.cs:1038），牌不在任何战斗堆时仍能拿到当前战斗。
			if (card.CardScope is not CombatState cs)
			{
				return;
			}

			// 如果牌结算过程中怪已经死了，DecrementAll 会清掉尸体，不会误触发打断。
			List<Creature> zeroed = ActionCountRuntime.DecrementAll();
			if (zeroed.Count == 0)
			{
				return;
			}
			ActionCountRuntime.MarkInterruptQueued(zeroed);
			TaskHelper.RunSafely(ActionCountRuntime.DrainAsync(cs));
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 行动计数减询失败：{ex.GetType().Name}: {ex.Message}");
		}
	}
}

/// <summary>
/// P6 打断期间挡住自然行动回合（TakeTurn prefix），由 DrainAsync 代为执行（§7.7）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(Creature), "TakeTurn")]
internal static class ActionCountTakeTurnPatch
{
	private static bool Prefix(Creature __instance)
	{
		if (!MonsterSettings.ActionCountEnabled)
		{
			return true;
		}
		try
		{
			return !ActionCountRuntime.HasPendingInterrupt(__instance);
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 行动计数拦截失败：{ex.GetType().Name}: {ex.Message}");
			return true;
		}
	}
}

/// <summary>
/// 战斗生命周期订阅入口（MainFile.Initialize 调用）。
/// </summary>
internal static class ActionCountPatches
{
	internal static void Initialize()
	{
		CombatManager.Instance.CombatSetUp += OnCombatSetUp;
		CombatManager.Instance.CombatEnded += OnCombatEnded;
	}

	private static void OnCombatSetUp(CombatState state)
	{
		if (MonsterSettings.ActionCountEnabled)
		{
			ActionCountRuntime.OnCombatSetUp(state);
		}
	}

	private static void OnCombatEnded(CombatRoom _)
	{
		ActionCountRuntime.OnCombatEnded();
	}
}

/// <summary>
/// 分级、基数公式与分类判据（设计文档 §7.3 / §7.4 / §7.8）。
/// </summary>
internal static class ActionCountTables
{
	// ── 基数公式（设计 §7.4）──────────────────────────────────────────────
	// 计数器是“阈值”而不是“倒计时”：它决定玩家这一回合多打几张牌，会多换来一次怪物行动。
	// 所以基数必须相对“玩家一回合能打几张牌 C”来定，不能拍绝对数；三个维度各管一段：
	//   章数   → 决定 C（玩家的出牌能力）
	//   档位   → 决定 A（一次额外行动值多少 = 每回合允许多几次行动）
	//   进阶   → 对结果整体收税
	//   同场数 → 把预算分摊给多只怪
	//
	//   C(act)    = 3.2 + 0.8 × actIndex            第 1/2/3 章 = 3.2 / 4.0 / 4.8
	//   Tol(tier) = 1 / A(tier)                     首领 2.00 / 精英 1.67 / 普通 1.33 / 随从 0.85
	//   GroupMul  = 1 + 0.4 × (min(n,4) - 1)        n = 同房间同档存活主敌数（随从恒 1）
	//   AscMul    = 进阶修正
	//   N0 = clamp(round(C × Tol × GroupMul × AscMul), 2, 14)
	//   N  = (N0 + IntentOff) × 玩家数
	private const double BaseCardsPerTurn = 3.2;

	private const double CardsPerActStep = 0.8;

	private const double GroupStep = 0.4;

	private const int MinCounter = 2;

	private const int MaxCounter = 14;

	/// <summary>
	/// 玩家一回合的典型出牌数 C：3 能量 / 5 抽牌 / 均费 1 费 ≈ 3 张，
	/// 之后逐章靠能量遗物、抽牌引擎与升级牌抬升。这是整条公式里唯一靠估算的量，
	/// 实测有偏差时只调这两个常量即可。
	/// </summary>
	private static double CardsPerTurn(int actIndex)
	{
		return BaseCardsPerTurn + CardsPerActStep * actIndex;
	}

	/// <summary>
	/// 每只怪每回合允许多出的行动次数 A 的倒数。
	/// 首领一次额外行动最贵 → 容忍最高（A=0.50）；随从最便宜 → 容忍最低（A≈1.18）。
	/// </summary>
	private static double Tolerance(MonsterTier tier)
	{
		switch (tier)
		{
			case MonsterTier.Boss:
				return 2.00;
			case MonsterTier.Elite:
				return 1.67;
			case MonsterTier.Normal:
				return 1.33;
			default:
				return 0.85;
		}
	}

	/// <summary>
	/// 进阶修正：按本体实际影响点分档，而不是线性硬套。
	/// A5–A7 诅咒/少药水/少好牌（玩家出牌能力下降）、A8 敌人更肉、A9 更疼、A10 双 Boss。
	/// </summary>
	private static double AscensionMultiplier(int ascensionLevel)
	{
		if (ascensionLevel >= 9)
		{
			return 0.85;
		}
		if (ascensionLevel >= 8)
		{
			return 0.90;
		}
		if (ascensionLevel >= 5)
		{
			return 0.95;
		}
		return 1.0;
	}

	/// <summary>
	/// 同房间、同档、存活的主敌数量（随从恒 1）。
	/// 多怪遭遇必须分摊预算：否则 3 只普通怪各 4，玩家打出第 4 张牌会同时逼出 3 次额外行动。
	/// </summary>
	private static int GroupSize(CombatState cs, MonsterTier tier)
	{
		if (tier == MonsterTier.Summon)
		{
			return 1;
		}
		int count = 0;
		foreach (Creature enemy in cs.Enemies)
		{
			if (!enemy.IsAlive || enemy.Monster == null || enemy.IsSecondaryEnemy)
			{
				continue;
			}
			if (TierOf(enemy, cs) == tier)
			{
				count++;
			}
		}
		return Math.Max(1, count);
	}

	/// <summary>单只敌人的档位（不含“计数范围”判定）。</summary>
	private static MonsterTier TierOf(Creature creature, CombatState cs)
	{
		if (creature.IsSecondaryEnemy)
		{
			return MonsterTier.Summon;
		}
		RoomType roomType = cs.Encounter?.RoomType ?? RoomType.Monster;
		switch (roomType)
		{
			case RoomType.Boss:
				return MonsterTier.Boss;
			case RoomType.Elite:
				return MonsterTier.Elite;
			default:
				return MonsterTier.Normal;
		}
	}

	/// <summary>
	/// 计算一只怪本次意图的基础计数（单人值，未乘玩家数）。
	/// 返回 null 表示本次意图全部不可计数（逃跑/睡眠/眩晕/隐藏）。
	/// </summary>
	public static int? CounterFor(MonsterModel monster, CombatState cs, MonsterTier tier, MoveState? move)
	{
		int? intentOffset = IntentOffsetFor(move);
		if (intentOffset == null)
		{
			return null;
		}
		Creature? creature = monster.Creature;
		if (creature == null)
		{
			return null;
		}

		int actIndex = Math.Clamp(cs.RunState.CurrentActIndex, 0, 2);
		int groupSize = Math.Clamp(GroupSize(cs, tier), 1, 4);
		double groupMultiplier = 1.0 + GroupStep * (groupSize - 1);
		double raw = CardsPerTurn(actIndex)
			* Tolerance(tier)
			* groupMultiplier
			* AscensionMultiplier(cs.RunState.AscensionLevel);

		int baseline = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
		baseline = Math.Clamp(baseline, MinCounter, MaxCounter);
		return baseline + intentOffset.Value;
	}

	/// <summary>
	/// 意图修正量。基准与意图种类无关，这里只修补两种会滚雪球的意图：
	/// - 攻击/斩杀 +1：额外的一次攻击是爆发伤害，最容易直接打死玩家；
	/// - 增益 +2：额外的一次 buff 是永久成长，随战斗回合数复利；
	/// - 其余 0（防御/减益/功能类）。
	/// 多意图取各意图修正的平均（四舍五入）。返回 null = 全部不可计数。
	/// </summary>
	private static int? IntentOffsetFor(MoveState? move)
	{
		// Intents 在 MoveState 构造后正常非空，但分支/未初始化状态可能是空集合；
		// 这里做一次防御，避免 RollMove postfix 抛 NRE 导致计数器整场失效。
		if (move?.Intents == null)
		{
			return null;
		}
		int sum = 0;
		int count = 0;
		foreach (AbstractIntent intent in move.Intents)
		{
			int? offset = OffsetOf(intent.IntentType);
			if (offset == null)
			{
				continue;
			}
			sum += offset.Value;
			count++;
		}
		if (count == 0)
		{
			return null;
		}
		return (int)Math.Round((double)sum / count, MidpointRounding.AwayFromZero);
	}

	/// <summary>IntentType → 修正量；不可计数返回 null（§7.4 不可计数清单）。</summary>
	private static int? OffsetOf(IntentType type)
	{
		switch (type)
		{
			case IntentType.Attack:
			case IntentType.DeathBlow:
				return 1;
			case IntentType.Buff:
				return 2;
			case IntentType.Escape:
			case IntentType.Sleep:
			case IntentType.Stun:
			case IntentType.Hidden:
				return null;
			default:
				return 0;
		}
	}

	/// <summary>
	/// 判据（§7.8）：事件战斗排除；随从按原生 <see cref="Creature.IsSecondaryEnemy"/> 判档；
	/// 其余按遭遇 RoomType 分级（Monster=Normal，Elite=Elite，Boss=Boss）。
	/// </summary>
	public static bool IsCountedCreature(MonsterModel monster, CombatState cs, out MonsterTier tier)
	{
		tier = MonsterTier.Normal;
		Creature? creature = monster.Creature;
		if (creature == null)
		{
			return false;
		}
		// 事件战斗不计数（§7.8）
		if (cs.RunState?.CurrentRoom is not CombatRoom)
		{
			return false;
		}
		// 设置里的“行动计数范围”对原生怪和 mod 怪一视同仁（1=首领，2=首领+精英，3=所有敌人）
		RoomType roomType = cs.Encounter?.RoomType ?? RoomType.Monster;
		int scope = Math.Clamp(MonsterSettings.ActionCountScope, 1, 3);
		if (!ScopeAllows(roomType, scope))
		{
			return false;
		}
		// 召唤判据（§7.8）；是否计数仍由所在房间的范围决定
		if (ActionCountRuntime.IsSummon(creature))
		{
			tier = MonsterTier.Summon;
			return true;
		}
		switch (roomType)
		{
			case RoomType.Boss:
				tier = MonsterTier.Boss;
				return true;
			case RoomType.Elite:
				tier = MonsterTier.Elite;
				return true;
			case RoomType.Monster:
				tier = MonsterTier.Normal;
				return true;
			default:
				return false;
		}
	}

	private static bool ScopeAllows(RoomType roomType, int scope)
	{
		return roomType switch
		{
			RoomType.Boss => scope >= 1,
			RoomType.Elite => scope >= 2,
			RoomType.Monster => scope >= 3,
			_ => false,
		};
	}
}
