namespace CznCore.Presentation;

internal interface ICombatPresentationAudienceSettings
{
	bool TeammatePresentationEnabled { get; }

	bool TeammateEffectsEnabled { get; }

	bool TeammateCombatSfxEnabled { get; }

	bool TeammateVoiceEnabled { get; }

	float TeammateVoiceCooldownSeconds { get; }

	bool TeammateCutInEnabled { get; }

	AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode { get; }

	bool LocalCutInEnabled { get; }
}
