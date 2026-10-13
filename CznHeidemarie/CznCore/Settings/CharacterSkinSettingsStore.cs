using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using CznCore.Presentation;
using Godot;

namespace CznCore.Settings;

internal sealed class CharacterSkinSettingsStore
{
	private sealed class CharacterSettingDescriptor(string settingName, Action<CharacterSkinCharacterSettings, string> load, Func<CharacterSkinCharacterSettings, string> save)
	{
		public string SettingName { get; } = settingName;

		public Action<CharacterSkinCharacterSettings, string> Load { get; } = load;

		public Func<CharacterSkinCharacterSettings, string> Save { get; } = save;
	}

	public const float DefaultVoiceVolume = 0.3f;

	public const float DefaultCharacterVoiceCooldownSeconds = 0f;

	public const float DefaultEffectVolume = 1f;

	public const float MaxEffectVolume = 2f;

	public const float MaxVoiceVolume = 1f;

	public const float MaxCharacterVoiceCooldownSeconds = 10f;

	public const float DefaultEffectBrightness = 1f;

	public const float DefaultEffectOpacity = 1f;

	public const float MinEffectBrightness = 0.25f;

	public const float MaxEffectBrightness = 2f;

	public const float MinEffectOpacity = 0.25f;

	public const float MaxEffectOpacity = 1f;

	public const float DefaultBattleReadyScale = 1f;

	public const float DefaultBattleReadyOpacity = 1f;

	public const LeftSideCardPresentationMode DefaultBattleReadyOverlayMode = LeftSideCardPresentationMode.AllSupportedCards;

	public const bool DefaultCardPlayPresentationEnabled = true;

	public const AttackFullScreenPresentationMode DefaultAttackFullScreenPresentationMode = AttackFullScreenPresentationMode.Always;

	public const CardSelectionPresentationTrigger DefaultCardSelectionPresentationTrigger = CardSelectionPresentationTrigger.CardHover;

	public const bool DefaultMeleeApproachMovementEnabled = true;

	public const bool DefaultOfficialAttackTimingEnabled = true;

	public const CombatPresentationMode DefaultCombatPresentationMode = CombatPresentationMode.Full;

	public const DefeatCgOverlayMode DefaultDefeatCgOverlayMode = DefeatCgOverlayMode.Random;

	public const bool DefaultParticlesEnabled = true;

	public const bool DefaultTeammatePresentationEnabled = true;

	public const bool DefaultTeammatePresentationChannelEnabled = true;

	public const float DefaultTeammateVoiceCooldownSeconds = 0f;

	public const float MaxTeammateVoiceCooldownSeconds = 10f;

	public const AttackFullScreenPresentationMode DefaultTeammateAttackFullScreenPresentationMode = AttackFullScreenPresentationMode.LethalOnly;

	public const string StandardConfigRootPath = "user://CznModConfig";

	public const string StandardConfigFileName = "settings.cfg";

	public const string DebugLogEnabledSetting = "debug_log_enabled";

	public const string EffectsEnabledSetting = "effects_enabled";

	public const string GlobalParticlesEnabledSetting = "particles_enabled";

	public const string GlobalCombatSfxEnabledSetting = "combat_sfx_enabled";

	public const string TeammatePresentationEnabledSetting = "teammate_presentation_enabled";

	public const string TeammateEffectsEnabledSetting = "teammate_effects_enabled";

	public const string TeammateCombatSfxEnabledSetting = "teammate_combat_sfx_enabled";

	public const string TeammateVoiceEnabledSetting = "teammate_voice_enabled";

	public const string TeammateVoiceCooldownSetting = "teammate_voice_cooldown_seconds";

	public const string TeammateCutInEnabledSetting = "teammate_cut_in_enabled";

	public const string TeammateAttackFullScreenPresentationModeSetting = "teammate_attack_full_screen_presentation_mode";

	public const string ParticlesEnabledSetting = "particles_enabled";

	public const string CombatSfxEnabledSetting = "combat_sfx_enabled";

	public const string VoiceEnabledSetting = "voice_enabled";

	public const string EffectVolumeSetting = "effect_volume";

	public const string EffectBrightnessSetting = "effect_brightness";

	public const string EffectOpacitySetting = "effect_opacity";

	public const string VoiceVolumeSetting = "voice_volume";

	public const string VoiceCooldownSetting = "voice_cooldown_seconds";

	public const string BattleReadyScaleSetting = "battle_ready_scale";

	public const string BattleReadyOpacitySetting = "battle_ready_opacity";

	public const string BattleReadyOffsetXSetting = "battle_ready_offset_x";

	public const string BattleReadyOffsetYSetting = "battle_ready_offset_y";

	public const string BattleReadyOverlayEnabledSetting = "battle_ready_overlay_enabled";

	public const string BattleReadyOverlayModeSetting = "battle_ready_overlay_mode";

