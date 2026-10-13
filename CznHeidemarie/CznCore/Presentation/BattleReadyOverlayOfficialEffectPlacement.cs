using Godot;

namespace CznCore.Presentation;

internal sealed record BattleReadyOverlayOfficialEffectPlacement(Vector2 StandbyPosition, Vector2 Offset, float Scale, Vector2 PrimitiveOffset, float PrimitiveOffsetScale = 0f, float VisualScaleMultiplier = 1f)
{
	public BattleReadyOverlayIdleEffectPlan ToScene(string scenePath, BattleReadyOverlayCoordinateCalibration calibration)
	{
		Vector2 vector = new Vector2(0f - PrimitiveOffset.X, PrimitiveOffset.Y);
		Vector2 vector2 = Offset + StandbyPosition + calibration.LocalOriginCorrection + vector * PrimitiveOffsetScale;
		return BattleReadyOverlayIdleEffectPlan.Scene(scenePath, vector2.X, vector2.Y, Scale * VisualScaleMultiplier * calibration.ScaleMultiplier);
	}
}
