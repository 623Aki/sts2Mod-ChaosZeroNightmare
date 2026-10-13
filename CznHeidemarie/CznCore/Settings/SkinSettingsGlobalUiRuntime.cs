using System;
using System.Collections.Generic;
using System.Reflection;
using CznCore.Presentation;

namespace CznCore.Settings;

internal sealed class SkinSettingsGlobalUiRuntime : ISkinSettingsGlobalUiRuntime
{
	private sealed record SettingsStoreTarget(object Store, Type StoreType);

	private readonly CharacterSkinSettingsStore _store;

	private static bool _syncingGlobalSettings;

	private static readonly object StoreDiscoverySync = new object();

	private static int _cachedAssemblyCount = -1;

	private static SettingsStoreTarget[]? _cachedStoreTargets;

	public bool DebugLogEnabled => _store.DebugLogEnabled;

	public bool GlobalEffectsEnabled => _store.GlobalEffectsEnabled;

	public bool GlobalParticlesEnabled => _store.GlobalParticlesEnabled;

	public bool GlobalCombatSfxEnabled => _store.GlobalCombatSfxEnabled;

	public bool GlobalVoiceEnabled => _store.GlobalVoiceEnabled;

	public bool TeammatePresentationEnabled => _store.TeammatePresentationEnabled;

	public bool TeammateEffectsEnabled => _store.TeammateEffectsEnabled;

	public bool TeammateCombatSfxEnabled => _store.TeammateCombatSfxEnabled;

	public bool TeammateVoiceEnabled => _store.TeammateVoiceEnabled;

	public float TeammateVoiceCooldownSeconds => _store.TeammateVoiceCooldownSeconds;

	public bool TeammateCutInEnabled => _store.TeammateCutInEnabled;

	public AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode => _store.TeammateAttackFullScreenPresentationMode;

	public SkinSettingsGlobalUiRuntime(CharacterSkinSettingsStore store)
	{
		_store = store ?? throw new ArgumentNullException("store");
	}

	public void SetDebugLogEnabled(bool value, bool persist)
	{
		_store.SetDebugLogEnabled(value, persist);
		SyncOtherStores("SetDebugLogEnabled", new object[2] { value, persist });
	}

	public void SetGlobalEffectsEnabled(bool value, bool persist)
	{
		_store.SetGlobalEffectsEnabled(value, persist);
		SyncOtherStores("SetGlobalEffectsEnabled", new object[2] { value, persist });
	}

	public void SetGlobalParticlesEnabled(bool value, bool persist)
	{
		_store.SetGlobalParticlesEnabled(value, persist);
		SyncOtherStores("SetGlobalParticlesEnabled", new object[2] { value, persist });
	}

	public void SetGlobalCombatSfxEnabled(bool value, bool persist)
	{
		_store.SetGlobalCombatSfxEnabled(value, persist);
		SyncOtherStores("SetGlobalCombatSfxEnabled", new object[2] { value, persist });
	}

	public void SetGlobalVoiceEnabled(bool value, bool persist)
	{
		_store.SetGlobalVoiceEnabled(value, persist);
		SyncOtherStores("SetGlobalVoiceEnabled", new object[2] { value, persist });
	}

	public void SetTeammatePresentationEnabled(bool value, bool persist)
	{
		_store.SetTeammatePresentationEnabled(value, persist);
		SyncOtherStores("SetTeammatePresentationEnabled", new object[2] { value, false });
	}

	public void SetTeammateEffectsEnabled(bool value, bool persist)
	{
		_store.SetTeammateEffectsEnabled(value, persist);
		SyncOtherStores("SetTeammateEffectsEnabled", new object[2] { value, false });
	}

	public void SetTeammateCombatSfxEnabled(bool value, bool persist)
	{
		_store.SetTeammateCombatSfxEnabled(value, persist);
		SyncOtherStores("SetTeammateCombatSfxEnabled", new object[2] { value, false });
	}

	public void SetTeammateVoiceEnabled(bool value, bool persist)
	{
		_store.SetTeammateVoiceEnabled(value, persist);
		SyncOtherStores("SetTeammateVoiceEnabled", new object[2] { value, false });
	}

	public void SetTeammateVoiceCooldownSeconds(float value, bool persist)
	{
		_store.SetTeammateVoiceCooldownSeconds(value, persist);
		SyncOtherStores("SetTeammateVoiceCooldownSeconds", new object[2] { value, false });
	}

	public void SetTeammateCutInEnabled(bool value, bool persist)
	{
		_store.SetTeammateCutInEnabled(value, persist);
		SyncOtherStores("SetTeammateCutInEnabled", new object[2] { value, false });
	}

	public void SetTeammateAttackFullScreenPresentationMode(AttackFullScreenPresentationMode value, bool persist)
	{
		_store.SetTeammateAttackFullScreenPresentationMode(value, persist);
		SyncOtherStores("SetTeammateAttackFullScreenPresentationMode", new object[2] { value, false });
	}

	public void EnableAllTeammatePresentationChannels(bool persist)
	{
		_store.EnableAllTeammatePresentationChannels(persist);
		SyncOtherStores("EnableAllTeammatePresentationChannels", new object[1] { false });
	}

