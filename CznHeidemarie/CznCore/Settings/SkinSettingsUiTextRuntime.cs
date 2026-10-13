namespace CznCore.Settings;

internal sealed class SkinSettingsUiTextRuntime : ISkinSettingsUiText
{
	private readonly SkinSettingsUiTextProfile _profile;

	public string CznSettingsTab => _profile.CznSettingsTab.Current;

	public string GlobalSettingsHeader => _profile.GlobalSettingsHeader.Current;

	public string GlobalSettingsNavLabel => _profile.GlobalSettingsNavLabel.Current;

	public string GlobalSettingsNavDescription => _profile.GlobalSettingsNavDescription.Current;

	public string DebugLogEnabled => _profile.DebugLogEnabled.Current;

	public string GlobalEffectsEnabled => _profile.GlobalEffectsEnabled.Current;

	public string EffectsEnabledDescription => _profile.EffectsEnabledDescription.Current;

	public string GlobalParticlesEnabled => _profile.GlobalParticlesEnabled.Current;

	public string GlobalParticlesEnabledDescription => _profile.GlobalParticlesEnabledDescription.Current;

	public string GlobalCombatSfxEnabled => _profile.GlobalCombatSfxEnabled.Current;

	public string GlobalVoiceEnabled => _profile.GlobalVoiceEnabled.Current;

	public string MultiplayerSettingsHeader => _profile.MultiplayerSettingsHeader.Current;

	public string TeammatePresentationIntro => _profile.TeammatePresentationIntro.Current;

	public string TeammatePresentationPerformanceWarning => _profile.TeammatePresentationPerformanceWarning.Current;

	public string TeammatePresentationEnableAll => _profile.TeammatePresentationEnableAll.Current;

	public string TeammateVisualSettingsHeader => _profile.TeammateVisualSettingsHeader.Current;

	public string TeammateAudioSettingsHeader => _profile.TeammateAudioSettingsHeader.Current;

	public string TeammatePresentationStatusAll => _profile.TeammatePresentationStatusAll.Current;

	public string TeammatePresentationStatusPartial => _profile.TeammatePresentationStatusPartial.Current;

	public string TeammatePresentationStatusOriginal => _profile.TeammatePresentationStatusOriginal.Current;

	public string TeammatePresentationStatusGlobalLimits => _profile.TeammatePresentationStatusGlobalLimits.Current;

	public string TeammatePresentationEnabled => _profile.TeammatePresentationEnabled.Current;

	public string TeammatePresentationEnabledDescription => _profile.TeammatePresentationEnabledDescription.Current;

	public string TeammateEffectsEnabled => _profile.TeammateEffectsEnabled.Current;

	public string TeammateEffectsEnabledDescription => _profile.TeammateEffectsEnabledDescription.Current;

	public string TeammateCombatSfxEnabled => _profile.TeammateCombatSfxEnabled.Current;

	public string TeammateCombatSfxEnabledDescription => _profile.TeammateCombatSfxEnabledDescription.Current;

	public string TeammateVoiceEnabled => _profile.TeammateVoiceEnabled.Current;

	public string TeammateVoiceEnabledDescription => _profile.TeammateVoiceEnabledDescription.Current;

	public string TeammateVoiceCooldown => _profile.TeammateVoiceCooldown.Current;

	public string TeammateVoiceCooldownDescription => _profile.TeammateVoiceCooldownDescription.Current;

	public string TeammateCutInEnabled => _profile.TeammateCutInEnabled.Current;

	public string TeammateCutInEnabledDescription => _profile.TeammateCutInEnabledDescription.Current;

	public string TeammateAttackFullScreenPresentationMode => _profile.TeammateAttackFullScreenPresentationMode.Current;

	public string TeammateAttackFullScreenPresentationModeDescription => _profile.TeammateAttackFullScreenPresentationModeDescription.Current;

	public string GlobalCombatPresentationApply => _profile.GlobalCombatPresentationApply.Current;

	public string GlobalCombatPresentationApplyDescription => _profile.GlobalCombatPresentationApplyDescription.Current;

	public string GlobalCombatPresentationApplyFast => _profile.GlobalCombatPresentationApplyFast.Current;

	public string GlobalCombatPresentationApplyFull => _profile.GlobalCombatPresentationApplyFull.Current;

	public string CombatPresentationMode => _profile.CombatPresentationMode.Current;

	public string CombatPresentationModeDescription => _profile.CombatPresentationModeDescription.Current;

	public string CombatPresentationFast => _profile.CombatPresentationFast.Current;

	public string CombatPresentationFull => _profile.CombatPresentationFull.Current;

	public string CombatPresentationCustom => _profile.CombatPresentationCustom.Current;

	public string SwitchCombatPresentationFast => _profile.SwitchCombatPresentationFast.Current;

	public string SwitchCombatPresentationFull => _profile.SwitchCombatPresentationFull.Current;

	public string SwitchCombatPresentationCustom => _profile.SwitchCombatPresentationCustom.Current;

	public string VisualSettingsHeader => _profile.VisualSettingsHeader.Current;

	public string CombatEffectsHeader => _profile.CombatEffectsHeader.Current;

