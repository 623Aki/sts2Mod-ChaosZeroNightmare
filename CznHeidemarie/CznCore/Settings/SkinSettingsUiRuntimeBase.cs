using System;
using System.Collections.Generic;
using CznCore.Presentation;

namespace CznCore.Settings;

internal abstract class SkinSettingsUiRuntimeBase : ISkinSettingsUiRuntime
{
	[Flags]
	private enum Channels
	{
		None = 0,
		Effects = 1,
		Particles = 2,
		Sfx = 4,
		Voice = 8,
		CutIn = 0x10
	}

	private readonly record struct PresentationState(Channels Local, Channels Team, LeftSideCardPresentationMode PortraitMode, (float Scale, float Opacity, float X, float Y) Portrait, (float Brightness, float Opacity) Effects);

	protected readonly SkinCharacterSettings _settings;

	private readonly SkinSettingsUiRuntimeHooks _hooks;

	private PresentationState _applied;

	public abstract string CharacterSettingsKey { get; }

	public abstract string CharacterSettingsHeader { get; }

	public abstract string CharacterSkinTarget { get; }

	public abstract string CharacterSettingsAvatarPath { get; }

	public abstract string GlobalSettingsIconPath { get; }

	public abstract string CharacterEffectsLabel { get; }

	public abstract string CharacterVoiceLabel { get; }

	public abstract bool SupportsMeleeApproachMovement { get; }

	public abstract bool SupportsOfficialAttackTiming { get; }

	public abstract IReadOnlyList<CharacterSelectBackgroundVariant> SupportedCharacterSelectBackgroundVariants { get; }

	public CharacterSelectBackgroundVariant CharacterSelectBackgroundVariant => NormalizeBackgroundVariant(_settings.CharacterSelectBackgroundVariant);

	public bool EffectsEnabled => _settings.EffectsEnabled;

	public bool ParticlesEnabled => _settings.ParticlesEnabled;

	public bool CombatSfxEnabled => _settings.CombatSfxEnabled;

	public bool VoiceEnabled => _settings.VoiceEnabled;

	public float VoiceVolume => _settings.VoiceVolume;

	public float VoiceCooldownSeconds => _settings.VoiceCooldownSeconds;

	public float EffectVolume => _settings.EffectVolume;

	public float EffectBrightness => _settings.EffectBrightness;

	public float EffectOpacity => _settings.EffectOpacity;

	public float BattleReadyScale => _settings.BattleReadyScale;

	public float BattleReadyOpacity => _settings.BattleReadyOpacity;

	public float BattleReadyOffsetX => _settings.BattleReadyOffsetX;

	public float BattleReadyOffsetY => _settings.BattleReadyOffsetY;

	public bool ShowBattleReadyOverlay => _settings.ShowBattleReadyOverlay;

	public LeftSideCardPresentationMode BattleReadyOverlayMode => _settings.BattleReadyOverlayMode;

	public bool ShowCardPlayPresentation => _settings.ShowCardPlayPresentation;

	public AttackFullScreenPresentationMode AttackFullScreenPresentationMode => _settings.AttackFullScreenPresentationMode;

	public CardSelectionPresentationTrigger CardSelectionPresentationTrigger => _settings.CardSelectionPresentationTrigger;

	public bool MeleeApproachMovementEnabled
	{
		get
		{
			if (SupportsMeleeApproachMovement)
			{
				return _settings.MeleeApproachMovementEnabled;
			}
			return false;
		}
	}

	public bool OfficialAttackTimingEnabled
	{
		get
		{
			if (SupportsOfficialAttackTiming)
			{
				return _settings.OfficialAttackTimingEnabled;
			}
			return false;
		}
	}

	public CombatPresentationMode CombatPresentationMode => _settings.CombatPresentationMode;

	public bool ShowDefeatCgOverlay => _settings.ShowDefeatCgOverlay;

	public DefeatCgOverlayMode DefeatCgOverlayMode => _settings.DefeatCgOverlayMode;

	public virtual IReadOnlyList<SettingsUiExtensionGroup> CharacterSettingsExtensions => Array.Empty<SettingsUiExtensionGroup>();

	protected SkinSettingsUiRuntimeBase(SkinCharacterSettings settings, SkinSettingsUiRuntimeHooks? hooks = null)
	{
		_settings = settings;
		_hooks = hooks ?? new SkinSettingsUiRuntimeHooks();
		_applied = ReadPresentationState();
		settings.Subscribe(OnSettingsChanged);
	}

