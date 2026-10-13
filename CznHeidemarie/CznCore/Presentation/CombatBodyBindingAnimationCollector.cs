using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CombatBodyBindingAnimationCollector
{
	public static IReadOnlyList<string> Collect(IEnumerable<CombatPresentationState>? states = null, IEnumerable<CombatPresentationRoutePlan>? routePlans = null, IEnumerable<CombatPresentationAttackPlan>? attackPlans = null, IEnumerable<string>? extraAnimations = null)
	{
		List<string> list = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		if (states != null)
		{
			foreach (CombatPresentationState state in states)
			{
				AddBodySteps(list, seen, state.BodySteps);
				AddAnimation(list, seen, state.LoopAnimation);
			}
		}
		if (routePlans != null)
		{
			foreach (CombatPresentationRoutePlan routePlan in routePlans)
			{
				AddBodySteps(list, seen, routePlan.BodySteps);
				AddAnimation(list, seen, routePlan.LoopAnimation);
			}
		}
		if (attackPlans != null)
		{
			foreach (CombatPresentationAttackPlan attackPlan in attackPlans)
			{
				AddAnimation(list, seen, attackPlan.ReadyAnimation);
				AddAnimations(list, seen, attackPlan.HitAnimations);
				AddAnimation(list, seen, attackPlan.EndAnimation);
				AddAnimation(list, seen, attackPlan.LoopAnimation);
				if (attackPlan.AnimationFallbacks == null)
				{
					continue;
				}
				foreach (CombatPresentationAnimationFallback animationFallback in attackPlan.AnimationFallbacks)
				{
					AddAnimation(list, seen, animationFallback.AnimationName);
					AddAnimations(list, seen, animationFallback.FallbackAnimations);
				}
			}
		}
		AddAnimations(list, seen, extraAnimations);
		return list;
	}

	private static void AddBodySteps(List<string> animations, HashSet<string> seen, IReadOnlyList<CombatPresentationBodyStep>? bodySteps)
	{
		if (bodySteps == null)
		{
			return;
		}
		foreach (CombatPresentationBodyStep bodyStep in bodySteps)
		{
			AddAnimation(animations, seen, bodyStep.BodyAnimation);
		}
	}

	private static void AddAnimations(List<string> animations, HashSet<string> seen, IEnumerable<string>? values)
	{
		if (values == null)
		{
			return;
		}
		foreach (string value in values)
		{
			AddAnimation(animations, seen, value);
		}
	}

	private static void AddAnimation(List<string> animations, HashSet<string> seen, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
		{
			animations.Add(value);
		}
	}
}
