using Godot;

namespace CznCore.Presentation;

internal sealed record BattleReadyOverlayCoordinateCalibration(Vector2 LocalOriginCorrection, float ScaleMultiplier = 1f);
