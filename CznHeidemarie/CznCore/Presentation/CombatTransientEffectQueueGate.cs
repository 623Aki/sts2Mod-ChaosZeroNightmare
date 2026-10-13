using System;

namespace CznCore.Presentation;

internal static class CombatTransientEffectQueueGate
{
	public static CombatTransientEffectQueueBudgetDecision EvaluateActiveBudget(CombatPresentationEffectCue cue, int activePlaybackCount, CombatTransientEffectQueueBudgetProfile profile)
	{
		if (cue.BypassActiveBudget)
		{
			return new CombatTransientEffectQueueBudgetDecision(CanQueue: true, int.MaxValue, HighPriority: true, "bypass_active_budget");
		}
		if (cue.ClearExisting)
		{
			return new CombatTransientEffectQueueBudgetDecision(CanQueue: true, int.MaxValue, HighPriority: false, "clear_existing");
		}
		bool highPriority = IsHighPriority(cue, profile);
		int num = ResolveActiveBudget(cue, highPriority, profile);
		return new CombatTransientEffectQueueBudgetDecision(Math.Max(0, activePlaybackCount) < num, num, highPriority, ResolveLodLabel(cue, highPriority));
	}

	public static bool CanQueueMaxActiveInstances(CombatPresentationEffectCue cue, int activeCount)
	{
		if (cue.MaxActiveInstances > 0)
		{
			return Math.Max(0, activeCount) < cue.MaxActiveInstances;
		}
		return true;
	}

	public static bool IsHighPriority(CombatPresentationEffectCue cue, CombatTransientEffectQueueBudgetProfile profile)
	{
		if (cue.PerformanceImportance == CombatPresentationEffectImportance.Optional)
		{
			return false;
		}
		bool flag = cue.PerformanceImportance == CombatPresentationEffectImportance.Critical || cue.MaxActiveInstances > 0 || cue.Anchor == CombatPresentationEffectAnchor.Screen || cue.Anchor == CombatPresentationEffectAnchor.ScreenBack;
		if (!flag)
		{
			bool flag2 = profile.TreatScreenTargetBackAsHighPriority;
			if (flag2)
			{
				CombatPresentationEffectAnchor anchor = cue.Anchor;
				bool flag3 = ((anchor == CombatPresentationEffectAnchor.ScreenTargetBack || anchor == CombatPresentationEffectAnchor.ScreenTargetSurfaceBack) ? true : false);
				flag2 = flag3;
			}
			flag = flag2;
		}
		return flag;
	}

	public static int ResolveActiveBudget(CombatPresentationEffectCue cue, bool highPriority, CombatTransientEffectQueueBudgetProfile profile)
	{
		int num = Math.Max(1, profile.SoftBudget);
		int result = Math.Max(num, profile.HardBudget);
		if (cue.PerformanceImportance == CombatPresentationEffectImportance.Optional)
		{
			return Math.Max(1, num - Math.Max(0, profile.OptionalBudgetReserve));
		}
		if (!highPriority)
		{
			return num;
		}
		return result;
	}

	public static string ResolveLodLabel(CombatPresentationEffectCue cue, bool highPriority)
	{
		if (cue.PerformanceImportance == CombatPresentationEffectImportance.Optional)
		{
			return "drop_optional";
		}
		if (!highPriority)
		{
			return "drop_secondary";
		}
		return "hard_cap";
	}
}
