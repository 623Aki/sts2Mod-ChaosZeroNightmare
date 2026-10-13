using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatActorRuntimeState<TCard, TAttackSession> where TCard : class where TAttackSession : class
{
	public int AnimationToken { get; private set; }

	public TCard? HoverPresentationCard { get; private set; }

	public bool HoverPresentationActive { get; private set; }

	public TAttackSession? ActiveAttackSession { get; set; }

	public CombatNormalAttackVariantState NormalAttackVariant { get; } = new CombatNormalAttackVariantState();

	public int HitResponseGuardToken { get; private set; }

	public CombatPresentationHitResponsePolicy HitResponsePolicy { get; private set; }

	public string? HitResponseGuardReason { get; private set; }

	public int NativeBodyTriggerSuppressionToken { get; private set; }

	public IReadOnlyList<string>? SuppressedNativeBodyTriggers { get; private set; }

	public string? NativeBodyTriggerSuppressionReason { get; private set; }

	public int NextAnimationToken()
	{
		ClearHitResponseGuard();
		ClearNativeBodyTriggerSuppression();
		return ++AnimationToken;
	}

	public bool IsAnimationTokenCurrent(int token)
	{
		return token == AnimationToken;
	}

	public void ResetPresentationState()
	{
		AnimationToken++;
		ClearHoverPresentation();
		ActiveAttackSession = null;
		ClearHitResponseGuard();
		ClearNativeBodyTriggerSuppression();
	}

	public void ClearTransientState()
	{
		ClearHoverPresentation();
		ActiveAttackSession = null;
	}

	public bool TryBeginHoverPresentation(TCard card)
	{
		if (HoverPresentationActive && card == HoverPresentationCard)
		{
			return false;
		}
		HoverPresentationActive = true;
		HoverPresentationCard = card;
		return true;
	}

	public void EndHoverPresentation(TCard? card)
	{
		if (card == HoverPresentationCard)
		{
			ClearHoverPresentation();
		}
	}

	public void ClearHoverPresentation()
	{
		HoverPresentationActive = false;
		HoverPresentationCard = null;
	}

	public void BeginHitResponseGuard(int token, CombatPresentationHitResponsePolicy policy, string? reason)
	{
		HitResponseGuardToken = token;
		HitResponsePolicy = policy;
		HitResponseGuardReason = (string.IsNullOrEmpty(reason) ? policy.ToString() : reason);
	}

	public void ClearHitResponseGuard()
	{
		HitResponseGuardToken = 0;
		HitResponsePolicy = CombatPresentationHitResponsePolicy.Allow;
		HitResponseGuardReason = null;
	}

	public void ClearHitResponseGuard(int token)
	{
		if (HitResponseGuardToken == token)
		{
			ClearHitResponseGuard();
		}
	}

	public bool HasHitResponseGuard(int token)
	{
		if (HitResponseGuardToken == token && HitResponsePolicy != CombatPresentationHitResponsePolicy.Allow)
		{
			return IsAnimationTokenCurrent(token);
		}
		return false;
	}

	public bool BeginNativeBodyTriggerSuppression(IReadOnlyList<string>? suppressedTriggers, int token, string? reason)
	{
		if (suppressedTriggers == null || suppressedTriggers.Count == 0)
		{
			return false;
		}
		NativeBodyTriggerSuppressionToken = token;
		SuppressedNativeBodyTriggers = suppressedTriggers;
		NativeBodyTriggerSuppressionReason = reason;
		return true;
	}

	public void ClearNativeBodyTriggerSuppression()
	{
		NativeBodyTriggerSuppressionToken = 0;
		SuppressedNativeBodyTriggers = null;
		NativeBodyTriggerSuppressionReason = null;
	}

	public bool IsNativeBodyTriggerSuppressed(string triggerName)
	{
		if (NativeBodyTriggerSuppressionToken <= 0 || NativeBodyTriggerSuppressionToken != AnimationToken || SuppressedNativeBodyTriggers == null)
		{
			return false;
		}
		foreach (string suppressedNativeBodyTrigger in SuppressedNativeBodyTriggers)
		{
			if (string.Equals(suppressedNativeBodyTrigger, triggerName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
