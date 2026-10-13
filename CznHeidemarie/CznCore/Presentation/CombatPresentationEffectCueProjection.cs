using System;
using Godot;

namespace CznCore.Presentation;

internal static class CombatPresentationEffectCueProjection
{
	public static CombatPresentationEffectCue ProjectToScreenTarget(CombatPresentationEffectCue cue, Node2D targetAnchor, Node2D screenAnchor, CombatPresentationEffectAnchor projectedAnchor = CombatPresentationEffectAnchor.Screen)
	{
		Vector2 vector = screenAnchor.ToLocal(targetAnchor.GlobalPosition);
		if (cue.NeutralizeAnchorScale)
		{
			Vector2 globalScale = screenAnchor.GlobalScale;
			vector = new Vector2(vector.X * ((Math.Abs(globalScale.X) > 0.001f) ? globalScale.X : 1f), vector.Y * ((Math.Abs(globalScale.Y) > 0.001f) ? globalScale.Y : 1f));
		}
		return cue with
		{
			Anchor = projectedAnchor,
			OffsetX = cue.OffsetX + vector.X,
			OffsetY = cue.OffsetY + vector.Y
		};
	}
}