	public string ParticlesEnabled => _profile.ParticlesEnabled.Current;

	public string ParticlesEnabledDescription => _profile.ParticlesEnabledDescription.Current;

	public string CombatSfxEnabled => _profile.CombatSfxEnabled.Current;

	public string EffectBrightness => _profile.EffectBrightness.Current;

	public string EffectOpacity => _profile.EffectOpacity.Current;

	public string ShowDefeatCgOverlay => _profile.ShowDefeatCgOverlay.Current;

	public string ShowDefeatCgOverlayDescription => _profile.ShowDefeatCgOverlayDescription.Current;

	public string DefeatCgOverlayOff => _profile.DefeatCgOverlayOff.Current;

	public string DefeatCgOverlayType1 => _profile.DefeatCgOverlayType1.Current;

	public string DefeatCgOverlayType2 => _profile.DefeatCgOverlayType2.Current;

	public string DefeatCgOverlayRandom => _profile.DefeatCgOverlayRandom.Current;

	public string CharacterSelectBackground => _profile.CharacterSelectBackground.Current;

	public string CharacterSelectBackgroundDescription => _profile.CharacterSelectBackgroundDescription.Current;

	public string CharacterSelectBackgroundStaticPortrait => _profile.CharacterSelectBackgroundStaticPortrait.Current;

	public string CharacterSelectBackgroundTeamJoinPortrait => _profile.CharacterSelectBackgroundTeamJoinPortrait.Current;

	public string CharacterSelectBackgroundLiveLobby => _profile.CharacterSelectBackgroundLiveLobby.Current;

	public string CardAnimationHeader => _profile.CardAnimationHeader.Current;

	public string LeftSideCardPresentationHeader => _profile.LeftSideCardPresentationHeader.Current;

	public string ShowBattleReadyOverlay => _profile.ShowBattleReadyOverlay.Current;

	public string ShowBattleReadyOverlayDescription => _profile.ShowBattleReadyOverlayDescription.Current;

	public string BattleReadyOverlayOff => _profile.BattleReadyOverlayOff.Current;

	public string BattleReadyOverlayAllSupportedCards => _profile.BattleReadyOverlayAllSupportedCards.Current;

	public string BattleReadyOverlayCutInCardsOnly => _profile.BattleReadyOverlayCutInCardsOnly.Current;

	public string ShowCardPlayPresentation => _profile.ShowCardPlayPresentation.Current;

	public string ShowCardPlayPresentationDescription => _profile.ShowCardPlayPresentationDescription.Current;

	public string AttackFullScreenPresentationMode => _profile.AttackFullScreenPresentationMode.Current;

	public string AttackFullScreenPresentationModeDescription => _profile.AttackFullScreenPresentationModeDescription.Current;

	public string AttackFullScreenPresentationAlways => _profile.AttackFullScreenPresentationAlways.Current;

	public string AttackFullScreenPresentationLethalOnly => _profile.AttackFullScreenPresentationLethalOnly.Current;

	public string CardSelectionPresentationTrigger => _profile.CardSelectionPresentationTrigger.Current;

	public string CardSelectionPresentationTriggerDescription => _profile.CardSelectionPresentationTriggerDescription.Current;

	public string CardSelectionPresentationTriggerHover => _profile.CardSelectionPresentationTriggerHover.Current;

	public string CardSelectionPresentationTriggerSelection => _profile.CardSelectionPresentationTriggerSelection.Current;

	public string MeleeApproachMovement => _profile.MeleeApproachMovement.Current;

	public string MeleeApproachMovementDescription => _profile.MeleeApproachMovementDescription.Current;

	public string OfficialAttackTiming => _profile.OfficialAttackTiming.Current;

	public string OfficialAttackTimingDescription => _profile.OfficialAttackTimingDescription.Current;

	public string BattleReadyScale => _profile.BattleReadyScale.Current;

	public string BattleReadyOpacity => _profile.BattleReadyOpacity.Current;

	public string BattleReadyOffsetX => _profile.BattleReadyOffsetX.Current;

	public string BattleReadyOffsetY => _profile.BattleReadyOffsetY.Current;

	public string ResetBattleReadyPosition => _profile.ResetBattleReadyPosition.Current;

	public string ResetBattleReadyPositionDescription => _profile.ResetBattleReadyPositionDescription.Current;

	public string AudioSettingsHeader => _profile.AudioSettingsHeader.Current;

	public string EffectVolume => _profile.EffectVolume.Current;

	public string VoiceVolume => _profile.VoiceVolume.Current;

	public string VoiceCooldown => _profile.VoiceCooldown.Current;

	public string VoiceCooldownDescription => _profile.VoiceCooldownDescription.Current;

	public string CollapseSection => _profile.CollapseSection.Current;

	public string ExpandSection => _profile.ExpandSection.Current;

	public string ResetDefaults => _profile.ResetDefaults.Current;

	public string DebugLogEnabledDescription => _profile.DebugLogEnabledDescription.Current;

	public SkinSettingsUiTextRuntime(SkinSettingsUiTextProfile profile)
	{
		_profile = profile;
	}
}
