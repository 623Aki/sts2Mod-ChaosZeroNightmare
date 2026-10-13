using System;

namespace CznCore.Presentation;

internal static class CombatPresentationSupportRoutePolicy
{
	public static bool IsSharedSupportRoute(CombatPresentationCardRoute route)
	{
		if (route == CombatPresentationCardRoute.GeneratedCard || (uint)(route - 5) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static bool ShouldRouteIdleRestoreThroughReadyLoop(CombatPresentationRoutePlan plan, CharacterSkinCombatBodyProfile bodyProfile, string? loopAnimation = null, string? sourceAnimation = null)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		ArgumentNullException.ThrowIfNull(bodyProfile, "bodyProfile");
		return ShouldRouteIdleLoopThroughReadyLoop(string.IsNullOrEmpty(loopAnimation) ? plan.LoopAnimation : loopAnimation, bodyProfile, sourceAnimation);
	}

	public static bool ShouldRouteIdleLoopThroughReadyLoop(string? loopAnimation, CharacterSkinCombatBodyProfile bodyProfile, string? sourceAnimation = null)
	{
		ArgumentNullException.ThrowIfNull(bodyProfile, "bodyProfile");
		if (string.Equals(loopAnimation, bodyProfile.IdleAnimation, StringComparison.Ordinal))
		{
			return !IsOfficialDirectIdleLoopSource(sourceAnimation, bodyProfile);
		}
		return false;
	}

	private static bool IsOfficialDirectIdleLoopSource(string? sourceAnimation, CharacterSkinCombatBodyProfile bodyProfile)
	{
		if (!string.Equals(sourceAnimation, bodyProfile.ReadyExitAnimation, StringComparison.Ordinal) && (sourceAnimation == null || !sourceAnimation.StartsWith("enter_", StringComparison.Ordinal)) && (sourceAnimation == null || !sourceAnimation.StartsWith("victory", StringComparison.Ordinal)))
		{
			return sourceAnimation?.StartsWith("death", StringComparison.Ordinal) ?? false;
		}
		return true;
	}
}
