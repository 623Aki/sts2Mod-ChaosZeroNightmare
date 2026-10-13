using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal static class CharacterLifecycleTransport
{
	[HarmonyPatch(typeof(Hook), "AfterDeath")]
	private static class PlayerDeathPatch
	{
		private static void Postfix(IRunState runState, Creature creature, bool wasRemovalPrevented, float deathAnimLength, ref Task __result)
		{
			if (!wasRemovalPrevented)
			{
				Player player = creature.Player;
				if (player != null)
				{
					__result = PublishDeathAfter(__result, runState, player, deathAnimLength);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Hook), "AfterCombatVictory")]
	private static class CombatVictoryPatch
	{
		private static void Postfix(ICombatState? combatState, ref Task __result)
		{
			if (combatState != null)
			{
				__result = PublishVictoryAfter(__result, combatState);
			}
		}
	}

	[HarmonyPatch(typeof(NGameOverScreen), "AfterOverlayOpened")]
	private static class GameOverOpenedPatch
	{
		private static void Postfix()
		{
			RunState runState = RunManager.Instance.DebugOnlyGetState();
			if (runState != null)
			{
				Player me = LocalContext.GetMe(runState);
				if (me != null)
				{
					PublishRunFinished(runState, me);
				}
			}
		}
	}

	internal static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[3]
	{
		typeof(PlayerDeathPatch),
		typeof(CombatVictoryPatch),
		typeof(GameOverOpenedPatch)
	});

	private static async Task PublishDeathAfter(Task nativeHook, IRunState runState, Player player, float deathAnimationLength)
	{
		await nativeHook;
		SkinEventHub.Publish(new PlayerDeathResolvedEvent(player, runState.IsGameOver, deathAnimationLength));
	}

	private static async Task PublishVictoryAfter(Task nativeHook, ICombatState combatState)
	{
		await nativeHook;
		foreach (Player player in combatState.Players)
		{
			SkinEventHub.Publish(new PlayerCombatVictoryEvent(player));
		}
	}

	private static void PublishRunFinished(IRunState runState, Player player)
	{
		if (runState.IsGameOver)
		{
			SkinEventHub.Publish(new RunFinishedPresentationEvent(player));
		}
	}
}
