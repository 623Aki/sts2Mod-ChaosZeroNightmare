using CznCore.Presentation;

namespace CznCore.Settings;

internal interface ISkinSettingsGlobalUiRuntime
{
	bool DebugLogEnabled { get; }

	bool GlobalEffectsEnabled { get; }

	bool GlobalParticlesEnabled { get; }

	bool GlobalCombatSfxEnabled { get; }

	bool GlobalVoiceEnabled { get; }

	bool TeammatePresentationEnabled { get; }

	bool TeammateEffectsEnabled { get; }

	bool TeammateCombatSfxEnabled { get; }

	bool TeammateVoiceEnabled { get; }

	float TeammateVoiceCooldownSeconds { get; }

	bool TeammateCutInEnabled { get; }

	AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode { get; }

	void SetDebugLogEnabled(bool value, bool persist);

	void SetGlobalEffectsEnabled(bool value, bool persist);

	void SetGlobalParticlesEnabled(bool value, bool persist);

	void SetGlobalCombatSfxEnabled(bool value, bool persist);

	void SetGlobalVoiceEnabled(bool value, bool persist);

	void SetTeammatePresentationEnabled(bool value, bool persist);

	void SetTeammateEffectsEnabled(bool value, bool persist);

	void SetTeammateCombatSfxEnabled(bool value, bool persist);

	void SetTeammateVoiceEnabled(bool value, bool persist);

	void SetTeammateVoiceCooldownSeconds(float value, bool persist);

	void SetTeammateCutInEnabled(bool value, bool persist);

	void SetTeammateAttackFullScreenPresentationMode(AttackFullScreenPresentationMode value, bool persist);

	void EnableAllTeammatePresentationChannels(bool persist);

	void ApplyCombatPresentationModeToLoadedCharacters(CombatPresentationMode mode, bool persist);

	void ResetGlobalToDefaults(bool persist);

	void ResetTeammatePresentationToDefaults(bool persist);
}
