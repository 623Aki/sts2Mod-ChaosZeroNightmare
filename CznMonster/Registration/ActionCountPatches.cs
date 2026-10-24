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
			if (!MonsterSettings.EffectiveActionCountEnabled)
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
/// P5 玩家出牌后统一减询,并在同一动作执行窗内同步收割打断。
/// <para>
/// 不能直接在 <see cref="CardModel.OnPlayWrapper"/> 的 postfix 里减：那是 async 方法，
/// postfix 在第一次 await 就返回，牌其实还没结算完。这里把 <c>__result</c> 换成组合 Task：
/// 先等整张牌结算完成（包括消耗/丢弃/奇巧等后续触发），再在 <strong>同一个 await 链内</strong>
/// 完成减询与打断，然后原动作的 <see cref="PlayCardAction.ExecuteAction"/> 才算执行完。
/// </para>
/// <para>
/// 多人一致性关键：旧实现用 <c>TaskHelper.RunSafely</c> fire-and-forget 启动打断，
/// 打断的实际执行时机（意图动画 → 行动 → 重摇）会漂到回合结束校验点
/// （如 after player turn phase two end）之后，且两端完成早晚取决于帧率/网络 ——
/// 一端已重摇（计数刷新 + 多消耗一次 MonsterAi RNG），另一端停在计数 0，
/// checksum 直接劈叉。改为组合 Task 后，打断被折进出牌动作自己的执行窗口，
/// 所有端都在同一个动作同步点完成，之前的下一个队列动作（含回合结束握手）
/// 必然先等到它结束。
/// </para>
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(CardModel), "OnPlayWrapper")]
internal static class ActionCountCardPlayPatch
{
	private static void Postfix(CardModel __instance, bool isAutoPlay, ref Task __result)
	{
		// 自动打出的牌不减询（§7.6），也不需要等待
		if (isAutoPlay || __result == null)
		{
			return;
		}

		Task playTask = __result;
		__result = PlayThenSettleAsync(playTask, __instance);
	}

	private static async Task PlayThenSettleAsync(Task playTask, CardModel card)
	{
		try
		{
			await playTask;

			// 这张牌已整体结算完（OnPlay / 附魔 / 苦难 / AfterCardPlayed /
			// 结果堆移动（消耗/丢弃）/ 空手检查都在 playTask 里）。
			// 在它所属 GameAction 的执行窗内继续收割打断，保持多人同步（见类注释）。
			await ActionCountRuntime.AfterCardPlaySettledAsync(card);
		}
		catch (System.Exception ex)
		{
			// 与旧实现一致：出牌/结算本身的异常在此吞掉并记日志，
			// 不让打断逻辑把 PlayCardAction 本身打成 faulted 而改变动作执行语义。
			MainFile.Log.Warn($"CznMonster 行动计数减询失败：{ex.GetType().Name}: {ex.Message}");
		}
	}
}

