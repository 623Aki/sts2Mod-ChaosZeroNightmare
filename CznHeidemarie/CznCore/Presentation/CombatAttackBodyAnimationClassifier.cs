using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CombatAttackBodyAnimationClassifier
{
	public static CombatAttackBodyAnimationRole Resolve(CombatPresentationAttackPlan? attackPlan, string animationName, string fallbackReadyAnimation, string fallbackEndAnimation, IReadOnlyList<string>? fallbackHitAnimations)
	{
		if ((attackPlan != null && string.Equals(attackPlan.ReadyAnimation, animationName, StringComparison.Ordinal)) || string.Equals(animationName, fallbackReadyAnimation, StringComparison.Ordinal))
		{
			return CombatAttackBodyAnimationRole.Ready;
		}
		if (((object)attackPlan != null && attackPlan.IsHitAnimation(animationName)) || IsFallbackHitAnimation(animationName, fallbackHitAnimations))
		{
			return CombatAttackBodyAnimationRole.Hit;
		}
		if ((attackPlan != null && string.Equals(attackPlan.EndAnimation, animationName, StringComparison.Ordinal)) || string.Equals(animationName, fallbackEndAnimation, StringComparison.Ordinal))
		{
			return CombatAttackBodyAnimationRole.End;
		}
		return CombatAttackBodyAnimationRole.Other;
	}

	private static bool IsFallbackHitAnimation(string animationName, IReadOnlyList<string>? fallbackHitAnimations)
	{
		if (fallbackHitAnimations == null)
		{
			return false;
		}
		foreach (string fallbackHitAnimation in fallbackHitAnimations)
		{
			if (string.Equals(animationName, fallbackHitAnimation, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
