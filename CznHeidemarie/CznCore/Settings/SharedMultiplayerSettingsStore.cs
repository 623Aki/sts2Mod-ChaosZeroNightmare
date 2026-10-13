using System;
using System.Globalization;
using System.Text;
using System.Threading;
using CznCore.Presentation;
using Godot;

namespace CznCore.Settings;

internal sealed class SharedMultiplayerSettingsStore
{
	public const string ConfigPath = "user://CznModConfig/shared/multiplayer.cfg";

	private readonly float _saveDebounceSeconds;

	private readonly Action<string, int> _warn;

	private int _settingsLoaded;

	private int _saveGeneration;

	private SharedMultiplayerSettingsSnapshot _settings = SharedMultiplayerSettingsSnapshot.Defaults;

	public bool PresentationEnabled => _settings.PresentationEnabled;

	public bool EffectsEnabled => _settings.EffectsEnabled;

	public bool CombatSfxEnabled => _settings.CombatSfxEnabled;

	public bool VoiceEnabled => _settings.VoiceEnabled;

	public float VoiceCooldownSeconds => _settings.VoiceCooldownSeconds;

	public bool CutInEnabled => _settings.CutInEnabled;

	public AttackFullScreenPresentationMode AttackFullScreenPresentationMode => _settings.AttackFullScreenPresentationMode;

	public SharedMultiplayerSettingsStore(Action<string, int> warn, float saveDebounceSeconds)
	{
		_warn = warn ?? throw new ArgumentNullException("warn");
		_saveDebounceSeconds = saveDebounceSeconds;
	}

	public void EnsureLoaded()
	{
		if (Interlocked.Exchange(ref _settingsLoaded, 1) != 0)
		{
			return;
		}
		_settings = SharedMultiplayerSettingsSnapshot.Defaults;
		if (!global::Godot.FileAccess.FileExists("user://CznModConfig/shared/multiplayer.cfg"))
		{
			SaveSettings();
			return;
		}
		try
		{
			using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open("user://CznModConfig/shared/multiplayer.cfg", global::Godot.FileAccess.ModeFlags.Read);
			LoadSettingsText(fileAccess?.GetAsText() ?? string.Empty);
		}
		catch (Exception ex)
		{
			_settings = SharedMultiplayerSettingsSnapshot.Defaults;
			_warn("Shared multiplayer settings load failed: " + ex.Message, 2);
		}
	}