	protected abstract CharacterSelectBackgroundVariant NormalizeBackgroundVariant(CharacterSelectBackgroundVariant variant);

	public void SetEffectsEnabled(bool value, bool persist)
	{
		_settings.SetEffectsEnabled(value, persist);
	}

	public void SetParticlesEnabled(bool value, bool persist)
	{
		_settings.SetParticlesEnabled(value, persist);
	}

	public void SetCombatSfxEnabled(bool value, bool persist)
	{
		_settings.SetCombatSfxEnabled(value, persist);
	}

	public void SetVoiceEnabled(bool value, bool persist)
	{
		_settings.SetVoiceEnabled(value, persist);
	}

	public void SetVoiceVolume(float value, bool persist)
	{
		_settings.SetVoiceVolume(value, persist);
	}

	public void SetVoiceCooldownSeconds(float value, bool persist)
	{
		_settings.SetVoiceCooldownSeconds(value, persist);
	}

	public void SetEffectVolume(float value, bool persist)
	{
		_settings.SetEffectVolume(value, persist);
	}

	public void SetEffectBrightness(float value, bool persist)
	{
		_settings.SetEffectBrightness(value, persist);
	}

	public void SetEffectOpacity(float value, bool persist)
	{
		_settings.SetEffectOpacity(value, persist);
	}

	public void SetBattleReadyScale(float value, bool persist)
	{
		_settings.SetBattleReadyScale(value, persist);
	}

	public void SetBattleReadyOpacity(float value, bool persist)
	{
		_settings.SetBattleReadyOpacity(value, persist);
	}

	public void SetBattleReadyOffsetX(float value, bool persist)
	{
		_settings.SetBattleReadyOffsetX(value, persist);
	}

	public void SetBattleReadyOffsetY(float value, bool persist)
	{
		_settings.SetBattleReadyOffsetY(value, persist);
	}

	public void SetBattleReadyOverlayEnabled(bool enabled, bool persist)
	{
		_settings.SetShowBattleReadyOverlay(enabled, persist);
	}

	public void SetBattleReadyOverlayMode(LeftSideCardPresentationMode mode, bool persist)
	{
		_settings.SetBattleReadyOverlayMode(mode, persist);
	}

	public void SetCardPlayPresentationEnabled(bool enabled, bool persist)
	{
		_settings.SetShowCardPlayPresentation(enabled, persist);
	}

	public void SetAttackFullScreenPresentationMode(AttackFullScreenPresentationMode mode, bool persist)
	{
		_settings.SetAttackFullScreenPresentationMode(mode, persist);
	}

	public void SetCardSelectionPresentationTrigger(CardSelectionPresentationTrigger trigger, bool persist)
	{
		_settings.SetCardSelectionPresentationTrigger(trigger, persist);
	}

	public void SetMeleeApproachMovementEnabled(bool enabled, bool persist)
	{
		_settings.SetMeleeApproachMovementEnabled(SupportsMeleeApproachMovement & enabled, persist);
	}

	public void SetOfficialAttackTimingEnabled(bool enabled, bool persist)
	{
		_settings.SetOfficialAttackTimingEnabled(SupportsOfficialAttackTiming & enabled, persist);
	}

	public void SetCombatPresentationMode(CombatPresentationMode mode, bool persist)
	{
		_settings.SetCombatPresentationMode(mode, persist);
	}

	public void SetDefeatCgOverlayEnabled(bool enabled, bool persist)
	{
		_settings.SetShowDefeatCgOverlay(enabled, persist);
	}

	public void SetDefeatCgOverlayMode(DefeatCgOverlayMode mode, bool persist)
	{
		_settings.SetDefeatCgOverlayMode(mode, persist);
	}

	public void SetCharacterSelectBackgroundVariant(CharacterSelectBackgroundVariant variant, bool persist)
	{
		_settings.SetCharacterSelectBackgroundVariant(NormalizeBackgroundVariant(variant), persist);
	}

	public void ResetCharacterToDefaults(bool persist)
	{
		_settings.ResetToDefaults(persist);
		foreach (SettingsUiExtensionGroup characterSettingsExtension in CharacterSettingsExtensions)
		{
			characterSettingsExtension.Reset?.Invoke();
		}
	}

	public void ApplyBattleReadyTransform()
	{
		_hooks.ApplyBattleReadyTransform?.Invoke();
	}

