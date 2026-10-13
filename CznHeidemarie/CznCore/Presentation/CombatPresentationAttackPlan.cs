using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationAttackPlan(string ReadyAnimation, IReadOnlyList<string> HitAnimations, string EndAnimation, string LoopAnimation, float TimeScale = 1f, bool RepeatLastHitAnimation = false, string? CutInStateId = null, IReadOnlyList<string>? SuppressedNativeBodyTriggers = null, CombatPresentationHitResponsePolicy HitResponsePolicy = CombatPresentationHitResponsePolicy.Allow, CombatPresentationVoiceCue? VoiceCue = null, float OpeningHitDelaySeconds = 0f, CombatPresentationMeleeApproachPlan? MeleeApproach = null, bool UseFullHitAnimationWait = false, IReadOnlyList<CombatPresentationAnimationFallback>? AnimationFallbacks = null, CombatPresentationBodyHitCountPolicy BodyHitCountPolicy = CombatPresentationBodyHitCountPolicy.MatchDamageHitCount, string? CinematicPresentationId = null)
{
	public int ResolveBodyHitCount(CombatPresentationCardContext context)
	{
		int num = Math.Max(1, context.HitCount);
		if (BodyHitCountPolicy == CombatPresentationBodyHitCountPolicy.SingleBodyForAreaMultiHit && context.IsAreaTarget && num >= 2)
		{
			return 1;
		}
		return num;
	}

	public string GetHitAnimationName(int hitIndex)
	{
		if (HitAnimations.Count == 0)
		{
			return EndAnimation;
		}
		int num = Math.Max(0, hitIndex);
		if (RepeatLastHitAnimation && num >= HitAnimations.Count)
		{
			return HitAnimations[HitAnimations.Count - 1];
		}
		return HitAnimations[num % HitAnimations.Count];
	}

	public bool IsHitAnimation(string animationName)
	{
		foreach (string hitAnimation in HitAnimations)
		{
			if (string.Equals(hitAnimation, animationName, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public bool HasCinematicPresentation(string presentationId)
	{
		if (!string.IsNullOrEmpty(presentationId))
		{
			return string.Equals(CinematicPresentationId, presentationId, StringComparison.Ordinal);
		}
		return false;
	}

	public bool TryGetAnimationFallbacks(string animationName, out IReadOnlyList<string> fallbackAnimations)
	{
		if (AnimationFallbacks != null)
		{
			foreach (CombatPresentationAnimationFallback animationFallback in AnimationFallbacks)
			{
				if (string.Equals(animationFallback.AnimationName, animationName, StringComparison.Ordinal) && animationFallback.FallbackAnimations.Count > 0)
				{
					fallbackAnimations = animationFallback.FallbackAnimations;
					return true;
				}
			}
		}
		fallbackAnimations = Array.Empty<string>();
		return false;
	}
}
