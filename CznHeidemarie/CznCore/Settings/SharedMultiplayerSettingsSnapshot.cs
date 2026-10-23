using CznCore.Presentation;

namespace CznCore.Settings;

/// <summary>
/// 「队友演出」设置块的内存快照。
///
/// <para><c>UseHostSettings</c>：联机时是否以房主的设置为标准（默认开启）。
/// 关闭后本机完全按自己的设置走，房主下发的内容会被忽略。</para>
/// </summary>
internal readonly record struct SharedMultiplayerSettingsSnapshot(bool PresentationEnabled, bool EffectsEnabled, bool CombatSfxEnabled, bool VoiceEnabled, float VoiceCooldownSeconds, bool CutInEnabled, AttackFullScreenPresentationMode AttackFullScreenPresentationMode, bool UseHostSettings)
{
	public static SharedMultiplayerSettingsSnapshot Defaults { get; } = new SharedMultiplayerSettingsSnapshot(PresentationEnabled: true, EffectsEnabled: true, CombatSfxEnabled: true, VoiceEnabled: true, 0f, CutInEnabled: true, AttackFullScreenPresentationMode.LethalOnly, UseHostSettings: true);
}
