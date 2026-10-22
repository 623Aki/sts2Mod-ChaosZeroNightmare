using System;
using System.Collections.Generic;
using System.Linq;
using CznMonster.Content;
using CznMonster.Content.Encounters;
using CznMonster.Content.Monsters;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Registration;

/// <summary>
/// P3 首领位置补丁（设计文档 §6.2 / §6.3）：
/// <para><b>随机模式</b>不再改写首领。首领遭遇由 <see cref="MonsterPoolPatch"/> 并入原生
/// <c>AllBossEncounters</c>，出现与否、出现在哪一章全部交给原生 <c>GenerateRooms</c> 抽取。</para>
/// <para><b>指定模式</b>在 <see cref="RunManager.GenerateRooms"/> postfix 里把所选章节的首领
/// 替换成「指定首领」或未使用的 CZN 首领中的随机一只；其余章节保持原生首领。</para>
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(RunManager), "GenerateRooms")]
internal static class BossPlacementPatch
{
	// 记录本次运行的状态对象（仅用于取 RunState；SetUpNew* 前缀补丁捕获，取不到回退 DebugOnlyGetState）
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

			// 随机模式：首领进入原生池后由原生抽取，这里不做任何改写（避免"必出"）
			if (!MonsterSettings.BossPosSpecified)
			{
				return;
			}

			RunState? state = CurrentState();
			if (state == null)
			{
				return;
			}
			ApplySpecified(state);
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 首领位置应用失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>
	/// 指定模式：把「指定首领」（未指定则随机取一只本局未出现的 CZN 首领）放进所选章节。
	/// </summary>
	private static void ApplySpecified(RunState state)
	{
		int chapter = Math.Clamp(MonsterSettings.BossChapter, 1, 3);
		ActModel? act = state.Acts.FirstOrDefault(a => a.Index == chapter - 1);
		if (act == null)
		{
			MainFile.Log.Warn($"CznMonster 指定章节不存在：{chapter}（Acts={state.Acts.Count}）");
			return;
		}

		// 设置里明确选了某只首领就照办；选「随机」时在本局未出现过的 CZN 首领里抽
		EncounterModel? chosen = MonsterContent.GetSelectedBoss(MonsterSettings.BossSelect);
		if (chosen == null)
		{
			List<EncounterModel> pool = new List<EncounterModel>();
			foreach (EncounterModel boss in MonsterContent.BossEncounters)
			{
				if (!IsUsedInOtherActs(state, act, boss))
				{
					pool.Add(boss);
				}
			}
			if (pool.Count == 0)
			{
				return;
			}
			chosen = state.Rng.UpFront.NextItem(pool);
		}
		else if (IsUsedInOtherActs(state, act, chosen))
		{
			// 指定首领已被本局其他章节占用（例如读档后重复应用）：不覆盖，交给原生
			MainFile.Log.Info($"CznMonster 指定首领 {chosen.Id.Entry} 本局已出现，跳过替换");
			return;
		}

		if (chosen == null)
		{
			return;
		}
		MainFile.Log.Info($"CznMonster 指定首领：{chosen.Id.Entry} → 第 {chapter} 章");
		ApplyBoss(act, chosen);
	}

	/// <summary>该首领是否已出现在本局除 <paramref name="except"/> 之外的章节（含双首领）。</summary>
	private static bool IsUsedInOtherActs(RunState state, ActModel except, EncounterModel boss)
	{
		foreach (ActModel act in state.Acts)
		{
			if (ReferenceEquals(act, except))
			{
				continue;
			}
			if (act.BossEncounter?.Id.Entry == boss.Id.Entry || act.SecondBossEncounter?.Id.Entry == boss.Id.Entry)
			{
				return true;
			}
		}
		return false;
	}

	private static void ApplyBoss(ActModel act, EncounterModel chosen)
	{
		if (act.BossEncounter?.Id.Entry == chosen.Id.Entry)
		{
			return;
		}

		// 波勒米卡需要知道本局出现的章节，用于 HP/伤害/格挡缩放
		// （各遭遇的 GenerateMonsters 也会按 CurrentActIndex 兜底写入）
		if (chosen is PolemicaBoss)
		{
			PolemicaScaling.ActIndex = act.Index;
		}

		// SetBossEncounter 存的是 canonical；RunManager.CreateRoom 会自己 ToMutable()。
		// 这里再 ToMutable() 会导致 “Mutable model used in incorrect place” 并黑屏。
		act.SetBossEncounter(chosen);
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
