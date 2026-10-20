using System;
using System.Collections.Generic;
using System.Linq;
using CznMonster.Content;
using CznMonster.Content.Encounters;
using CznMonster.Content.Monsters;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Registration;

/// <summary>
/// P3 首领位置补丁（设计文档 §6.2）：
/// 在 <see cref="RunManager.GenerateRooms"/> postfix 里按设置替换各章首领。
/// 本次运行已出现的 CZN 首领记入 <see cref="UsedBossIds"/>，同一次运行不重复（R7）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(RunManager), "GenerateRooms")]
internal static class BossPlacementPatch
{
	// 本次运行已使用的 CZN 首领 id（用于随机模式去重）
	internal static readonly HashSet<string> UsedBossIds = new HashSet<string>(StringComparer.Ordinal);

	// 记录本次运行的状态对象：新开一局/读档时重建去重集合（§6.2 伪代码）。
	// 只比 seed 不够：极小概率新局同 seed 会把上一局的 UsedBossIds 带进来。
	private static ulong _usedBossSeed;

	private static RunState? _lastState;

	// SetUpNewSingleplayer / SetUpNewMultiplayer 的前缀补丁捕获 RunState（公开 API），
	// 取不到时回退 DebugOnlyGetState()
	private static RunState? _capturedState;

	internal static void CaptureState(RunState state)
	{
		_capturedState = state;
	}

	private static RunState? CurrentState()
	{
		return _capturedState ?? RunManager.Instance.DebugOnlyGetState();
	}

	private static void Postfix()
	{
		try
		{
			if (!MonsterSettings.CznMonstersEnabled)
			{
				return;
			}
			RunState? state = CurrentState();
			if (state == null)
			{
				return;
			}
			EnsureInitialized(state);

			// 指定模式优先；随机模式；两个都关 = 不替换，保留原生首领。
			if (MonsterSettings.BossPosSpecified)
			{
				ApplySpecified(state);
			}
			else if (MonsterSettings.BossPosRandom)
			{
				ApplyRandom(state);
			}
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 首领位置应用失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>
	/// 新局/读档后重建去重集合。读档时 Acts 里可能已经有 CZN 首领，
	/// 只清空会导致同一只再次被选中或重复替换。
	/// </summary>
	private static void EnsureInitialized(RunState state)
	{
		if (ReferenceEquals(_lastState, state))
		{
			return;
		}
		_lastState = state;
		_usedBossSeed = state.Rng.Seed;
		UsedBossIds.Clear();
		foreach (ActModel act in state.Acts)
		{
			EncounterModel? boss = act.BossEncounter;
			if (boss != null && MonsterContent.IsCznBoss(boss.Id.Entry))
			{
				UsedBossIds.Add(boss.Id.Entry);
			}
		}
	}

	/// <summary>指定模式：把选中的 CZN 首领强制放进所选章节。</summary>
	private static void ApplySpecified(RunState state)
	{
		EncounterModel? chosen = MonsterContent.ChosenBossEncounter;
		if (chosen == null)
		{
			return;
		}
		int chapter = Math.Clamp(MonsterSettings.BossChapter, 1, 3);
		ActModel? act = state.Acts.FirstOrDefault(a => a.Index == chapter - 1);
		if (act == null)
		{
			MainFile.Log.Warn($"CznMonster 指定章节不存在：{chapter}（Acts={state.Acts.Count}）");
			return;
		}
		ApplyBoss(act, chosen);
	}

	/// <summary>
	/// 随机模式：每只未使用的 CZN 首领随机放进一个尚未被 CZN 占用的章节；
	/// CZN 首领少于 3 只时，剩余章节保留原生首领（D4）。
	/// </summary>
	private static void ApplyRandom(RunState state)
	{
		List<EncounterModel> pool = new List<EncounterModel>();
		foreach (EncounterModel boss in MonsterContent.BossEncounters)
		{
			if (!UsedBossIds.Contains(boss.Id.Entry))
			{
				pool.Add(boss);
			}
		}
		if (pool.Count == 0)
		{
			return;
		}

		List<ActModel> availableActs = new List<ActModel>();
		foreach (ActModel act in state.Acts)
		{
			EncounterModel? current = act.BossEncounter;
			if (current == null || !MonsterContent.IsCznBoss(current.Id.Entry))
			{
				availableActs.Add(act);
			}
		}

		while (pool.Count > 0 && availableActs.Count > 0)
		{
			EncounterModel? chosen = state.Rng.UpFront.NextItem(pool);
			ActModel? act = state.Rng.UpFront.NextItem(availableActs);
			if (chosen == null || act == null)
			{
				break;
			}
			ApplyBoss(act, chosen);
			pool.Remove(chosen);
			availableActs.Remove(act);
		}
	}

	private static void ApplyBoss(ActModel act, EncounterModel chosen)
	{
		if (act.BossEncounter?.Id.Entry == chosen.Id.Entry)
		{
			return;
		}

		// 波勒米卡需要知道本局出现的章节，用于 HP/伤害/格挡缩放
		if (chosen is PolemicaBoss)
		{
			PolemicaScaling.ActIndex = act.Index;
		}

		// SetBossEncounter 存的是 canonical；RunManager.CreateRoom 会自己 ToMutable()。
		// 这里再 ToMutable() 会导致 “Mutable model used in incorrect place” 并黑屏。
		act.SetBossEncounter(chosen);
		UsedBossIds.Add(chosen.Id.Entry);
		MainFile.Log.Info($"CznMonster 首领替换：act {act.Index} -> {chosen.Id.Entry}");
	}
}

[HarmonyLib.HarmonyPatch(typeof(RunManager), "SetUpNewSingleplayer")]
internal static class RunSetupSingleplayerPatch
{
	private static void Prefix(RunState state)
	{
		BossPlacementPatch.CaptureState(state);
	}
}

[HarmonyLib.HarmonyPatch(typeof(RunManager), "SetUpNewMultiplayer")]
internal static class RunSetupMultiplayerPatch
{
	private static void Prefix(RunState state, StartRunLobby lobby, bool shouldSave, DateTimeOffset? dailyTime)
	{
		BossPlacementPatch.CaptureState(state);
	}
}