	public void CleanupBattleReadyOverlay()
	{
		_hooks.CleanupBattleReadyOverlay?.Invoke();
	}

	public void CleanupSkinEffectsRuntime()
	{
		_hooks.CleanupSkinEffectsRuntime?.Invoke();
	}

	public void CleanupSkinVisualEffectsRuntime()
	{
		_hooks.CleanupSkinVisualEffectsRuntime?.Invoke();
	}

	public void CleanupSkinSfxRuntime()
	{
		_hooks.CleanupSkinSfxRuntime?.Invoke();
	}

	public void StopVoice()
	{
		_hooks.StopVoice?.Invoke();
	}

	public void DebugLog(string message, int priority = 2)
	{
		_hooks.DebugLog?.Invoke(message, priority);
	}

	private PresentationState ReadPresentationState()
	{
		Channels channels = Channels.None;
		if (_settings.EffectiveEffectsEnabled)
		{
			channels |= Channels.Effects;
		}
		if (_settings.EffectiveParticlesEnabled)
		{
			channels |= Channels.Particles;
		}
		if (_settings.EffectiveCombatSfxEnabled)
		{
			channels |= Channels.Sfx;
		}
		if (_settings.EffectiveVoiceEnabled)
		{
			channels |= Channels.Voice;
		}
		if (_settings.ShowCardPlayPresentation)
		{
			channels |= Channels.CutIn;
		}
		Channels channels2 = Channels.None;
		if (_settings.TeammatePresentationEnabled)
		{
			if (_settings.TeammateEffectsEnabled)
			{
				channels2 |= channels & (Channels.Effects | Channels.Particles);
			}
			if (_settings.TeammateCombatSfxEnabled)
			{
				channels2 |= channels & Channels.Sfx;
			}
			if (_settings.TeammateVoiceEnabled)
			{
				channels2 |= channels & Channels.Voice;
			}
			if (_settings.TeammateCutInEnabled)
			{
				channels2 |= Channels.CutIn;
			}
		}
		return new PresentationState(channels, channels2, _settings.BattleReadyOverlayMode, (Scale: _settings.BattleReadyScale, Opacity: _settings.BattleReadyOpacity, X: _settings.BattleReadyOffsetX, Y: _settings.BattleReadyOffsetY), (Brightness: _settings.EffectBrightness, Opacity: _settings.EffectOpacity));
	}

	private void OnSettingsChanged(string? characterId)
	{
		if (characterId == null || !(characterId != _settings.CharacterId))
		{
			PresentationState applied = _applied;
			PresentationState presentationState = (_applied = ReadPresentationState());
			Channels channels = (applied.Local & ~presentationState.Local) | (applied.Team & ~presentationState.Team);
			if ((channels & Channels.Effects) != 0)
			{
				CleanupSkinEffectsRuntime();
			}
			else if ((channels & Channels.Particles) != 0)
			{
				CleanupSkinVisualEffectsRuntime();
			}
			if ((channels & Channels.Sfx) != 0)
			{
				CleanupSkinSfxRuntime();
			}
			if ((channels & Channels.Voice) != 0)
			{
				StopVoice();
			}
			if ((channels & Channels.CutIn) != 0)
			{
				_hooks.ApplyCardPlayPresentationEnabled?.Invoke(obj: false);
			}
			else if ((((presentationState.Local & ~applied.Local) | (presentationState.Team & ~applied.Team)) & Channels.CutIn) != 0)
			{
				_hooks.ApplyCardPlayPresentationEnabled?.Invoke(obj: true);
			}
			if (applied.PortraitMode != presentationState.PortraitMode)
			{
				CleanupBattleReadyOverlay();
			}
			(float, float, float, float) portrait = applied.Portrait;
			(float, float, float, float) portrait2 = presentationState.Portrait;
			if (portrait.Item1 != portrait2.Item1 || portrait.Item2 != portrait2.Item2 || portrait.Item3 != portrait2.Item3 || portrait.Item4 != portrait2.Item4)
			{
				ApplyBattleReadyTransform();
			}
			(float, float) effects = applied.Effects;
			(float, float) effects2 = presentationState.Effects;
			if (effects.Item1 != effects2.Item1 || effects.Item2 != effects2.Item2)
			{
				_hooks.ApplyEffectBrightnessSettings?.Invoke();
			}
		}
	}
}
