using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CznCore.Presentation;

internal static class SkinCharacterSelectPatches
{
	[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTitle")]
	private static class CharacterSelectTitlePatch
	{
		private static void Postfix(CharacterModel __instance, ref string __result)
		{
			CharacterSkinProfile registeredProfile = CharacterSkinModBootstrap.RegisteredProfile;
			if ((object)registeredProfile != null)
			{
				CharacterSkinModBootstrap.ApplyCharacterSelectTitle(registeredProfile, __instance, ref __result);
			}
		}
	}

	[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectDesc")]
	private static class CharacterSelectDescriptionPatch
	{
		private static void Postfix(CharacterModel __instance, ref string __result)
		{
			CharacterSkinProfile registeredProfile = CharacterSkinModBootstrap.RegisteredProfile;
			if ((object)registeredProfile != null)
			{
				CharacterSkinModBootstrap.ApplyCharacterSelectDescription(registeredProfile, __instance, ref __result);
			}
		}
	}

	[HarmonyPatch(typeof(CharacterModel), "get_Title")]
	private static class CharacterTitlePatch
	{
		private static void Postfix(CharacterModel __instance, ref LocString __result)
		{
			CharacterSkinProfile registeredProfile = CharacterSkinModBootstrap.RegisteredProfile;
			if ((object)registeredProfile != null)
			{
				CharacterSkinModBootstrap.ApplyCharacterTitle(registeredProfile, __instance, ref __result);
			}
		}
	}

	[HarmonyPatch(typeof(CharacterModel), "get_TitleObject")]
	private static class CharacterTitleObjectPatch
	{
		private static void Postfix(CharacterModel __instance, ref LocString __result)
		{
			CharacterSkinProfile registeredProfile = CharacterSkinModBootstrap.RegisteredProfile;
			if ((object)registeredProfile != null)
			{
				CharacterSkinModBootstrap.ApplyCharacterTitleObject(registeredProfile, __instance, ref __result);
			}
		}
	}

	[HarmonyPatch(typeof(CharacterModel), "get_MapMarker")]
	private static class CharacterMapMarkerPatch
	{
		private static void Postfix(CharacterModel __instance, ref CompressedTexture2D __result)
		{
			CharacterSkinProfile registeredProfile = CharacterSkinModBootstrap.RegisteredProfile;
			if ((object)registeredProfile != null)
			{
				CharacterSkinModBootstrap.ApplyCharacterMapMarker(registeredProfile, __instance, ref __result);
			}
		}
	}

	[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "AfterMultiplayerStarted")]
	private static class MultiplayerLoadScreenStartedPatch
	{
		private static void Postfix(NMultiplayerLoadGameScreen __instance)
		{
			SkinEventHub.Publish(new MultiplayerLoadScreenStartedEvent(__instance));
		}
	}

	[HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
	private static class CharacterSelectedPatch
	{
		private static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, CharacterModel characterModel)
		{
			RunStartPreloadRuntime.Current?.ScheduleCharacterSelectEntryWarmup(__instance, characterModel, "Character-select");
			SkinEventHub.Publish(new CharacterSelectedEvent(__instance, charSelectButton, characterModel));
		}
	}

	[HarmonyPatch(typeof(NCharacterSelectScreen), "OnLocalCharacterChangedForRandom")]
	private static class RandomCharacterResolvedPatch
	{
		private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
		{
			RunStartPreloadRuntime.Current?.ScheduleCharacterSelectEntryWarmup(__instance, characterModel, "Random character-select");
			SkinEventHub.Publish(new RandomCharacterResolvedEvent(__instance, characterModel));
		}
	}

	[HarmonyPatch(typeof(NCharacterSelectButton), "Select")]
	private static class CharacterSelectButtonSelectedPatch
	{
		private static void Postfix(NCharacterSelectButton __instance)
		{
			RunStartPreloadRuntime.Current?.ScheduleCharacterSelectEntryWarmup(__instance, __instance.Character, "Character-select button");
			SkinEventHub.Publish(new CharacterSelectButtonSelectedEvent(__instance));
		}
	}

	public static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlyArray<Type>(new Type[9]
	{
		typeof(CharacterSelectTitlePatch),
		typeof(CharacterSelectDescriptionPatch),
		typeof(CharacterTitlePatch),
		typeof(CharacterTitleObjectPatch),
		typeof(CharacterMapMarkerPatch),
		typeof(MultiplayerLoadScreenStartedPatch),
		typeof(CharacterSelectedPatch),
		typeof(RandomCharacterResolvedPatch),
		typeof(CharacterSelectButtonSelectedPatch)
	});
}
