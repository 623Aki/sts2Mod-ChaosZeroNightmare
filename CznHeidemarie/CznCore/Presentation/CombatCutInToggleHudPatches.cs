using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatCutInToggleHudPatches
{
	[HarmonyPatch(typeof(NCombatUi), "Activate")]
	private static class CombatUiActivatePatch
	{
		private static bool Prepare()
		{
			return CombatCutInToggleHudService.IsRegistered;
		}

		private static void Postfix(NCombatUi __instance, CombatState state)
		{
			SkinPatchGuard.Run("Combat cut-in toggle HUD activate", () =>
			{
				CombatCutInToggleHudService.Activate(__instance, state);
			});
		}
	}

	[HarmonyPatch(typeof(NCombatUi), "Deactivate")]
	private static class CombatUiDeactivatePatch
	{
		private static bool Prepare()
		{
			return CombatCutInToggleHudService.IsRegistered;
		}

		private static void Postfix()
		{
			SkinPatchGuard.Run("Combat cut-in toggle HUD deactivate", CombatCutInToggleHudService.Deactivate);
		}
	}

	[HarmonyPatch(typeof(NCombatUi), "Enable")]
	private static class CombatUiEnablePatch
	{
		private static bool Prepare()
		{
			return CombatCutInToggleHudService.IsRegistered;
		}

		private static void Postfix(NCombatUi __instance)
		{
			SkinPatchGuard.Run("Combat cut-in toggle HUD enable", () =>
			{
				CombatCutInToggleHudService.Enable(__instance);
			});
		}
	}

	[HarmonyPatch(typeof(NCombatUi), "Disable")]
	private static class CombatUiDisablePatch
	{
		private static bool Prepare()
		{
			return CombatCutInToggleHudService.IsRegistered;
		}

		private static void Postfix()
		{
			SkinPatchGuard.Run("Combat cut-in toggle HUD disable", CombatCutInToggleHudService.Disable);
		}
	}

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[4]
	{
		typeof(CombatUiActivatePatch),
		typeof(CombatUiDeactivatePatch),
		typeof(CombatUiEnablePatch),
		typeof(CombatUiDisablePatch)
	});
}
