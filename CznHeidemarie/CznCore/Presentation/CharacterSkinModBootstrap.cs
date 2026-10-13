using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CharacterSkinModBootstrap
{
	private enum InitializationState
	{
		NotStarted,
		Initializing,
		Initialized,
		Failed
	}

	private static readonly object InitializationSync = new object();

	private static readonly object CorePatchSync = new object();

	private static InitializationState _initializationState;

	private static bool _coreCompatibilityPatchesApplied;

	public static CharacterSkinProfile? RegisteredProfile { get; private set; }

	public static void Init(Type entryType, CharacterSkinProfile profile, string harmonyId, string initializedMessage, int logPriority = 2, Func<bool>? isDebugEnabled = null, Func<CardModel?, bool>? isPresentationTargetCard = null, Action? configureBeforePatching = null)
	{
		lock (InitializationSync)
		{
			if (_initializationState != InitializationState.NotStarted)
			{
				SkinLog.Warn("Ignored character skin initialization for " + harmonyId + " because state=" + _initializationState.ToString() + ". Restart the game before loading a replaced DLL.", logPriority);
				return;
			}
			_initializationState = InitializationState.Initializing;
		}
		try
		{
			RegisteredProfile = profile;
			SkinLog.Configure(profile.LogPrefix);
			SkinLog.ConfigureDebug(isDebugEnabled);
			CombatTriggeredCardComboRuntime.Configure(isPresentationTargetCard);
			configureBeforePatching?.Invoke();
			Harmony harmony = new Harmony(harmonyId);
			ApplyCoreCompatibilityPatchesSafely(harmony, logPriority);
			ApplyHarmonyPatchesSafely(entryType, harmony, logPriority);
			lock (InitializationSync)
			{
				_initializationState = InitializationState.Initialized;
			}
			SkinLog.Info(initializedMessage, logPriority);
		}
		catch (Exception ex)
		{
			lock (InitializationSync)
			{
				_initializationState = InitializationState.Failed;
			}
			SkinLog.Error("Failed to initialize: " + ex, logPriority);
		}
	}

	public static void ApplyHarmonyPatchesSafely(Type entryType, Harmony harmony, int logPriority = 2)
	{
		int num = 0;
		List<string> list = null;
		Type[] nestedTypes = entryType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
		foreach (Type type in nestedTypes)
		{
			if (HasHarmonyPatchAttribute(type))
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
					num++;
				}
				catch (Exception ex)
				{
					(list ?? (list = new List<string>())).Add(type.Name);
					SkinLog.Warn("Skipped incompatible patch class " + type.Name + " (target " + DescribePatchTarget(type) + "): " + FormatExceptionForLog(ex), logPriority);
				}
			}
		}
		SkinLog.Info("Harmony patch summary: patched=" + num + " skipped=" + (list?.Count ?? 0) + ((list != null) ? (" [" + string.Join(", ", list) + "]") : string.Empty), logPriority);
	}

	private static void ApplyCoreCompatibilityPatchesSafely(Harmony harmony, int logPriority)
	{
		lock (CorePatchSync)
		{
			if (_coreCompatibilityPatchesApplied)
			{
				return;
			}
			int patched = 0;
			List<string> skipped = null;
			foreach (Type patchType in RunStartPreloadHarmonyPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType2 in SkinTransportPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType2, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType3 in CombatBlockGainSfxReplacementPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType3, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType4 in CombatFormVfxSurfacePatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType4, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType5 in SkinCharacterSelectPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType5, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType6 in SkinCardTransportPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType6, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType7 in CombatCutInToggleHudPatches.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType7, ref patched, ref skipped, logPriority);
			}
			foreach (Type patchType8 in CombatTriggeredCardComboRuntime.PatchTypes)
			{
				ApplyCorePatchSafely(harmony, patchType8, ref patched, ref skipped, logPriority);
			}
			_coreCompatibilityPatchesApplied = true;
			SkinLog.Info("Core compatibility patch summary: patched=" + patched + " skipped=" + (skipped?.Count ?? 0) + ((skipped != null) ? (" [" + string.Join(", ", skipped) + "]") : string.Empty), logPriority);
		}
	}

	private static void ApplyCorePatchSafely(Harmony harmony, Type patchType, ref int patched, ref List<string>? skipped, int logPriority)
	{
		try
		{
			harmony.CreateClassProcessor(patchType).Patch();
			patched++;
		}
		catch (Exception ex)
		{
			(skipped ?? (skipped = new List<string>())).Add(patchType.Name);
			SkinLog.Warn("Skipped core compatibility patch " + patchType.Name + " (target " + DescribePatchTarget(patchType) + "): " + FormatExceptionForLog(ex), logPriority);
		}
	}

	private static string DescribePatchTarget(Type patchType)
	{
		try
		{
			Type type = null;
			string text = null;
			MethodType? methodType = null;
			Type[] array = null;
			object[] customAttributes = patchType.GetCustomAttributes(inherit: false);
			for (int i = 0; i < customAttributes.Length; i++)
			{
				if (customAttributes[i] is HarmonyAttribute { info: var info })
				{
					if ((object)type == null)
					{
						type = info.declaringType;
					}
					if (text == null)
					{
						text = info.methodName;
					}
					MethodType? methodType2 = methodType;
					if (!methodType2.HasValue)
					{
						methodType = info.methodType;
					}
					if (array == null)
					{
						array = info.argumentTypes;
					}
				}
			}
			if (type == null)
			{
				return "unresolved";
			}
			string text2 = type.Name + "." + (text ?? "<method-level>");
			if (HasTargetMethodResolver(patchType))
			{
				return text2 + " (TargetMethod)";
			}
			MethodType valueOrDefault = methodType.GetValueOrDefault();
			if (text == null && valueOrDefault != MethodType.Constructor)
			{
				return text2;
			}
			return (valueOrDefault switch
			{
				MethodType.Constructor => (MethodBase?)AccessTools.Constructor(type, array), 
				MethodType.Getter => AccessTools.PropertyGetter(type, text), 
				MethodType.Setter => AccessTools.PropertySetter(type, text), 
				MethodType.Normal => AccessTools.Method(type, text, array), 
				_ => null, 
			} == null) ? (text2 + " [missing]") : text2;
		}
		catch
		{
			return "unresolved";
		}
	}

	private static bool HasTargetMethodResolver(Type patchType)
	{
		if (!(patchType.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null))
		{
			return patchType.GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null;
		}
		return true;
	}

	public static void ApplyCharacterSelectTitle(CharacterSkinProfile profile, CharacterModel characterModel, ref string result)
	{
		if (TryGetCharacterSelectIdentity(profile, characterModel, out CharacterSelectIdentityProfile identity))
		{
			result = identity.TitleLocKey;
		}
	}

	public static void ApplyCharacterSelectDescription(CharacterSkinProfile profile, CharacterModel characterModel, ref string result)
	{
		if (TryGetCharacterSelectIdentity(profile, characterModel, out CharacterSelectIdentityProfile identity))
		{
			result = identity.DescriptionLocKey;
		}
	}

	public static void ApplyCharacterMapMarker(CharacterSkinProfile profile, CharacterModel characterModel, ref CompressedTexture2D result)
	{
		if (!profile.AppliesTo(characterModel))
		{
			return;
		}
		string effectiveMapMarkerIconPath = profile.CharacterSelectPresentation.EffectiveMapMarkerIconPath;
		if (!string.IsNullOrWhiteSpace(effectiveMapMarkerIconPath))
		{
			CompressedTexture2D compressedTexture2D = ResourceLoader.Load<CompressedTexture2D>(effectiveMapMarkerIconPath, null, ResourceLoader.CacheMode.Reuse);
			if (compressedTexture2D != null)
			{
				result = compressedTexture2D;
			}
		}
	}

	public static void ApplyCharacterTitle(CharacterSkinProfile profile, CharacterModel characterModel, ref LocString result)
	{
		if (TryGetCharacterSelectIdentity(profile, characterModel, out CharacterSelectIdentityProfile identity))
		{
			result = new LocString("characters", identity.TitleLocKey);
		}
	}

	public static void ApplyCharacterTitleObject(CharacterSkinProfile profile, CharacterModel characterModel, ref LocString result)
	{
		if (TryGetCharacterSelectIdentity(profile, characterModel, out CharacterSelectIdentityProfile identity))
		{
			result = new LocString("characters", GetCharacterTitleObjectLocKey(identity));
		}
	}

	public static bool TryGetCharacterSelectIdentity(CharacterSkinProfile profile, CharacterModel characterModel, out CharacterSelectIdentityProfile identity)
	{
		identity = profile.CharacterSelect;
		return profile.AppliesTo(characterModel);
	}

	private static bool HasHarmonyPatchAttribute(Type type)
	{
		try
		{
			object[] customAttributes = type.GetCustomAttributes(inherit: false);
			for (int i = 0; i < customAttributes.Length; i++)
			{
				if (customAttributes[i].GetType().FullName == "HarmonyLib.HarmonyPatch")
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static string GetCharacterTitleObjectLocKey(CharacterSelectIdentityProfile identity)
	{
		if (!identity.TitleLocKey.EndsWith(".title", StringComparison.Ordinal))
		{
			return identity.TitleLocKey;
		}
		string titleLocKey = identity.TitleLocKey;
		int length = ".title".Length;
		return titleLocKey.Substring(0, titleLocKey.Length - length) + ".titleObject";
	}

	private static string FormatExceptionForLog(Exception ex)
	{
		Exception ex2 = ex;
		while (ex2.InnerException != null)
		{
			ex2 = ex2.InnerException;
		}
		return ex2.GetType().Name + ": " + ex2.Message;
	}
}
