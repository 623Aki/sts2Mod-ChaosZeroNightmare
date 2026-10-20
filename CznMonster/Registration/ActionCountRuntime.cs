using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznMonster.Registration;

/// <summary>
/// 行动计数运行时（设计文档 §7.5 / §7.7）：
/// 战斗内单一数据源。计数器字典 + 待打断集合 + 初始敌人快照（召唤判据）。
/// Power 显示与计数器严格同步：ApplyInternal / SetAmount / RemoveInternal。
/// </summary>
internal static class ActionCountRuntime
{
	private static readonly Dictionary<Creature, int> Counters = new Dictionary<Creature, int>();

	private static readonly HashSet<Creature> InterruptPending = new HashSet<Creature>();

	private static bool _draining;

	/// <summary>战斗搭建完成：重置并快照初始敌人（此时全部生物已加入，CombatManager.cs:463-467）。</summary>
	public static void OnCombatSetUp(CombatState state)
	{
		ResetCombat();
	}

	/// <summary>战斗结束：清空全部运行时状态。</summary>
	public static void OnCombatEnded()
	{
		ResetCombat();
	}

	private static void ResetCombat()
	{
		Counters.Clear();
		InterruptPending.Clear();
		_draining = false;
	}

	/// <summary>给生物设置计数器并同步显示；value ≤ 0 视为清除。</summary>
	public static void Assign(Creature creature, int value)
	{
		if (value <= 0)
		{
			Clear(creature);
			return;
		}
		Counters[creature] = value;
		SyncPower(creature, value);
	}

	/// <summary>清除生物的计数器、待打断标记与显示。</summary>
	public static void Clear(Creature creature)
	{
		Counters.Remove(creature);
		InterruptPending.Remove(creature);
		RemovePower(creature);
	}

	/// <summary>该生物是否在计数中。</summary>
	public static bool IsCounted(Creature creature)
	{
		return Counters.ContainsKey(creature);
	}

	/// <summary>
	/// 随从判据：直接采用游戏原生的“次要敌人”语义
	/// （<see cref="Creature.IsSecondaryEnemy"/>，即挂了 <see cref="MinionPower"/> 的单位，
	/// 主敌全灭时会自动死亡）。
	/// 不能用“是否在初始敌人快照里”判断：分裂史莱姆、复活/换阶段的普通怪也会出现在快照之后，
	/// 但它们不是随从；开局自带的真正随从（GasBomb、Ovicopter 的蛋等）反而在快照里。
	/// </summary>
	public static bool IsSummon(Creature creature)
	{
		return creature.IsSecondaryEnemy;
	}

	/// <summary>全部在计数生物计数 -1（§7.6 减询时机在出牌 postfix），返回本次归零的生物。</summary>
	public static List<Creature> DecrementAll()
	{
		List<Creature> zeroed = new List<Creature>();
		foreach (Creature creature in Counters.Keys.ToList())
		{
			// 死亡、已被移出战斗状态、或 Monster 已清空的单位不再递减/刷新 Power。
			// 异蛙寄生虫死亡生成扭动虫、瀑布巨兽换阶段等都会走到这里。
			if (!creature.IsAlive || creature.CombatState == null)
			{
				Clear(creature);
				continue;
			}
			int value = Counters[creature] - 1;
			Counters[creature] = value;
			SyncPower(creature, value);
			if (value <= 0)
			{
				zeroed.Add(creature);
			}
		}
		return zeroed;
	}

	/// <summary>标记本次需要打断行动回合的生物。</summary>
	public static void MarkInterruptQueued(IEnumerable<Creature> creatures)
	{
		foreach (Creature creature in creatures)
		{
			if (Counters.ContainsKey(creature))
			{
				InterruptPending.Add(creature);
			}
		}
	}

	/// <summary>该生物是否有待执行的打断行动。</summary>
	public static bool HasPendingInterrupt(Creature creature)
	{
		return InterruptPending.Contains(creature);
	}