	public void SetPresentationEnabled(bool value, bool persist)
	{
		_settings = _settings with
		{
			PresentationEnabled = value
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetEffectsEnabled(bool value, bool persist)
	{
		_settings = _settings with
		{
			EffectsEnabled = value
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetCombatSfxEnabled(bool value, bool persist)
	{
		_settings = _settings with
		{
			CombatSfxEnabled = value
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetVoiceEnabled(bool value, bool persist)
	{
		_settings = _settings with
		{
			VoiceEnabled = value
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetVoiceCooldownSeconds(float value, bool persist)
	{
		_settings = _settings with
		{
			VoiceCooldownSeconds = Mathf.Clamp(value, 0f, 10f)
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetCutInEnabled(bool value, bool persist)
	{
		_settings = _settings with
		{
			CutInEnabled = value
		};
		RequestSaveIfNeeded(persist);
	}

	public void SetAttackFullScreenPresentationMode(AttackFullScreenPresentationMode value, bool persist)
	{
		_settings = _settings with
		{
			AttackFullScreenPresentationMode = ((!Enum.IsDefined(typeof(AttackFullScreenPresentationMode), value)) ? AttackFullScreenPresentationMode.LethalOnly : value)
		};
		RequestSaveIfNeeded(persist);
	}

	public void EnableAllChannels(bool persist)
	{
		_settings = _settings with
		{
			PresentationEnabled = true,
			EffectsEnabled = true,
			CombatSfxEnabled = true,
			VoiceEnabled = true,
			CutInEnabled = true
		};
		RequestSaveIfNeeded(persist);
	}

	public void ResetToDefaults(bool persist)
	{
		_settings = SharedMultiplayerSettingsSnapshot.Defaults;
		RequestSaveIfNeeded(persist);
	}

	private void RequestSaveIfNeeded(bool persist)
	{
		if (!persist)
		{
			return;
		}
		int generation = Interlocked.Increment(ref _saveGeneration);
		try
		{
			if (!(Engine.GetMainLoop() is SceneTree sceneTree))
			{
				SaveSettings();
				return;
			}
			sceneTree.CreateTimer(_saveDebounceSeconds).Timeout += () =>
			{
				if (generation == Volatile.Read(in _saveGeneration))
				{
					SaveSettings();
				}
			};
		}
		catch (Exception ex)
		{
			_warn("Shared multiplayer settings deferred save failed: " + ex.Message, 2);
			SaveSettings();
		}
	}

	private void LoadSettingsText(string text)
	{
		string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array)
		{
			int num = text2.IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string key = text2.Substring(0, num).Trim();
			string text3 = text2;
			int num2 = num + 1;
			string text4 = text3.Substring(num2, text3.Length - num2).Trim();
			bool value;
			if (SkinSettingSchema.MatchesGlobal(key, "teammate_voice_cooldown_seconds"))
			{
				if (float.TryParse(text4, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					_settings = _settings with
					{
						VoiceCooldownSeconds = Mathf.Clamp(result, 0f, 10f)
					};
				}
			}
			else if (SkinSettingSchema.MatchesGlobal(key, "teammate_attack_full_screen_presentation_mode"))
			{
				if (AttackFullScreenPresentationModeSettings.TryParseSettingValue(text4, out var mode))
				{
					_settings = _settings with
					{
						AttackFullScreenPresentationMode = mode
					};
				}
			}
			else if (CharacterSkinSettingsStore.TryParseBoolSetting(text4, out value))
			{
				if (SkinSettingSchema.MatchesGlobal(key, "teammate_presentation_enabled"))
				{
					_settings = _settings with
					{
						PresentationEnabled = value
					};
				}
				else if (SkinSettingSchema.MatchesGlobal(key, "teammate_effects_enabled"))
				{
					_settings = _settings with
					{
						EffectsEnabled = value
					};
				}
				else if (SkinSettingSchema.MatchesGlobal(key, "teammate_combat_sfx_enabled"))
				{
					_settings = _settings with
					{
						CombatSfxEnabled = value
					};
				}
				else if (SkinSettingSchema.MatchesGlobal(key, "teammate_voice_enabled"))
				{
					_settings = _settings with
					{
						VoiceEnabled = value
					};
				}
				else if (SkinSettingSchema.MatchesGlobal(key, "teammate_cut_in_enabled"))
				{
					_settings = _settings with
					{
						CutInEnabled = value
					};
				}
			}
		}
	}

	private void SaveSettings()
	{
		try
		{
			Error error = DirAccess.MakeDirRecursiveAbsolute("user://CznModConfig/shared");
			if (error != Error.Ok && error != Error.AlreadyExists)
			{
				throw new InvalidOperationException("Failed to create shared multiplayer settings directory: user://CznModConfig/shared (" + error.ToString() + ")");
			}
			using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open("user://CznModConfig/shared/multiplayer.cfg", global::Godot.FileAccess.ModeFlags.Write);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(GlobalLine("teammate_presentation_enabled", _settings.PresentationEnabled));
			stringBuilder.Append(GlobalLine("teammate_effects_enabled", _settings.EffectsEnabled));
			stringBuilder.Append(GlobalLine("teammate_combat_sfx_enabled", _settings.CombatSfxEnabled));
			stringBuilder.Append(GlobalLine("teammate_voice_enabled", _settings.VoiceEnabled));
			stringBuilder.Append(GlobalLine("teammate_voice_cooldown_seconds", _settings.VoiceCooldownSeconds));
			stringBuilder.Append(GlobalLine("teammate_cut_in_enabled", _settings.CutInEnabled));
			stringBuilder.Append(GlobalLine("teammate_attack_full_screen_presentation_mode", AttackFullScreenPresentationModeSettings.ToSettingValue(_settings.AttackFullScreenPresentationMode)));
			fileAccess?.StoreString(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			_warn("Shared multiplayer settings save failed: " + ex.Message, 2);
		}
	}

	private static string GlobalLine(string settingName, bool value)
	{
		return SkinSettingSchema.Global(settingName) + "=" + (value ? "true" : "false") + "\n";
	}

	private static string GlobalLine(string settingName, float value)
	{
		return SkinSettingSchema.Global(settingName) + "=" + value.ToString("0.###", CultureInfo.InvariantCulture) + "\n";
	}

	private static string GlobalLine(string settingName, string value)
	{
		return SkinSettingSchema.Global(settingName) + "=" + value + "\n";
	}
}
