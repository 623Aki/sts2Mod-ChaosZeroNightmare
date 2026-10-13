using System;

namespace CznCore.Settings;

internal sealed record SkinSettingsUiRuntimeHooks(Action? ApplyEffectBrightnessSettings = null, Action? ApplyBattleReadyTransform = null, Action? CleanupBattleReadyOverlay = null, Action? CleanupSkinEffectsRuntime = null, Action? CleanupSkinVisualEffectsRuntime = null, Action? CleanupSkinSfxRuntime = null, Action? StopVoice = null, Action<bool>? ApplyCardPlayPresentationEnabled = null, Action<string, int>? DebugLog = null);
