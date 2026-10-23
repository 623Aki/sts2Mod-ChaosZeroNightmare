using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Powers;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
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

	/// <summary>
	/// 收割是否正在进行。
	/// P6（<c>Creature.TakeTurn</c> 拦截）必须同时满足「正在收割」才挡，
	/// 否则一个残留标记会让这只怪整回合不行动。
	/// </summary>
	public static bool IsDraining => _draining;

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
	/// 出牌结算完成后的统一减询与打断收割（§7.6）。
	/// <para>
	/// 多人一致性关键：P5 已把本调用折进该张牌 <see cref="CardModel.OnPlayWrapper"/>
	/// 的组合 Task 里，因此本方法必然在那张牌的 <see cref="GameAction"/> 执行窗内被
	/// <strong>await 到结束</strong> —— 减询 / 打断行动 / 重摇的状态变化，对两端来说
	/// 都发生在同一个动作同步点，回合结束校验点（after player turn phase two end 等）
	/// 之前状态一定收敛；不会再出现“一端已重摇（计数刷新 + 多消耗一次 MonsterAi RNG）、
	/// 另一端停在计数 0”的 checksum 劈叉。
	/// </para>
	/// </summary>
	public static async Task AfterCardPlaySettledAsync(CardModel card)
	{
		CombatManager cm = CombatManager.Instance;
		if (!cm.IsInProgress || cm.IsOverOrEnding)
		{
			return;
		}
		if (!MonsterSettings.EffectiveActionCountEnabled)
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
		await DecrementAndDrainAsync(cs);
	}

	/// <summary>减询一次并在有生物归零时执行打断收割（§7.6）。</summary>
	public static async Task DecrementAndDrainAsync(CombatState cs)
	{
		// 如果牌结算过程中怪已经死了，DecrementAll 会清掉尸体，不会误触发打断。
		List<Creature> zeroed = DecrementAll();
		if (zeroed.Count == 0)
		{
			return;
		}
		MarkInterruptQueued(zeroed);
		await DrainAsync(cs);
	}

	/// <summary>
	/// 打断执行队列（设计文档 §7.5）：意图动画 → 行动 → 重摇下一步。
	/// 只在结算动作窗内被 await 调用（P5 / <see cref="AfterCardPlaySettledAsync"/>）；
	/// <see cref="Creature.TakeTurn"/> 仍保留 P6 安全网拦截，防止遗留标记时与
	/// 怪物自然回合并发执行。
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
						MainFile.Log.Info($"CznMonster 行动计数打断：{creature.LogName} 执行 {monster.NextMove.Id}");
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
						// 重摇失败会让本端的 MonsterAi RNG 少推进一次，理论上与对端错位。
						// 这里已是最内层兜底（IsActionable 挡掉了绝大多数非法状态），
						// 只能保证不因一次异常让这只怪后续回合被整回合跳过。
						InterruptPending.Remove(creature);
						MainFile.Log.Warn($"CznMonster 行动计数重摇失败：{ex.GetType().Name}: {ex.Message}");
					}
				}
			}
		}
		finally
		{
			_draining = false;
			// 无论怎么退出（战斗结束 / 提前 break / 异常），都不能把「待打断」标记留到下一回合：
			// 残留标记会让这只怪在下一回合被 P6 整回合跳过。
			InterruptPending.Clear();
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
