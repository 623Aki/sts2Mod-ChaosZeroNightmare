namespace CznCore.Settings;

internal interface ISkinSettingsUiText
{
	string CznSettingsTab { get; }

	string GlobalSettingsHeader { get; }

	string GlobalSettingsNavLabel { get; }

	string GlobalSettingsNavDescription { get; }

	string DebugLogEnabled { get; }

	string GlobalEffectsEnabled { get; }

	string EffectsEnabledDescription { get; }

	string GlobalParticlesEnabled { get; }

	string GlobalParticlesEnabledDescription { get; }

	string GlobalCombatSfxEnabled { get; }

	string GlobalVoiceEnabled { get; }

	string MultiplayerSettingsHeader { get; }

	string TeammatePresentationIntro { get; }

	string TeammatePresentationPerformanceWarning { get; }

	string TeammatePresentationEnableAll { get; }

	string TeammateVisualSettingsHeader { get; }

	string TeammateAudioSettingsHeader { get; }

	string TeammatePresentationStatusAll { get; }

	string TeammatePresentationStatusPartial { get; }

	string TeammatePresentationStatusOriginal { get; }

	string TeammatePresentationStatusGlobalLimits { get; }

	string TeammatePresentationEnabled { get; }

	string TeammatePresentationEnabledDescription { get; }

	string TeammateEffectsEnabled { get; }

	string TeammateEffectsEnabledDescription { get; }

	string TeammateCombatSfxEnabled { get; }

	string TeammateCombatSfxEnabledDescription { get; }

	string TeammateVoiceEnabled { get; }

	string TeammateVoiceEnabledDescription { get; }

	string TeammateVoiceCooldown { get; }

	string TeammateVoiceCooldownDescription { get; }

	string TeammateCutInEnabled { get; }

	string TeammateCutInEnabledDescription { get; }

	string TeammateAttackFullScreenPresentationMode { get; }

	string TeammateAttackFullScreenPresentationModeDescription { get; }

	string GlobalCombatPresentationApply { get; }

	string GlobalCombatPresentationApplyDescription { get; }

	string GlobalCombatPresentationApplyFast { get; }

	string GlobalCombatPresentationApplyFull { get; }

	string CombatPresentationMode { get; }

	string CombatPresentationModeDescription { get; }

	string CombatPresentationFast { get; }

	string CombatPresentationFull { get; }

	string CombatPresentationCustom { get; }

	string VisualSettingsHeader { get; }

	string CombatEffectsHeader { get; }

	string ParticlesEnabled { get; }

	string ParticlesEnabledDescription { get; }

	string CombatSfxEnabled { get; }

	string EffectBrightness { get; }

	string EffectOpacity { get; }

	string ShowDefeatCgOverlay { get; }

	string ShowDefeatCgOverlayDescription { get; }

	string DefeatCgOverlayOff { get; }

	string DefeatCgOverlayType1 { get; }

	string DefeatCgOverlayType2 { get; }

	string DefeatCgOverlayRandom { get; }

	string CharacterSelectBackground { get; }

	string CharacterSelectBackgroundDescription { get; }

	string CharacterSelectBackgroundStaticPortrait { get; }

	string CharacterSelectBackgroundTeamJoinPortrait { get; }

	string CharacterSelectBackgroundLiveLobby { get; }

	string CardAnimationHeader { get; }

	string LeftSideCardPresentationHeader { get; }

	string ShowBattleReadyOverlay { get; }

	string ShowBattleReadyOverlayDescription { get; }

	string BattleReadyOverlayOff { get; }

	string BattleReadyOverlayAllSupportedCards { get; }

	string BattleReadyOverlayCutInCardsOnly { get; }

	string ShowCardPlayPresentation { get; }

	string ShowCardPlayPresentationDescription { get; }

	string AttackFullScreenPresentationMode { get; }

	string AttackFullScreenPresentationModeDescription { get; }

	string AttackFullScreenPresentationAlways { get; }

	string AttackFullScreenPresentationLethalOnly { get; }

	string CardSelectionPresentationTrigger { get; }

	string CardSelectionPresentationTriggerDescription { get; }

	string CardSelectionPresentationTriggerHover { get; }

	string CardSelectionPresentationTriggerSelection { get; }

	string MeleeApproachMovement { get; }

	string MeleeApproachMovementDescription { get; }

	string OfficialAttackTiming { get; }

	string OfficialAttackTimingDescription { get; }

	string BattleReadyScale { get; }

	string BattleReadyOpacity { get; }

	string BattleReadyOffsetX { get; }

	string BattleReadyOffsetY { get; }

	string ResetBattleReadyPosition { get; }

	string ResetBattleReadyPositionDescription { get; }

	string AudioSettingsHeader { get; }

	string EffectVolume { get; }

	string VoiceVolume { get; }

	string VoiceCooldown { get; }

	string VoiceCooldownDescription { get; }

	string CollapseSection { get; }

	string ExpandSection { get; }

	string ResetDefaults { get; }

	string DebugLogEnabledDescription { get; }
}
