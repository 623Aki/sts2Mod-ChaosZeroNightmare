namespace CznCore.Presentation.Vfx;

internal readonly record struct OverlayStageProfileSnapshot(bool HasActiveOverlay, bool HasParkedOverlay, int ActiveSpines, int ParkedSpines, int ActiveViewports, int ParkedViewports, long ManagedMiB, ulong NativeMiB);
