using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatAttackPlanAvailabilityRuntime<TCreatureNode> where TCreatureNode : class
{
	public bool TryResolvePlayablePlan(TCreatureNode? creatureNode, CombatPresentationAttackPlan attackPlan, Func<TCreatureNode?, bool> isUsableCreatureNode, Func<TCreatureNode, string?, bool> hasBodyAnimation, string idleAnimation, Action<string, int>? debug, out CombatPresentationAttackPlan playablePlan)
	{
		ArgumentNullException.ThrowIfNull(isUsableCreatureNode, "isUsableCreatureNode");
		ArgumentNullException.ThrowIfNull(hasBodyAnimation, "hasBodyAnimation");
		playablePlan = attackPlan;
		if (!isUsableCreatureNode(creatureNode))
		{
			return true;
		}
		string resolvedAnimation = attackPlan.ReadyAnimation;
		if (!hasBodyAnimation(creatureNode, resolvedAnimation) && !TryResolvePhaseFallback(creatureNode, attackPlan, resolvedAnimation, hasBodyAnimation, idleAnimation, out resolvedAnimation))
		{
			debug?.Invoke("Skipped custom attack body because ready animation is unavailable: " + attackPlan.ReadyAnimation, 2);
			return false;
		}
		List<string> list = new List<string>(attackPlan.HitAnimations.Count);
		foreach (string hitAnimation in attackPlan.HitAnimations)
		{
			string resolvedAnimation2;
			if (hasBodyAnimation(creatureNode, hitAnimation))
			{
				list.Add(hitAnimation);
			}
			else if (TryResolveExplicitFallback(creatureNode, attackPlan, hitAnimation, hasBodyAnimation, out resolvedAnimation2))
			{
				list.Add(resolvedAnimation2);
			}
		}
		if (list.Count == 0)
		{
			debug?.Invoke("Skipped custom attack body because no hit animations are available.", 2);
			return false;
		}
		string resolvedAnimation3 = attackPlan.EndAnimation;
		if (!hasBodyAnimation(creatureNode, resolvedAnimation3) && !TryResolvePhaseFallback(creatureNode, attackPlan, resolvedAnimation3, hasBodyAnimation, idleAnimation, out resolvedAnimation3))
		{
			debug?.Invoke("Skipped custom attack body because end animation is unavailable: " + attackPlan.EndAnimation, 2);
			return false;
		}
		if (!string.Equals(resolvedAnimation, attackPlan.ReadyAnimation, StringComparison.Ordinal) || list.Count != attackPlan.HitAnimations.Count || !string.Equals(resolvedAnimation3, attackPlan.EndAnimation, StringComparison.Ordinal))
		{
			playablePlan = attackPlan with
			{
				ReadyAnimation = resolvedAnimation,
				HitAnimations = list.ToArray(),
				EndAnimation = resolvedAnimation3
			};
			debug?.Invoke("Resolved playable attack plan ready=" + resolvedAnimation + " hits=" + string.Join(",", list) + " end=" + resolvedAnimation3, 2);
		}
		return true;
	}

	private static bool TryResolvePhaseFallback(TCreatureNode creatureNode, CombatPresentationAttackPlan attackPlan, string animationName, Func<TCreatureNode, string?, bool> hasBodyAnimation, string idleAnimation, out string resolvedAnimation)
	{
		if (TryResolveExplicitFallback(creatureNode, attackPlan, animationName, hasBodyAnimation, out resolvedAnimation))
		{
			return true;
		}
		if (hasBodyAnimation(creatureNode, attackPlan.LoopAnimation))
		{
			resolvedAnimation = attackPlan.LoopAnimation;
			return true;
		}
		if (hasBodyAnimation(creatureNode, idleAnimation))
		{
			resolvedAnimation = idleAnimation;
			return true;
		}
		resolvedAnimation = animationName;
		return false;
	}

	private static bool TryResolveExplicitFallback(TCreatureNode creatureNode, CombatPresentationAttackPlan attackPlan, string animationName, Func<TCreatureNode, string?, bool> hasBodyAnimation, out string resolvedAnimation)
	{
		if (attackPlan.TryGetAnimationFallbacks(animationName, out IReadOnlyList<string> fallbackAnimations))
		{
			foreach (string item in fallbackAnimations)
			{
				if (hasBodyAnimation(creatureNode, item))
				{
					resolvedAnimation = item;
					return true;
				}
			}
		}
		resolvedAnimation = animationName;
		return false;
	}
}
