using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CombatNormalAttackVariantRuntime
{
	public const string DefaultNormalAttackA = "attack_play1";

	public const string DefaultNormalAttackB = "attack_play2";

	public static CombatPresentationAttackPlan ResolveDefaultSingleHitAttackPlan(CombatPresentationCardContext context, CombatPresentationAttackPlan attackPlan, CombatNormalAttackVariantState state, object? room, string firstHitAnimation = "attack_play1", string secondHitAnimation = "attack_play2")
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (!IsDefaultSingleLightHitAttack(context, attackPlan, firstHitAnimation, secondHitAnimation))
		{
			return attackPlan;
		}
		return attackPlan with
		{
			HitAnimations = new _003C_003Ez__ReadOnlySingleElementList<string>(ResolveNextHitAnimation(state, room, firstHitAnimation, secondHitAnimation))
		};
	}

	public static CombatPresentationRoutePlan ResolveDefaultSingleHitRoutePlan(CombatPresentationCardContext context, CombatPresentationRoutePlan routePlan, CombatNormalAttackVariantState state, object? room, CombatPresentationRoutePlan firstPlan, CombatPresentationRoutePlan secondPlan, string firstHitAnimation = "attack_play1", string secondHitAnimation = "attack_play2")
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (!IsDefaultSingleLightHitRoute(context, routePlan, firstHitAnimation, secondHitAnimation))
		{
			return routePlan;
		}
		if (!IsNextFirstVariant(state, room))
		{
			return secondPlan;
		}
		return firstPlan;
	}

	private static bool IsDefaultSingleLightHitAttack(CombatPresentationCardContext context, CombatPresentationAttackPlan attackPlan, string firstHitAnimation, string secondHitAnimation)
	{
		if (IsSingleHitAttackContext(context) && attackPlan.HitAnimations.Count == 1)
		{
			return IsDefaultLightHitAnimation(attackPlan.HitAnimations[0], firstHitAnimation, secondHitAnimation);
		}
		return false;
	}

	private static bool IsDefaultSingleLightHitRoute(CombatPresentationCardContext context, CombatPresentationRoutePlan routePlan, string firstHitAnimation, string secondHitAnimation)
	{
		if (IsSingleHitAttackContext(context))
		{
			return CountDefaultLightHitSteps(routePlan.BodySteps, firstHitAnimation, secondHitAnimation) == 1;
		}
		return false;
	}

	private static bool IsSingleHitAttackContext(CombatPresentationCardContext context)
	{
		if (context.Route == CombatPresentationCardRoute.Attack && !context.IsAreaTarget)
		{
			return Math.Max(1, context.HitCount) == 1;
		}
		return false;
	}

	private static int CountDefaultLightHitSteps(IReadOnlyList<CombatPresentationBodyStep>? bodySteps, string firstHitAnimation, string secondHitAnimation)
	{
		if (bodySteps == null)
		{
			return 0;
		}
		int num = 0;
		foreach (CombatPresentationBodyStep bodyStep in bodySteps)
		{
			if (IsDefaultLightHitAnimation(bodyStep.BodyAnimation, firstHitAnimation, secondHitAnimation))
			{
				num++;
			}
		}
		return num;
	}

	private static bool IsNextFirstVariant(CombatNormalAttackVariantState state, object? room)
	{
		return (state.NextIndex(room) & 1) == 0;
	}

	private static string ResolveNextHitAnimation(CombatNormalAttackVariantState state, object? room, string firstHitAnimation, string secondHitAnimation)
	{
		if (!IsNextFirstVariant(state, room))
		{
			return secondHitAnimation;
		}
		return firstHitAnimation;
	}

	private static bool IsDefaultLightHitAnimation(string animationName, string firstHitAnimation, string secondHitAnimation)
	{
		if (!string.Equals(animationName, firstHitAnimation, StringComparison.Ordinal))
		{
			return string.Equals(animationName, secondHitAnimation, StringComparison.Ordinal);
		}
		return true;
	}
}