	/// <summary>取走「已标记且计数 ≤ 0」的生物，并循环收割直到没有剩余。</summary>
	public static List<Creature> TakeZeroedQueued()
	{
		List<Creature> result = new List<Creature>();
		foreach (Creature creature in InterruptPending.ToList())
		{
			if (Counters.TryGetValue(creature, out int value) && value <= 0)
			{
				result.Add(creature);
			}
		}
		return result;
	}

	/// <summary>
	/// 打断执行队列（设计文档 §7.5）：意图动画 → 行动 → 重摇下一步。
	/// 打断期间 <see cref="Creature.TakeTurn"/> 被 P6 挡住，避免与玩家出牌流程并发执行。
	/// </summary>
	public static async Task DrainAsync(CombatState cs)
	{
		if (_draining)
		{
			return;
		}
		_draining = true;
		try
		{
			CombatManager cm = CombatManager.Instance;
			while (true)
			{
				List<Creature> ready = TakeZeroedQueued();
				if (ready.Count == 0)
				{
					break;
				}
				if (!cm.IsInProgress || cm.IsOverOrEnding)
				{
					break;
				}
				foreach (Creature creature in ready)
				{
					if (!IsActionable(creature, cs))
					{
						InterruptPending.Remove(creature);
						continue;
					}
					if (!cm.IsInProgress || cm.IsOverOrEnding)
					{
						break;
					}
					// 先播意图动画再执行行动，和原生行动回合观感一致（§7.7）
					Task? intentTask = NCombatRoom.Instance?.GetCreatureNode(creature)?.PerformIntent();
					if (intentTask != null)
					{
						await intentTask;
					}
					MonsterModel? monster = creature.Monster;
					if (monster != null)
					{
						await monster.PerformMove();
					}

					// PerformMove 可能让怪物死亡/被移除/换阶段，或直接结束战斗。
					// 例如异蛙寄生虫死亡生成扭动虫、瀑布巨兽蒸汽喷发：
					// 此时再 PrepareForNextTurn 会进入 NCreature.RefreshIntents，
					// Entity.CombatState 已为 null，抛 NullReferenceException。
					InterruptPending.Remove(creature);
					if (!cm.IsInProgress || cm.IsOverOrEnding)
					{
						break;
					}
					if (!IsActionable(creature, cs))
					{
						continue;
					}
					// 重摇下一步：RollMove 的 postfix（P4）会顺势分配新计数器
					try
					{
						creature.PrepareForNextTurn(cs.PlayerCreatures);
					}
					catch (Exception ex)
					{
						MainFile.Log.Warn($"CznMonster 行动计数重摇失败：{ex.GetType().Name}: {ex.Message}");
					}
				}
			}
		}
		finally
		{
			_draining = false;
		}
	}

	/// <summary>
	/// 行动后是否还能安全地重摇/刷新意图。
	/// 死亡、被移出战斗状态、Monster 被清空时都不行；
	/// 否则 Creature.PrepareForNextTurn → NCreature.RefreshIntents
	/// 会访问 Entity.CombatState 并抛 NRE。
	/// </summary>
	private static bool IsActionable(Creature creature, CombatState cs)
	{
		return creature.IsAlive
			&& creature.Monster != null
			&& creature.CombatState != null
			&& cs.ContainsCreature(creature);
	}

	private static void SyncPower(Creature creature, int value)
	{
		PowerModel? power = creature.GetPower<CznActionCountPower>();
		if (power == null)
		{
			if (value <= 0)
			{
				return;
			}
			// 注意：不能写 ToMutable(value)。PowerModel.ToMutable 在设置 Amount 时
			// Owner 还是 null，会触发 NullReferenceException，导致计数器从未挂上。
			// 先 ToMutable() 建 0 层实例，再由 ApplyInternal 设置 Owner 和层数。
			ModelDb.Power<CznActionCountPower>().ToMutable().ApplyInternal(creature, value);
			return;
		}
		power.SetAmount(value);
	}

	private static void RemovePower(Creature creature)
	{
		PowerModel? power = creature.GetPower<CznActionCountPower>();
		power?.RemoveInternal();
	}
}