/// <summary>
/// P6 安全网：若打断因异常中断等原因遗留待执行标记（理论上 P5 已在动作窗内同步收割完），
/// 自然行动回合（TakeTurn prefix）被挡住，避免打断与怪物自然回合并发执行（§7.7）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(Creature), "TakeTurn")]
internal static class ActionCountTakeTurnPatch
{
	private static bool Prefix(Creature __instance)
	{
		if (!MonsterSettings.EffectiveActionCountEnabled)
		{
			return true;
		}
		try
		{
			// 只在「收割正在进行」时挡住自然行动。
			// 不能只看 HasPendingInterrupt：标记如果在收割提前退出时残留，
			// 会让这只怪下一回合直接不行动（而且两端可能只残留一端）。
			return !(ActionCountRuntime.IsDraining && ActionCountRuntime.HasPendingInterrupt(__instance));
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
	private static bool _subscribed;

	internal static void Initialize()
	{
		// 幂等：热重载 / 二次 Initialize 时不能重复订阅（否则计数器被重置两次、预热跑两遍）
		if (_subscribed)
		{
			return;
		}
		_subscribed = true;
		CombatManager.Instance.CombatSetUp += OnCombatSetUp;
		CombatManager.Instance.CombatEnded += OnCombatEnded;
	}

	private static void OnCombatSetUp(CombatState state)
	{
		// 无条件重置：计数器是静态表，不重置会把上一场战斗的状态带进新战斗
		// （上一场开着计数、这一场关掉时尤其明显）。
		ActionCountRuntime.OnCombatSetUp(state);
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
	//   C(act)    = 3.0 + 0.3 × actIndex            第 1/2/3 章 = 3.0 / 3.3 / 3.6
	//   Tol(tier) = 1 / A(tier)                     首领 2.00 / 精英 1.67 / 普通 1.33 / 随从 0.85
	//   GroupMul  = 1 + 0.4 × (min(n,4) - 1)        n = 同房间同档存活主敌数（随从恒 1）
	//   AscMul    = 进阶修正
	//   N0 = round(C × Tol × GroupMul × AscMul)
	//   N  = clamp(N0 + IntentOff, 2, 14) × 玩家数
	// 用 decimal 而不是 double：这条公式的结果是「阈值整数」，
	// 落在 x.5 边界时二进制浮点的表示差异会被放大成 ±1 次怪物行动。
	// decimal 是精确十进制运算，两端必然得到同一个整数。
	private const decimal BaseCardsPerTurn = 3.0m;

	/// <summary>
	/// 每章递增的「典型出牌数」：0.3/章。
	///
	/// <para><b>为什么从 0.8 降下来</b>：后期的能量增长大多被「均费上升」吃掉
	/// （初始卡组打击/防御 1 费 → 主力牌 1.2~1.5 费），抽牌引擎只解决「手牌够」
	/// 而不解决「能量够」，因此单回合主动出牌张数并不会同比 +25%/章。
	/// 原 0.8/章 让第 3 章基数比第 1 章高 50%，而玩家实际出牌数只涨一成左右，
	/// 触发率被按 1/N 稀释 —— 表现就是「后期几乎不触发」。
	/// 本机制的目标是<b>全程稳定压力</b>，故斜率必须贴近真实张数增长。</para>
	/// </summary>
	private const decimal CardsPerActStep = 0.3m;

	private const decimal GroupStep = 0.4m;

	private const int MinCounter = 2;

	private const int MaxCounter = 14;

	/// <summary>
	/// 玩家一回合的典型<b>主动出牌数</b> C：3 能量 / 5 抽牌 / 均费 1 费 ≈ 3 张。
	///
	/// <para>逐章只 +0.3：能量遗物的增量大多被后期均费上升吃掉，抽牌引擎只解决
	/// 「手牌够」而不解决「能量够」。这是整条公式里唯一靠估算的量，
	/// 实测有偏差时只调 <see cref="BaseCardsPerTurn"/> 与 <see cref="CardsPerActStep"/> 即可。</para>
	/// </summary>
	private static decimal CardsPerTurn(int actIndex)
	{
		return BaseCardsPerTurn + CardsPerActStep * actIndex;
	}

	/// <summary>
	/// 每只怪每回合允许多出的行动次数 A 的倒数。
	/// 首领一次额外行动最贵 → 容忍最高（A=0.50）；随从最便宜 → 容忍最低（A≈1.18）。
	/// </summary>
	private static decimal Tolerance(MonsterTier tier)
	{
		switch (tier)
		{
			case MonsterTier.Boss:
				return 2.00m;
			case MonsterTier.Elite:
				return 1.67m;
			case MonsterTier.Normal:
				return 1.33m;
			default:
				return 0.85m;
		}
	}

	/// <summary>
	/// 进阶修正：按本体实际影响点分档，而不是线性硬套。
	/// A5–A7 诅咒/少药水/少好牌（玩家出牌能力下降）、A8 敌人更肉、A9 更疼、A10 双 Boss。
	/// </summary>
	private static decimal AscensionMultiplier(int ascensionLevel)
	{
		if (ascensionLevel >= 9)
		{
			return 0.85m;
		}
		if (ascensionLevel >= 8)
		{
			return 0.90m;
		}
		if (ascensionLevel >= 5)
		{
			return 0.95m;
		}
		return 1.0m;
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
		decimal groupMultiplier = 1.0m + GroupStep * (groupSize - 1);
		decimal raw = CardsPerTurn(actIndex)
			* Tolerance(tier)
			* groupMultiplier
			* AscensionMultiplier(cs.RunState.AscensionLevel);

		int baseline = (int)Math.Round(raw, MidpointRounding.AwayFromZero);

		// 意图修正先加、再夹取：夹取的语义是「最终阈值上限」。
		// 旧顺序（先夹再加）会让多怪/高基数场的意图修正突破 14，上限形同虚设；
		// 同时 +1/+2 相对小基数档（随从 3 层）会被放大成 +33%~+67%，破坏跨档稳定性。
		int total = baseline + intentOffset.Value;
		return Math.Clamp(total, MinCounter, MaxCounter);
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
		int scope = Math.Clamp(MonsterSettings.EffectiveActionCountScope, 1, 3);
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
