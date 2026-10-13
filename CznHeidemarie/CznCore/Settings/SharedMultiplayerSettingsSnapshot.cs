using CznCore.Presentation;

namespace CznCore.Settings;

internal readonly record struct SharedMultiplayerSettingsSnapshot(bool PresentationEnabled, bool EffectsEnabled, bool CombatSfxEnabled, bool VoiceEnabled, float VoiceCooldownSeconds, bool CutInEnabled, AttackFullScreenPresentationMode AttackFullScreenPresentationMode)
{
	public static SharedMultiplayerSettingsSnapshot Defaults { get; } = new SharedMultiplayerSettingsSnapshot(PresentationEnabled: true, EffectsEnabled: true, CombatSfxEnabled: true, VoiceEnabled: true, 0f, CutInEnabled: true, AttackFullScreenPresentationMode.LethalOnly);
}
