using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CznCore.Presentation;

internal static class RunStartPreloadHarmonyPatches
{
	[HarmonyPatch(typeof(NCharacterSelectScreen), "BeginRun")]
	private static class CharacterSelectRunStartPreloadPatch
	{
		private static bool Prefix(NCharacterSelectScreen __instance, string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers)
		{
			if (_runStartPreloadBypass || !ShouldInterceptRunStartPreload(__instance))
			{
				return true;
			}
			if (_runStartPreloadActive)
			{
				return false;
			}
			TaskHelper.RunSafely(RunStartPreloadThenBegin(__instance, seed, acts, modifiers));
			return false;
		}
	}

	[HarmonyPatch(typeof(NMainMenu), "OnContinueButtonPressedAsync")]
	private static class MainMenuContinueRunPreloadPatch
	{
		private static bool Prefix(NMainMenu __instance, ref Task __result)
		{
			if (_continueRunPreloadBypass || !ShouldInterceptContinueRunPreload(__instance))
			{
				return true;
			}
			if (_continueRunPreloadActive)
			{
				__result = Task.CompletedTask;
				return false;
			}
			__result = ContinueRunPreloadThenContinue(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(NCharacterSelectScreen), "OnEmbarkPressed")]
	private static class CharacterSelectMultiplayerReadyPreloadPatch
	{
		private static bool Prefix(NCharacterSelectScreen __instance)
		{
			if (_multiplayerReadyPreloadBypass || !ShouldInterceptMultiplayerReadyPreload(__instance))
			{
				return true;
			}
			if (_multiplayerReadyPreloadActive)
			{
				return false;
			}
			TaskHelper.RunSafely(MultiplayerReadyPreloadThenEmbark(__instance));
			return false;
		}
	}

	private static readonly FieldInfo? ContinueRunSaveResultField = AccessTools.Field(typeof(NMainMenu), "_readRunSaveResult");

	private static readonly MethodInfo? CharacterSelectEmbarkPressedMethod = AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed");

	private static readonly MethodInfo? ContinueButtonPressedAsyncMethod = AccessTools.Method(typeof(NMainMenu), "OnContinueButtonPressedAsync");

	private const string MultiplayerReadyPreloadCompletedMetaPrefix = "czn_run_start_preload_multiplayer_ready_completed_";

	private static bool _runStartPreloadBypass;

	private static bool _runStartPreloadActive;

	private static bool _continueRunPreloadBypass;

	private static bool _continueRunPreloadActive;

	private static bool _multiplayerReadyPreloadBypass;

	private static bool _multiplayerReadyPreloadActive;

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[3]
	{
		typeof(CharacterSelectRunStartPreloadPatch),
		typeof(MainMenuContinueRunPreloadPatch),
		typeof(CharacterSelectMultiplayerReadyPreloadPatch)
	});