	public void ApplyCombatPresentationModeToLoadedCharacters(CombatPresentationMode mode, bool persist)
	{
		_store.ApplyCombatPresentationModeToKnownCharacters(mode, persist);
		SyncOtherStores("ApplyCombatPresentationModeToKnownCharacters", new object[2] { mode, persist });
	}

	public void ResetGlobalToDefaults(bool persist)
	{
		_store.ResetGlobalToDefaults(persist);
		SyncOtherStores("ResetGlobalToDefaults", new object[1] { persist });
	}

	public void ResetTeammatePresentationToDefaults(bool persist)
	{
		_store.ResetTeammatePresentationToDefaults(persist);
		SyncOtherStores("ResetTeammatePresentationToDefaults", new object[1] { false });
	}

	private void SyncOtherStores(string methodName, object?[] arguments)
	{
		if (_syncingGlobalSettings)
		{
			return;
		}
		_syncingGlobalSettings = true;
		try
		{
			foreach (SettingsStoreTarget loadedSettingsStoreTarget in GetLoadedSettingsStoreTargets())
			{
				if (loadedSettingsStoreTarget.Store == _store)
				{
					continue;
				}
				try
				{
					MethodInfo method = loadedSettingsStoreTarget.StoreType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						method.Invoke(loadedSettingsStoreTarget.Store, AdaptCrossAssemblyEnumArguments(method, arguments));
					}
				}
				catch (Exception ex)
				{
					SkinLog.Warn("Settings global sync target failed: " + SkinLog.FormatException(ex));
				}
			}
		}
		catch (Exception ex2)
		{
			SkinLog.Warn("Settings global sync failed: " + SkinLog.FormatException(ex2));
		}
		finally
		{
			_syncingGlobalSettings = false;
		}
	}

	private static object?[] AdaptCrossAssemblyEnumArguments(MethodInfo method, object?[] arguments)
	{
		ParameterInfo[] parameters = method.GetParameters();
		if (parameters.Length != arguments.Length)
		{
			return arguments;
		}
		object[] array = arguments;
		for (int i = 0; i < arguments.Length; i++)
		{
			object obj = arguments[i];
			Type parameterType = parameters[i].ParameterType;
			if (obj is Enum && parameterType.IsEnum && !parameterType.IsInstanceOfType(obj))
			{
				if (array == arguments)
				{
					array = (object[])arguments.Clone();
				}
				array[i] = Enum.ToObject(parameterType, Convert.ToInt32(obj));
			}
		}
		return array;
	}

	private static IReadOnlyList<SettingsStoreTarget> GetLoadedSettingsStoreTargets()
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		lock (StoreDiscoverySync)
		{
			if (_cachedStoreTargets != null && _cachedAssemblyCount == assemblies.Length)
			{
				return _cachedStoreTargets;
			}
			_cachedStoreTargets = DiscoverLoadedSettingsStores(assemblies);
			_cachedAssemblyCount = assemblies.Length;
			return _cachedStoreTargets;
		}
	}

	private static SettingsStoreTarget[] DiscoverLoadedSettingsStores(Assembly[] assemblies)
	{
		List<SettingsStoreTarget> list = new List<SettingsStoreTarget>();
		foreach (Assembly assembly in assemblies)
		{
			Type type = assembly.GetType(typeof(CharacterSkinSettingsStore).FullName ?? string.Empty, throwOnError: false);
			if (type == null)
			{
				continue;
			}
			Type[] assemblyTypesSafely = GetAssemblyTypesSafely(assembly);
			foreach (Type type2 in assemblyTypesSafely)
			{
				FieldInfo[] fields = type2.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (type.IsAssignableFrom(fieldInfo.FieldType))
					{
						object staticFieldValue = GetStaticFieldValue(fieldInfo);
						if (staticFieldValue != null)
						{
							AddStoreTarget(list, staticFieldValue);
						}
					}
				}
				PropertyInfo[] properties = type2.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (PropertyInfo propertyInfo in properties)
				{
					if (type.IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.GetIndexParameters().Length == 0)
					{
						object staticPropertyValue = GetStaticPropertyValue(propertyInfo);
						if (staticPropertyValue != null)
						{
							AddStoreTarget(list, staticPropertyValue);
						}
					}
				}
			}
		}
		return list.ToArray();
	}

	private static Type[] GetAssemblyTypesSafely(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			List<Type> list = new List<Type>();
			Type[] types = ex.Types;
			foreach (Type type in types)
			{
				if (type != null)
				{
					list.Add(type);
				}
			}
			return list.ToArray();
		}
		catch (Exception ex2)
		{
			SkinLog.Warn("Settings store discovery skipped assembly: " + SkinLog.FormatException(ex2), 3);
			return Array.Empty<Type>();
		}
	}

	private static object? GetStaticFieldValue(FieldInfo field)
	{
		try
		{
			return field.GetValue(null);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Settings store field discovery failed: " + SkinLog.FormatException(ex), 3);
			return null;
		}
	}

	private static object? GetStaticPropertyValue(PropertyInfo property)
	{
		try
		{
			return property.GetValue(null);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Settings store property discovery failed: " + SkinLog.FormatException(ex), 3);
			return null;
		}
	}

	private static void AddStoreTarget(List<SettingsStoreTarget> targets, object store)
	{
		foreach (SettingsStoreTarget target in targets)
		{
			if (target.Store == store)
			{
				return;
			}
		}
		targets.Add(new SettingsStoreTarget(store, store.GetType()));
	}
}