	public const string CardPlayPresentationEnabledSetting = "card_play_presentation_enabled";

	public const string AttackFullScreenPresentationModeSetting = "attack_full_screen_presentation_mode";

	public const string CardSelectionPresentationTriggerSetting = "card_selection_presentation_trigger";

	public const string MeleeApproachMovementEnabledSetting = "melee_approach_movement_enabled";

	public const string OfficialAttackTimingEnabledSetting = "official_attack_timing_enabled";

	public const string CombatPresentationModeSetting = "combat_presentation_mode";

	public const string DefeatCgOverlayEnabledSetting = "defeat_cg_overlay_enabled";

	public const string DefeatCgOverlayModeSetting = "defeat_cg_overlay_mode";

	public const string CharacterSelectBackgroundSetting = "character_select_background";

	private readonly string _configPath;

	private readonly string _fallbackCharacterId;

	private readonly float _saveDebounceSeconds;

	private readonly Action<string, int> _warn;

	private readonly Func<CharacterSkinSettingsStore, string, bool>? _tryLoadLegacyScalarConfig;

	private readonly Func<CharacterSkinSettingsStore, string, string, bool>? _tryLoadLegacySetting;

	private readonly Action<CharacterSkinSettingsStore>? _beforeSave;

	private readonly IReadOnlyList<string> _legacyConfigPaths;

	private readonly SharedMultiplayerSettingsStore _sharedMultiplayerSettings;

	private readonly Dictionary<string, CharacterSkinCharacterSettings> _characters = new Dictionary<string, CharacterSkinCharacterSettings>(StringComparer.OrdinalIgnoreCase);

