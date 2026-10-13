using System.Collections.Generic;
using CznCore.Presentation;

namespace CznCore.Settings;

internal interface ISkinSettingsUiRuntime
{
	SkinSettingsUiCapabilities Capabilities => SkinSettingsUiCapabilities.All;

	string CharacterSettingsKey { get; }

	string CharacterSettingsHeader { get; }

	string CharacterSkinTarget { get; }

	string CharacterSettingsAvatarPath { get; }

	string GlobalSettingsIconPath { get; }

	string CharacterEffectsLabel { get; }

	string CharacterVoiceLabel { get; }

	bool EffectsEnabled { get; }

	bool ParticlesEnabled { get; }

	bool CombatSfxEnabled { get; }

	bool VoiceEnabled { get; }

	float VoiceVolume { get; }

	float VoiceCooldownSeconds { get; }

	float EffectVolume { get; }

	float EffectBrightness { get; }

	float EffectOpacity { get; }

	float BattleReadyScale { get; }

	float BattleReadyOpacity { get; }

	float BattleReadyOffsetX { get; }

	float BattleReadyOffsetY { get; }

	bool ShowBattleReadyOverlay { get; }

	LeftSideCardPresentationMode BattleReadyOverlayMode { get; }

	bool ShowCardPlayPresentation { get; }

	AttackFullScreenPresentationMode AttackFullScreenPresentationMode { get; }

	CardSelectionPresentationTrigger CardSelectionPresentationTrigger { get; }

	bool SupportsMeleeApproachMovement { get; }

	bool MeleeApproachMovementEnabled { get; }

	bool SupportsOfficialAttackTiming { get; }

	bool OfficialAttackTimingEnabled { get; }

	CombatPresentationMode CombatPresentationMode { get; }

	bool ShowDefeatCgOverlay { get; }

	DefeatCgOverlayMode DefeatCgOverlayMode { get; }

	CharacterSelectBackgroundVariant CharacterSelectBackgroundVariant { get; }

	IReadOnlyList<CharacterSelectBackgroundVariant> SupportedCharacterSelectBackgroundVariants { get; }

	IReadOnlyList<SettingsUiExtensionGroup> CharacterSettingsExtensions { get; }

	void SetEffectsEnabled(bool value, bool persist);

	void SetParticlesEnabled(bool value, bool persist);

	void SetCombatSfxEnabled(bool value, bool persist);

	void SetVoiceEnabled(bool value, bool persist);

	void SetVoiceVolume(float value, bool persist);

	void SetVoiceCooldownSeconds(float value, bool persist);

	void SetEffectVolume(float value, bool persist);

	void SetEffectBrightness(float value, bool persist);

	void SetEffectOpacity(float value, bool persist);

	void SetBattleReadyScale(float value, bool persist);

	void SetBattleReadyOpacity(float value, bool persist);

	void SetBattleReadyOffsetX(float value, bool persist);

	void SetBattleReadyOffsetY(float value, bool persist);

	void SetBattleReadyOverlayEnabled(bool enabled, bool persist);

	void SetBattleReadyOverlayMode(LeftSideCardPresentationMode mode, bool persist);

	void SetCardPlayPresentationEnabled(bool enabled, bool persist);

	void SetAttackFullScreenPresentationMode(AttackFullScreenPresentationMode mode, bool persist);

	void SetCardSelectionPresentationTrigger(CardSelectionPresentationTrigger trigger, bool persist);

	void SetMeleeApproachMovementEnabled(bool enabled, bool persist);

	void SetOfficialAttackTimingEnabled(bool enabled, bool persist);

	void SetCombatPresentationMode(CombatPresentationMode mode, bool persist);

	void SetDefeatCgOverlayEnabled(bool enabled, bool persist);

	void SetDefeatCgOverlayMode(DefeatCgOverlayMode mode, bool persist);

	void SetCharacterSelectBackgroundVariant(CharacterSelectBackgroundVariant variant, bool persist);

	void ResetCharacterToDefaults(bool persist);

	void ApplyBattleReadyTransform();

	void CleanupBattleReadyOverlay();

	void CleanupSkinEffectsRuntime();

	void CleanupSkinVisualEffectsRuntime();

	void CleanupSkinSfxRuntime();

	void StopVoice();

	void DebugLog(string message, int priority = 2);
}