	private static bool ShouldInterceptRunStartPreload(NCharacterSelectScreen? screen)
	{
		try
		{
			RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
			if (current == null || screen == null || !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
			{
				return false;
			}
			if (screen.Lobby.NetService.Type != NetGameType.Singleplayer)
			{
				return false;
			}
			CharacterModel character = screen.Lobby.LocalPlayer.character;
			return current.AppliesTo(character);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Run-start preload guard failed: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static bool ShouldInterceptMultiplayerReadyPreload(NCharacterSelectScreen? screen)
	{
		try
		{
			RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
			if (current == null || screen == null || !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
			{
				return false;
			}
			if (!screen.Lobby.NetService.Type.IsMultiplayer())
			{
				return false;
			}
			CharacterModel character = screen.Lobby.LocalPlayer.character;
			return current.AppliesTo(character) && !HasCompletedMultiplayerReadyPreload(screen, current, character);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Multiplayer-ready preload guard failed: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static bool ShouldInterceptContinueRunPreload(NMainMenu? menu)
	{
		try
		{
			RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
			if (current == null || menu == null || !GodotObject.IsInstanceValid(menu) || !menu.IsInsideTree())
			{
				return false;
			}
			ReadSaveResult<SerializableRun> obj = ContinueRunSaveResultField?.GetValue(menu) as ReadSaveResult<SerializableRun>;
			SerializableRun serializableRun = obj?.SaveData;
			if (obj == null || !obj.Success || serializableRun?.Players == null || serializableRun.Players.Count == 0)
			{
				return false;
			}
			// 联机存档里 Players[0] 是「存档里的第一个玩家」，不是本机玩家。
			// 必须按本机 NetId 找自己，否则会出现「别人是海德玛丽 → 我这边去预加载」
			// 或「我是海德玛丽 → 反而不预加载」。
			ulong? localNetId = LocalContext.NetId;
			SerializablePlayer? me = null;
			if (localNetId.HasValue)
			{
				foreach (SerializablePlayer candidate in serializableRun.Players)
				{
					if (candidate.NetId == localNetId.Value)
					{
						me = candidate;
						break;
					}
				}
			}
			me ??= serializableRun.Players[0];
			if (me.CharacterId == null)
			{
				return false;
			}
			CharacterModel byId = ModelDb.GetById<CharacterModel>(me.CharacterId);
			return current.AppliesTo(byId);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Continue-run preload guard failed: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static async Task MultiplayerReadyPreloadThenEmbark(NCharacterSelectScreen screen)
	{
		RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
		CharacterModel character = TryGetLocalCharacter(screen);
		string completedMetaKey = ((current == null) ? null : GetMultiplayerReadyPreloadMetaKey(current));
		string completedToken = ((current == null) ? null : BuildMultiplayerReadyPreloadToken(current, character));
		_multiplayerReadyPreloadActive = true;
		try
		{
			if (current != null)
			{
				await current.RunPreload(screen, "Multiplayer-ready");
			}
		}
		finally
		{
			_multiplayerReadyPreloadActive = false;
		}
		if (!GodotObject.IsInstanceValid(screen))
		{
			return;
		}
		if (!string.IsNullOrEmpty(completedMetaKey) && completedToken != null)
		{
			screen.SetMeta(completedMetaKey, Variant.From(in completedToken));
		}
		try
		{
			_multiplayerReadyPreloadBypass = true;
			if (CharacterSelectEmbarkPressedMethod != null)
			{
				CharacterSelectEmbarkPressedMethod.Invoke(screen, new object[1]);
			}
			else
			{
				SkinLog.Warn("Multiplayer-ready native handler missing after preload; ready skipped.");
			}
		}
		finally
		{
			_multiplayerReadyPreloadBypass = false;
		}
	}

	private static bool HasCompletedMultiplayerReadyPreload(NCharacterSelectScreen screen, RunStartPreloadRuntime runtime, CharacterModel? character)
	{
		string multiplayerReadyPreloadMetaKey = GetMultiplayerReadyPreloadMetaKey(runtime);
		if (screen.HasMeta(multiplayerReadyPreloadMetaKey))
		{
			return screen.GetMeta(multiplayerReadyPreloadMetaKey).AsString() == BuildMultiplayerReadyPreloadToken(runtime, character);
		}
		return false;
	}

	private static string GetMultiplayerReadyPreloadMetaKey(RunStartPreloadRuntime runtime)
	{
		return "czn_run_start_preload_multiplayer_ready_completed_" + runtime.ProfileId;
	}

	private static string BuildMultiplayerReadyPreloadToken(RunStartPreloadRuntime runtime, CharacterModel? character)
	{
		return runtime.ProfileId + ":" + (character?.Id.Entry ?? string.Empty);
	}

	private static CharacterModel? TryGetLocalCharacter(NCharacterSelectScreen? screen)
	{
		try
		{
			return screen?.Lobby.LocalPlayer.character;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Multiplayer-ready preload character lookup failed: " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private static async Task RunStartPreloadThenBegin(NCharacterSelectScreen screen, string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers)
	{
		_runStartPreloadActive = true;
		try
		{
			RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
			if (current != null)
			{
				await current.RunPreload(screen, "Run-start");
			}
		}
		finally
		{
			_runStartPreloadActive = false;
		}
		if (!GodotObject.IsInstanceValid(screen))
		{
			return;
		}
		try
		{
			_runStartPreloadBypass = true;
			screen.BeginRun(seed, acts, modifiers);
		}
		finally
		{
			_runStartPreloadBypass = false;
		}
	}

	private static async Task ContinueRunPreloadThenContinue(NMainMenu menu)
	{
		_continueRunPreloadActive = true;
		try
		{
			RunStartPreloadRuntime current = RunStartPreloadRuntime.Current;
			if (current != null)
			{
				await current.RunPreload(menu, "Continue-run");
			}
		}
		finally
		{
			_continueRunPreloadActive = false;
		}
		if (GodotObject.IsInstanceValid(menu))
		{
			Task task = null;
			try
			{
				_continueRunPreloadBypass = true;
				task = ContinueButtonPressedAsyncMethod?.Invoke(menu, Array.Empty<object>()) as Task;
			}
			finally
			{
				_continueRunPreloadBypass = false;
			}
			if (task != null)
			{
				await task;
			}
			else
			{
				SkinLog.Warn("Continue-run native handler missing after preload; load run skipped.");
			}
		}
	}
}
