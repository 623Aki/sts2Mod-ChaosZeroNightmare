using System;

namespace CznCore.Presentation;

internal static class CombatCutInPresentationPolicy
{
	public static CombatPresentationAttackPlan StripCutInPresentation(CombatPresentationAttackPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		if (string.IsNullOrWhiteSpace(plan.CutInStateId) && string.IsNullOrWhiteSpace(plan.CinematicPresentationId))
		{
			return plan;
		}
		return plan with
		{
			CutInStateId = null,
			CinematicPresentationId = null,
			VoiceCue = (string.IsNullOrWhiteSpace(plan.CutInStateId) ? plan.VoiceCue : null),
			OpeningHitDelaySeconds = (string.IsNullOrWhiteSpace(plan.CutInStateId) ? plan.OpeningHitDelaySeconds : 0f)
		};
	}

	public static CombatPresentationRoutePlan StripCutInPresentation(CombatPresentationRoutePlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		if (!string.IsNullOrWhiteSpace(plan.CutInStateId))
		{
			return plan with
			{
				CutInStateId = null
			};
		}
		return plan;
	}
}