	private static readonly CharacterSettingDescriptor[] CharacterSettingDescriptors = new CharacterSettingDescriptor[23]
	{
		BoolSetting("effects_enabled", (CharacterSkinCharacterSettings settings) => settings.EffectsEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.EffectsEnabled = value;
		}),
		BoolSetting("particles_enabled", (CharacterSkinCharacterSettings settings) => settings.ParticlesEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.ParticlesEnabled = value;
		}),
		BoolSetting("combat_sfx_enabled", (CharacterSkinCharacterSettings settings) => settings.CombatSfxEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.CombatSfxEnabled = value;
		}),
		BoolSetting("voice_enabled", (CharacterSkinCharacterSettings settings) => settings.VoiceEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.VoiceEnabled = value;
		}),
		FloatSetting("effect_volume", (CharacterSkinCharacterSettings settings) => settings.EffectVolume, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.EffectVolume = value;
		}, 0f, 2f),
		FloatSetting("effect_brightness", (CharacterSkinCharacterSettings settings) => settings.EffectBrightness, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.EffectBrightness = value;
		}, 0.25f, 2f),
		FloatSetting("effect_opacity", (CharacterSkinCharacterSettings settings) => settings.EffectOpacity, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.EffectOpacity = value;
		}, 0.25f, 1f),
		FloatSetting("voice_volume", (CharacterSkinCharacterSettings settings) => settings.VoiceVolume, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.VoiceVolume = value;
		}, 0f, 1f),
		FloatSetting("voice_cooldown_seconds", (CharacterSkinCharacterSettings settings) => settings.VoiceCooldownSeconds, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.VoiceCooldownSeconds = value;
		}, 0f, 10f),
		FloatSetting("battle_ready_scale", (CharacterSkinCharacterSettings settings) => settings.BattleReadyScale, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.BattleReadyScale = value;
		}, 0.5f, 2f),
		FloatSetting("battle_ready_opacity", (CharacterSkinCharacterSettings settings) => settings.BattleReadyOpacity, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.BattleReadyOpacity = value;
		}, 0.5f, 1f),
		FloatSetting("battle_ready_offset_x", (CharacterSkinCharacterSettings settings) => settings.BattleReadyOffsetX, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.BattleReadyOffsetX = value;
		}, -400f, 400f),
		FloatSetting("battle_ready_offset_y", (CharacterSkinCharacterSettings settings) => settings.BattleReadyOffsetY, (CharacterSkinCharacterSettings settings, float value) =>
		{
			settings.BattleReadyOffsetY = value;
		}, -400f, 400f),
		BoolSetting("battle_ready_overlay_enabled", (CharacterSkinCharacterSettings settings) => settings.BattleReadyOverlayMode != LeftSideCardPresentationMode.Off, SetBattleReadyOverlayEnabled),
		new CharacterSettingDescriptor("battle_ready_overlay_mode", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (LeftSideCardPresentationModeSettings.TryParseSettingValue(valueText, out var mode))
			{
				SetBattleReadyOverlayMode(settings, mode);
			}
		}, (CharacterSkinCharacterSettings settings) => LeftSideCardPresentationModeSettings.ToSettingValue(settings.BattleReadyOverlayMode)),
		BoolSetting("card_play_presentation_enabled", (CharacterSkinCharacterSettings settings) => settings.ShowCardPlayPresentation, SetShowCardPlayPresentation),
		new CharacterSettingDescriptor("attack_full_screen_presentation_mode", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (AttackFullScreenPresentationModeSettings.TryParseSettingValue(valueText, out var mode))
			{
				SetAttackFullScreenPresentationMode(settings, mode);
			}
		}, (CharacterSkinCharacterSettings settings) => AttackFullScreenPresentationModeSettings.ToSettingValue(settings.AttackFullScreenPresentationMode)),
		new CharacterSettingDescriptor("card_selection_presentation_trigger", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (CardSelectionPresentationTriggerSettings.TryParseSettingValue(valueText, out var trigger))
			{
				SetCardSelectionPresentationTrigger(settings, trigger);
			}
		}, (CharacterSkinCharacterSettings settings) => CardSelectionPresentationTriggerSettings.ToSettingValue(settings.CardSelectionPresentationTrigger)),
		BoolSetting("melee_approach_movement_enabled", (CharacterSkinCharacterSettings settings) => settings.MeleeApproachMovementEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.MeleeApproachMovementEnabled = value;
		}),
		BoolSetting("official_attack_timing_enabled", (CharacterSkinCharacterSettings settings) => settings.OfficialAttackTimingEnabled, (CharacterSkinCharacterSettings settings, bool value) =>
		{
			settings.OfficialAttackTimingEnabled = value;
		}),
		new CharacterSettingDescriptor("combat_presentation_mode", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (CombatPresentationModeSettings.TryParseSettingValue(valueText, out var mode))
			{
				SetCombatPresentationMode(settings, mode);
			}
		}, (CharacterSkinCharacterSettings settings) => CombatPresentationModeSettings.ToSettingValue(settings.CombatPresentationMode)),
		new CharacterSettingDescriptor("defeat_cg_overlay_mode", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (DefeatCgOverlayModeSettings.TryParseSettingValue(valueText, out var mode))
			{
				SetDefeatCgOverlayMode(settings, mode);
			}
		}, (CharacterSkinCharacterSettings settings) => DefeatCgOverlayModeSettings.ToSettingValue(settings.DefeatCgOverlayMode)),
		new CharacterSettingDescriptor("character_select_background", (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (CharacterSelectBackgroundVariantSettings.TryParseSettingValue(valueText, out var variant))
			{
				settings.CharacterSelectBackgroundVariant = variant;
			}
		}, (CharacterSkinCharacterSettings settings) => CharacterSelectBackgroundVariantSettings.ToSettingValue(settings.CharacterSelectBackgroundVariant))
	};

	private int _settingsLoaded;

	private int _saveGeneration;

	private bool _debugLogEnabled;

	private bool _globalEffectsEnabled = true;

	private bool _globalParticlesEnabled = true;

	private bool _globalCombatSfxEnabled = true;

	private bool _globalVoiceEnabled = true;

	public bool DebugLogEnabled
	{
		get
		{
			EnsureLoaded();
			return _debugLogEnabled;
		}
	}

	public bool GlobalEffectsEnabled
	{
		get
		{
			EnsureLoaded();
			return _globalEffectsEnabled;
		}
	}

	public bool GlobalParticlesEnabled
	{
		get
		{
			EnsureLoaded();
			return _globalParticlesEnabled;
		}
	}

	public bool GlobalCombatSfxEnabled
	{
		get
		{
			EnsureLoaded();
			return _globalCombatSfxEnabled;
		}
	}

	public bool GlobalVoiceEnabled
	{
		get
		{
			EnsureLoaded();
			return _globalVoiceEnabled;
		}
	}

	public bool TeammatePresentationEnabled
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.PresentationEnabled;
		}
	}

	public bool TeammateEffectsEnabled
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.EffectsEnabled;
		}
	}

	public bool TeammateCombatSfxEnabled
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.CombatSfxEnabled;
		}
	}

	public bool TeammateVoiceEnabled
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.VoiceEnabled;
		}
	}

	public float TeammateVoiceCooldownSeconds
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.VoiceCooldownSeconds;
		}
	}

	public bool TeammateCutInEnabled
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.CutInEnabled;
		}
	}

	public AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode
	{
		get
		{
			EnsureLoaded();
			return _sharedMultiplayerSettings.AttackFullScreenPresentationMode;
		}
	}

	internal event Action<string?>? Changed;

	public CharacterSkinSettingsStore(string configPath, string fallbackCharacterId, Action<string, int>? warn = null, Func<CharacterSkinSettingsStore, string, bool>? tryLoadLegacyScalarConfig = null, Func<CharacterSkinSettingsStore, string, string, bool>? tryLoadLegacySetting = null, Action<CharacterSkinSettingsStore>? beforeSave = null, float saveDebounceSeconds = 0.2f, IReadOnlyList<string>? legacyConfigPaths = null)
	{
		_configPath = configPath;
		_fallbackCharacterId = fallbackCharacterId;
		_warn = warn ?? new Action<string, int>(NoopWarn);
		_tryLoadLegacyScalarConfig = tryLoadLegacyScalarConfig;
		_tryLoadLegacySetting = tryLoadLegacySetting;
		_beforeSave = beforeSave;
		_saveDebounceSeconds = saveDebounceSeconds;
		_legacyConfigPaths = legacyConfigPaths ?? Array.Empty<string>();
		_sharedMultiplayerSettings = new SharedMultiplayerSettingsStore(_warn, saveDebounceSeconds);
	}

	public static string BuildConfigPath(string modId)
	{
		return "user://CznModConfig/" + NormalizeConfigPathSegment(modId) + "/settings.cfg";
	}

	public static string BuildLegacyRootConfigPath(string fileName)
	{
		return "user://" + NormalizeConfigPathSegment(fileName);
	}

	public bool GetCharacterEffectsEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.EffectsEnabled);
	}

	public bool GetCharacterVoiceEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.VoiceEnabled);
	}

	public bool GetCharacterParticlesEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.ParticlesEnabled);
	}

	public bool GetCharacterCombatSfxEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.CombatSfxEnabled);
	}

	public bool GetEffectiveCharacterEffectsEnabled(string characterId)
	{
		if (GlobalEffectsEnabled)
		{
			return GetCharacterEffectsEnabled(characterId);
		}
		return false;
	}

	public bool GetEffectiveCharacterCombatSfxEnabled(string characterId)
	{
		if (GlobalCombatSfxEnabled)
		{
			return GetCharacterCombatSfxEnabled(characterId);
		}
		return false;
	}

	public bool GetEffectiveCharacterParticlesEnabled(string characterId)
	{
		if (GlobalEffectsEnabled && GlobalParticlesEnabled && GetCharacterEffectsEnabled(characterId))
		{
			return GetCharacterParticlesEnabled(characterId);
		}
		return false;
	}

	public bool GetEffectiveCharacterVoiceEnabled(string characterId)
	{
		if (GlobalVoiceEnabled)
		{
			return GetCharacterVoiceEnabled(characterId);
		}
		return false;
	}

	public float GetCharacterEffectVolume(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.EffectVolume);
	}

	public float GetCharacterEffectBrightness(string characterId)
	{
		return Mathf.Clamp(GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.EffectBrightness), 0.25f, 2f);
	}

	public float GetCharacterEffectOpacity(string characterId)
	{
		return Mathf.Clamp(GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.EffectOpacity), 0.25f, 1f);
	}

	public float GetCharacterVoiceVolume(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.VoiceVolume);
	}

	public float GetCharacterVoiceCooldownSeconds(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.VoiceCooldownSeconds);
	}

	public float GetCharacterBattleReadyScale(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyScale);
	}

	public float GetCharacterBattleReadyOpacity(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyOpacity);
	}

	public float GetCharacterBattleReadyOffsetX(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyOffsetX);
	}

	public float GetCharacterBattleReadyOffsetY(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyOffsetY);
	}

	public bool GetCharacterShowBattleReadyOverlay(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyOverlayMode != LeftSideCardPresentationMode.Off);
	}

	public LeftSideCardPresentationMode GetCharacterBattleReadyOverlayMode(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.BattleReadyOverlayMode);
	}

	public bool GetCharacterShowCardPlayPresentation(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.ShowCardPlayPresentation);
	}

	public AttackFullScreenPresentationMode GetCharacterAttackFullScreenPresentationMode(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.AttackFullScreenPresentationMode);
	}

	public CardSelectionPresentationTrigger GetCharacterCardSelectionPresentationTrigger(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.CardSelectionPresentationTrigger);
	}

	public bool GetCharacterMeleeApproachMovementEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.MeleeApproachMovementEnabled);
	}

	public bool GetCharacterOfficialAttackTimingEnabled(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.OfficialAttackTimingEnabled);
	}

	public CombatPresentationMode GetCharacterCombatPresentationMode(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.CombatPresentationMode);
	}

	public bool GetCharacterShowDefeatCgOverlay(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.ShowDefeatCgOverlay);
	}

	public DefeatCgOverlayMode GetCharacterDefeatCgOverlayMode(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.DefeatCgOverlayMode);
	}

	public CharacterSelectBackgroundVariant GetCharacterSelectBackgroundVariant(string characterId)
	{
		return GetCharacterValue(characterId, (CharacterSkinCharacterSettings settings) => settings.CharacterSelectBackgroundVariant);
	}

	public void SetDebugLogEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_debugLogEnabled = value;
		RequestSaveIfNeeded(persist);
	}

	public void SetGlobalEffectsEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_globalEffectsEnabled = value;
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void SetGlobalParticlesEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_globalParticlesEnabled = value;
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void SetGlobalCombatSfxEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_globalCombatSfxEnabled = value;
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void SetGlobalVoiceEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_globalVoiceEnabled = value;
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void SetTeammatePresentationEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetPresentationEnabled(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateEffectsEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetEffectsEnabled(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateCombatSfxEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetCombatSfxEnabled(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateVoiceEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetVoiceEnabled(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateVoiceCooldownSeconds(float value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetVoiceCooldownSeconds(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateCutInEnabled(bool value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetCutInEnabled(value, persist);
		Changed?.Invoke(null);
	}

	public void SetTeammateAttackFullScreenPresentationMode(AttackFullScreenPresentationMode value, bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.SetAttackFullScreenPresentationMode(value, persist);
		Changed?.Invoke(null);
	}

	public void ApplyCombatPresentationModeToKnownCharacters(CombatPresentationMode mode, bool persist)
	{
		EnsureLoaded();
		EnsureCharacterRegistered(_fallbackCharacterId);
		foreach (CharacterSkinCharacterSettings value in _characters.Values)
		{
			SetCombatPresentationMode(value, mode);
		}
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void SetCharacterEffectsEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.EffectsEnabled = value;
		});
	}

	public void SetCharacterVoiceEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.VoiceEnabled = value;
		});
	}

	public void SetCharacterCombatSfxEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.CombatSfxEnabled = value;
		});
	}

	public void SetCharacterParticlesEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.ParticlesEnabled = value;
		});
	}

	public void SetCharacterEffectVolume(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.EffectVolume = Mathf.Clamp(value, 0f, 2f);
		});
	}

	public void SetCharacterEffectBrightness(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.EffectBrightness = Mathf.Clamp(value, 0.25f, 2f);
		});
	}

	public void SetCharacterEffectOpacity(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.EffectOpacity = Mathf.Clamp(value, 0.25f, 1f);
		});
	}

	public void SetCharacterVoiceVolume(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.VoiceVolume = Mathf.Clamp(value, 0f, 1f);
		});
	}

	public void SetCharacterVoiceCooldownSeconds(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.VoiceCooldownSeconds = Mathf.Clamp(value, 0f, 10f);
		});
	}

	public void SetCharacterBattleReadyScale(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.BattleReadyScale = Mathf.Clamp(value, 0.5f, 2f);
		});
	}

	public void SetCharacterBattleReadyOpacity(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.BattleReadyOpacity = Mathf.Clamp(value, 0.5f, 1f);
		});
	}

	public void SetCharacterBattleReadyOffsetX(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.BattleReadyOffsetX = Mathf.Clamp(value, -400f, 400f);
		});
	}

	public void SetCharacterBattleReadyOffsetY(string characterId, float value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.BattleReadyOffsetY = Mathf.Clamp(value, -400f, 400f);
		});
	}

	public void SetCharacterShowBattleReadyOverlay(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetBattleReadyOverlayEnabled(settings, value);
		});
	}

	public void SetCharacterBattleReadyOverlayMode(string characterId, LeftSideCardPresentationMode value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetBattleReadyOverlayMode(settings, value);
		});
	}

	public void SetCharacterShowCardPlayPresentation(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetShowCardPlayPresentation(settings, value);
		});
	}

	public void SetCharacterAttackFullScreenPresentationMode(string characterId, AttackFullScreenPresentationMode value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetAttackFullScreenPresentationMode(settings, value);
		});
	}

	public void SetCharacterCardSelectionPresentationTrigger(string characterId, CardSelectionPresentationTrigger value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetCardSelectionPresentationTrigger(settings, value);
		});
	}

	public void SetCharacterMeleeApproachMovementEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.MeleeApproachMovementEnabled = value;
		});
	}

	public void SetCharacterOfficialAttackTimingEnabled(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.OfficialAttackTimingEnabled = value;
		});
	}

	public void SetCharacterCombatPresentationMode(string characterId, CombatPresentationMode value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetCombatPresentationMode(settings, value);
		});
	}

	public void SetCharacterShowDefeatCgOverlay(string characterId, bool value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetDefeatCgOverlayEnabled(settings, value);
		});
	}

	public void SetCharacterDefeatCgOverlayMode(string characterId, DefeatCgOverlayMode value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			SetDefeatCgOverlayMode(settings, value);
		});
	}

	public void SetCharacterSelectBackgroundVariant(string characterId, CharacterSelectBackgroundVariant value, bool persist)
	{
		SetCharacterValue(characterId, persist, (CharacterSkinCharacterSettings settings) =>
		{
			settings.CharacterSelectBackgroundVariant = value;
		});
	}

	public void ResetGlobalToDefaults(bool persist)
	{
		EnsureLoaded();
		_debugLogEnabled = false;
		_globalEffectsEnabled = true;
		_globalParticlesEnabled = true;
		_globalCombatSfxEnabled = true;
		_globalVoiceEnabled = true;
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(null);
	}

	public void ResetTeammatePresentationToDefaults(bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.ResetToDefaults(persist);
		Changed?.Invoke(null);
	}

	public void EnableAllTeammatePresentationChannels(bool persist)
	{
		EnsureLoaded();
		_sharedMultiplayerSettings.EnableAllChannels(persist);
		Changed?.Invoke(null);
	}

	public void ResetCharacterToDefaults(string characterId, bool persist)
	{
		EnsureLoaded();
		GetCharacterSettings(characterId).Reset();
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(NormalizeCharacterId(characterId));
	}

	public CharacterSkinCharacterSettings GetCharacterSettings(string characterId)
	{
		string key = NormalizeCharacterId(characterId);
		if (!_characters.TryGetValue(key, out CharacterSkinCharacterSettings value))
		{
			value = new CharacterSkinCharacterSettings();
			_characters[key] = value;
		}
		return value;
	}

	private void EnsureCharacterRegistered(string characterId)
	{
		string key = NormalizeCharacterId(characterId);
		if (!_characters.ContainsKey(key))
		{
			_characters[key] = new CharacterSkinCharacterSettings();
		}
	}

	private T GetCharacterValue<T>(string characterId, Func<CharacterSkinCharacterSettings, T> read)
	{
		EnsureLoaded();
		return read(GetCharacterSettings(characterId));
	}

	private void SetCharacterValue(string characterId, bool persist, Action<CharacterSkinCharacterSettings> write)
	{
		EnsureLoaded();
		write(GetCharacterSettings(characterId));
		RequestSaveIfNeeded(persist);
		Changed?.Invoke(NormalizeCharacterId(characterId));
	}

	public string NormalizeCharacterId(string characterId)
	{
		if (!string.IsNullOrWhiteSpace(characterId))
		{
			return characterId.Trim();
		}
		return _fallbackCharacterId;
	}

	public void EnsureLoaded()
	{
		if (Interlocked.Exchange(ref _settingsLoaded, 1) != 0)
		{
			return;
		}
		bool flag = false;
		try
		{
			string text = ResolveConfigLoadPath();
			if (text != null)
			{
				using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open(text, global::Godot.FileAccess.ModeFlags.Read);
				string text2 = fileAccess?.GetAsText() ?? string.Empty;
				LoadSettingsText(text2);
				flag = !IsSameConfigPath(text, _configPath);
			}
		}
		catch (Exception ex)
		{
			ResetToDefaults();
			_warn("Skin settings load failed: " + ex.Message, 2);
		}
		_sharedMultiplayerSettings.EnsureLoaded();
		if (flag)
		{
			RequestSave();
		}
	}

	public void ReloadFromDisk()
	{
		ResetToDefaults();
		Interlocked.Exchange(ref _settingsLoaded, 0);
		EnsureLoaded();
		Changed?.Invoke(null);
	}

	public void RequestSave()
	{
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
			_warn("Skin settings deferred save failed: " + ex.Message, 2);
			SaveSettings();
		}
	}

	public static bool TryParseBoolSetting(string valueText, out bool value)
	{
		if (bool.TryParse(valueText, out value))
		{
			return true;
		}
		if (int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			value = result != 0;
			return true;
		}
		value = false;
		return false;
	}

	private void LoadSetting(string key, string valueText)
	{
		if (SkinSettingSchema.MatchesGlobal(key, "debug_log_enabled"))
		{
			if (TryParseBoolSetting(valueText, out var value))
			{
				_debugLogEnabled = value;
			}
		}
		else if (SkinSettingSchema.MatchesGlobal(key, "effects_enabled"))
		{
			if (TryParseBoolSetting(valueText, out var value2))
			{
				_globalEffectsEnabled = value2;
			}
		}
		else if (SkinSettingSchema.MatchesGlobal(key, "particles_enabled"))
		{
			if (TryParseBoolSetting(valueText, out var value3))
			{
				_globalParticlesEnabled = value3;
			}
		}
		else if (SkinSettingSchema.MatchesGlobal(key, "combat_sfx_enabled"))
		{
			if (TryParseBoolSetting(valueText, out var value4))
			{
				_globalCombatSfxEnabled = value4;
			}
		}
		else if (SkinSettingSchema.MatchesGlobal(key, "voice_enabled"))
		{
			if (TryParseBoolSetting(valueText, out var value5))
			{
				_globalVoiceEnabled = value5;
			}
		}
		else if (!TryLoadCharacterSetting(key, valueText))
		{
			_ = _tryLoadLegacySetting?.Invoke(this, key, valueText) ?? false;
		}
	}

	private bool TryLoadCharacterSetting(string key, string valueText)
	{
		if (!SkinSettingSchema.TryParseCharacterSetting(key, out string characterId, out string settingName))
		{
			return false;
		}
		CharacterSkinCharacterSettings characterSettings = GetCharacterSettings(characterId);
		if (string.Equals(settingName, "defeat_cg_overlay_enabled", StringComparison.Ordinal))
		{
			DefeatCgOverlayMode mode;
			if (TryParseBoolSetting(valueText, out var value))
			{
				SetDefeatCgOverlayEnabled(characterSettings, value);
			}
			else if (DefeatCgOverlayModeSettings.TryParseSettingValue(valueText, out mode))
			{
				SetDefeatCgOverlayMode(characterSettings, mode);
			}
			return true;
		}
		CharacterSettingDescriptor[] characterSettingDescriptors = CharacterSettingDescriptors;
		foreach (CharacterSettingDescriptor characterSettingDescriptor in characterSettingDescriptors)
		{
			if (string.Equals(settingName, characterSettingDescriptor.SettingName, StringComparison.Ordinal))
			{
				characterSettingDescriptor.Load(characterSettings, valueText);
				return true;
			}
		}
		return true;
	}

	private void RequestSaveIfNeeded(bool persist)
	{
		if (persist)
		{
			RequestSave();
		}
	}

	private void SaveSettings()
	{
		try
		{
			EnsureConfigDirectory();
			using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open(_configPath, global::Godot.FileAccess.ModeFlags.Write);
			_beforeSave?.Invoke(this);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(GlobalLine("debug_log_enabled", _debugLogEnabled));
			stringBuilder.Append(GlobalLine("effects_enabled", _globalEffectsEnabled));
			stringBuilder.Append(GlobalLine("particles_enabled", _globalParticlesEnabled));
			stringBuilder.Append(GlobalLine("combat_sfx_enabled", _globalCombatSfxEnabled));
			stringBuilder.Append(GlobalLine("voice_enabled", _globalVoiceEnabled));
			foreach (KeyValuePair<string, CharacterSkinCharacterSettings> character in _characters)
			{
				AppendCharacterSettings(stringBuilder, character.Key, character.Value);
			}
			fileAccess?.StoreString(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			_warn("Skin settings save failed: " + ex.Message, 2);
		}
	}

	private string? ResolveConfigLoadPath()
	{
		if (global::Godot.FileAccess.FileExists(_configPath))
		{
			return _configPath;
		}
		foreach (string legacyConfigPath in _legacyConfigPaths)
		{
			if (!string.IsNullOrWhiteSpace(legacyConfigPath) && !IsSameConfigPath(legacyConfigPath, _configPath) && global::Godot.FileAccess.FileExists(legacyConfigPath))
			{
				return legacyConfigPath;
			}
		}
		return null;
	}

	private void LoadSettingsText(string text)
	{
		string arg = text.Trim();
		Func<CharacterSkinSettingsStore, string, bool>? tryLoadLegacyScalarConfig = _tryLoadLegacyScalarConfig;
		if (tryLoadLegacyScalarConfig != null && tryLoadLegacyScalarConfig(this, arg))
		{
			return;
		}
		string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array)
		{
			int num = text2.IndexOf('=');
			if (num > 0)
			{
				string key = text2.Substring(0, num).Trim();
				string text3 = text2;
				int num2 = num + 1;
				string valueText = text3.Substring(num2, text3.Length - num2).Trim();
				LoadSetting(key, valueText);
			}
		}
	}

	private void EnsureConfigDirectory()
	{
		string directoryPath = GetDirectoryPath(_configPath);
		if (directoryPath != null)
		{
			Error error = DirAccess.MakeDirRecursiveAbsolute(directoryPath);
			if (error != Error.Ok && error != Error.AlreadyExists)
			{
				throw new InvalidOperationException("Failed to create skin settings directory: " + directoryPath + " (" + error.ToString() + ")");
			}
		}
	}

	private static string GlobalLine(string settingName, bool value)
	{
		return SkinSettingSchema.Global(settingName) + "=" + (value ? "true" : "false") + "\n";
	}

	private void AppendCharacterSettings(StringBuilder text, string characterId, CharacterSkinCharacterSettings character)
	{
		string characterId2 = NormalizeCharacterId(characterId);
		CharacterSettingDescriptor[] characterSettingDescriptors = CharacterSettingDescriptors;
		foreach (CharacterSettingDescriptor characterSettingDescriptor in characterSettingDescriptors)
		{
			text.Append(CharacterLine(characterId2, characterSettingDescriptor.SettingName, characterSettingDescriptor.Save(character)));
		}
	}

	private static string CharacterLine(string characterId, string settingName, string value)
	{
		return SkinSettingSchema.Character(characterId, settingName) + "=" + value + "\n";
	}

	private static string? GetDirectoryPath(string path)
	{
		int num = path.LastIndexOf('/');
		if (num >= "user://".Length)
		{
			return path.Substring(0, num);
		}
		return null;
	}

	private static bool IsSameConfigPath(string left, string right)
	{
		return string.Equals(left, right, StringComparison.Ordinal);
	}

	private static string NormalizeConfigPathSegment(string value)
	{
		string obj = (string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim());
		StringBuilder stringBuilder = new StringBuilder(obj.Length);
		string text = obj;
		foreach (char c in text)
		{
			bool flag = char.IsLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == '-' || c == '.' || c == '_') ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				stringBuilder.Append(c);
			}
			else
			{
				stringBuilder.Append('_');
			}
		}
		if (stringBuilder.Length != 0)
		{
			return stringBuilder.ToString();
		}
		return "unknown";
	}

	private static CharacterSettingDescriptor BoolSetting(string settingName, Func<CharacterSkinCharacterSettings, bool> read, Action<CharacterSkinCharacterSettings, bool> write)
	{
		return new CharacterSettingDescriptor(settingName, (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (TryParseBoolSetting(valueText, out var value))
			{
				write(settings, value);
			}
		}, (CharacterSkinCharacterSettings settings) => (!read(settings)) ? "false" : "true");
	}

	private static CharacterSettingDescriptor FloatSetting(string settingName, Func<CharacterSkinCharacterSettings, float> read, Action<CharacterSkinCharacterSettings, float> write, float min, float max)
	{
		return new CharacterSettingDescriptor(settingName, (CharacterSkinCharacterSettings settings, string valueText) =>
		{
			if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				write(settings, Mathf.Clamp(result, min, max));
			}
		}, (CharacterSkinCharacterSettings settings) => read(settings).ToString("0.###", CultureInfo.InvariantCulture));
	}

	private static void SetBattleReadyOverlayEnabled(CharacterSkinCharacterSettings settings, bool enabled)
	{
		SetBattleReadyOverlayMode(settings, enabled ? LeftSideCardPresentationMode.AllSupportedCards : LeftSideCardPresentationMode.Off);
	}

	private static void SetBattleReadyOverlayMode(CharacterSkinCharacterSettings settings, LeftSideCardPresentationMode mode)
	{
		settings.BattleReadyOverlayMode = ((!Enum.IsDefined(typeof(LeftSideCardPresentationMode), mode)) ? LeftSideCardPresentationMode.AllSupportedCards : mode);
		settings.ShowBattleReadyOverlay = settings.BattleReadyOverlayMode != LeftSideCardPresentationMode.Off;
	}

	private static void SetShowCardPlayPresentation(CharacterSkinCharacterSettings settings, bool enabled)
	{
		settings.ShowCardPlayPresentation = enabled;
	}

	private static void SetAttackFullScreenPresentationMode(CharacterSkinCharacterSettings settings, AttackFullScreenPresentationMode mode)
	{
		settings.AttackFullScreenPresentationMode = (Enum.IsDefined(typeof(AttackFullScreenPresentationMode), mode) ? mode : AttackFullScreenPresentationMode.Always);
	}

	private static void SetCardSelectionPresentationTrigger(CharacterSkinCharacterSettings settings, CardSelectionPresentationTrigger trigger)
	{
		settings.CardSelectionPresentationTrigger = (Enum.IsDefined(typeof(CardSelectionPresentationTrigger), trigger) ? trigger : CardSelectionPresentationTrigger.CardHover);
	}

	private static void SetCombatPresentationMode(CharacterSkinCharacterSettings settings, CombatPresentationMode mode)
	{
		settings.CombatPresentationMode = (Enum.IsDefined(typeof(CombatPresentationMode), mode) ? mode : CombatPresentationMode.Full);
		switch (settings.CombatPresentationMode)
		{
		case CombatPresentationMode.Fast:
			SetShowCardPlayPresentation(settings, enabled: false);
			settings.MeleeApproachMovementEnabled = false;
			settings.OfficialAttackTimingEnabled = false;
			break;
		case CombatPresentationMode.Full:
			SetShowCardPlayPresentation(settings, enabled: true);
			SetAttackFullScreenPresentationMode(settings, AttackFullScreenPresentationMode.Always);
			settings.MeleeApproachMovementEnabled = true;
			settings.OfficialAttackTimingEnabled = true;
			break;
		}
	}

	private static void SetDefeatCgOverlayEnabled(CharacterSkinCharacterSettings settings, bool enabled)
	{
		SetDefeatCgOverlayMode(settings, enabled ? DefeatCgOverlayMode.Random : DefeatCgOverlayMode.Off);
	}

	private static void SetDefeatCgOverlayMode(CharacterSkinCharacterSettings settings, DefeatCgOverlayMode mode)
	{
		settings.DefeatCgOverlayMode = (Enum.IsDefined(typeof(DefeatCgOverlayMode), mode) ? mode : DefeatCgOverlayMode.Random);
	}

	private void ResetToDefaults()
	{
		_debugLogEnabled = false;
		_globalEffectsEnabled = true;
		_globalParticlesEnabled = true;
		_globalCombatSfxEnabled = true;
		_globalVoiceEnabled = true;
		_characters.Clear();
	}

	private static void NoopWarn(string message, int priority)
	{
	}
}
