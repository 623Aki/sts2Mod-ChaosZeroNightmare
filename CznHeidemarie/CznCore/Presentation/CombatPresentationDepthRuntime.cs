using System;

namespace CznCore.Presentation;

internal static class CombatPresentationDepthRuntime
{
	public const int CreatureIntentZIndex = -1;

	public const int RenderingServerFrontBaseZIndex = -3;

	public const int ScreenFrontZIndex = -3;

	public const int LiftedCreatureVisualsZIndex = -6;

	public const int ScreenBackZIndex = -12;

	public static int ResolveAttachedEffectZIndex(CombatPresentationEffectCue cue)
	{
		ArgumentNullException.ThrowIfNull(cue, "cue");
		if (IsScreenAnchor(cue.Anchor))
		{
			return 0;
		}
		return Math.Sign(cue.GodotZIndex ?? cue.ZIndex);
	}

	public static bool IsScreenAnchor(CombatPresentationEffectAnchor anchor)
	{
		switch (anchor)
		{
		case CombatPresentationEffectAnchor.Screen:
		case CombatPresentationEffectAnchor.ScreenBack:
		case CombatPresentationEffectAnchor.ScreenTargetBack:
		case CombatPresentationEffectAnchor.ScreenTargetSurfaceBack:
			return true;
		default:
			return false;
		}
	}
}
