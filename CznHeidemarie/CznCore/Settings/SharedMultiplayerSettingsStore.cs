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

	/// <summary>联机时是否以房主的设置为标准（默认开启）。</summary>
	public bool UseHostSettings => _settings.UseHostSettings;

	/// <summary>
	/// 房主设置接管期间置为 true：此时任何写入都不落盘，
	/// 避免「陪房主玩一局」把自己的本地配置改掉。
	/// </summary>
	public bool PersistSuppressed { get; set; }

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

	public void SetUseHostSettings(bool value, bool persist)
	{
		_settings = _settings with
		{
			UseHostSettings = value
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
		if (!persist || PersistSuppressed)
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
				if (SkinSettingSchema.MatchesGlobal(key, "multiplayer_use_host_settings"))
				{
					_settings = _settings with
					{
						UseHostSettings = value
					};
				}
				else if (SkinSettingSchema.MatchesGlobal(key, "teammate_presentation_enabled"))
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

	/// <summary>
	/// 导出「需要跟着房主走」的队友演出设置。
	/// 文本格式与磁盘 <c>multiplayer.cfg</c> 一致，可直接被 <see cref="ApplyHostSyncedText"/> 解析。
	/// </summary>
	public string ExportHostSyncedText()
	{
		EnsureLoaded();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(GlobalLine("teammate_presentation_enabled", _settings.PresentationEnabled));
		stringBuilder.Append(GlobalLine("teammate_effects_enabled", _settings.EffectsEnabled));
		stringBuilder.Append(GlobalLine("teammate_combat_sfx_enabled", _settings.CombatSfxEnabled));
		stringBuilder.Append(GlobalLine("teammate_voice_enabled", _settings.VoiceEnabled));
		stringBuilder.Append(GlobalLine("teammate_voice_cooldown_seconds", _settings.VoiceCooldownSeconds));
		stringBuilder.Append(GlobalLine("teammate_cut_in_enabled", _settings.CutInEnabled));
		stringBuilder.Append(GlobalLine("teammate_attack_full_screen_presentation_mode", AttackFullScreenPresentationModeSettings.ToSettingValue(_settings.AttackFullScreenPresentationMode)));
		return stringBuilder.ToString();
	}

	/// <summary>
	/// 应用房主下发的队友演出设置。只认 <c>teammate_*</c> 这组键，
	/// 其它内容被 <see cref="LoadSettingsText"/> 自然忽略（本身就是白名单解析）。
	/// </summary>
	public void ApplyHostSyncedText(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		// 白名单过滤：这组键只允许 teammate_*。
		// 「是否跟随房主」是本机策略，绝不能被房主下发的内容改写。
		StringBuilder filtered = new StringBuilder();
		string[] lines = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string line in lines)
		{
			if (line.IndexOf("multiplayer_use_host_settings", StringComparison.Ordinal) >= 0)
			{
				continue;
			}
			filtered.Append(line).Append('\n');
		}
		LoadSettingsText(filtered.ToString());
	}

	/// <summary>当前快照，用于房主接管前的本地备份。</summary>
	public SharedMultiplayerSettingsSnapshot Snapshot
	{
		get
		{
			EnsureLoaded();
			return _settings;
		}
	}

	/// <summary>
	/// 恢复本地快照。
	///
	/// <para><c>UseHostSettings</c> 是玩家自己的选择，不属于「被房主覆盖」的字段，
	/// 恢复时必须保留当前值，否则玩家在接管期间关掉的开关会被悄悄改回开启。</para>
	/// </summary>
	public void RestoreFromSnapshot(SharedMultiplayerSettingsSnapshot snapshot)
	{
		_settings = snapshot with
		{
			UseHostSettings = _settings.UseHostSettings
		};
	}

	/// <summary>
	/// 接管结束、解除写盘抑制后补写一次。
	/// 用于「玩家在接管期间改过『以房主为准』开关」这种被抑制掉的本地改动。
	/// </summary>
	public void PersistNow()
	{
		if (!PersistSuppressed)
		{
			SaveSettings();
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
			stringBuilder.Append(GlobalLine("multiplayer_use_host_settings", _settings.UseHostSettings));
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
