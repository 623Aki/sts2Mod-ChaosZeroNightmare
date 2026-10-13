using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatRoutePlaybackRuntime<TActor> where TActor : class
{
	private const float FallbackGuardReleaseDelaySeconds = 0.25f;

	private readonly ICombatPresentationProfile _profile;

	private readonly CombatBodySequenceRuntime _bodySequences;

	private readonly CombatGuardedScheduler<TActor> _scheduler;

	private readonly Func<TActor?, int> _getAnimationToken;

	private readonly Action<TActor?, CombatPresentationRoutePlan, int> _beginNativeBodyTriggerSuppression;

	private readonly Action<TActor?, int> _clearNativeBodyTriggerSuppression;

	private readonly Action<string, int> _warn;

	public CombatRoutePlaybackRuntime(ICombatPresentationProfile profile, CombatBodySequenceRuntime bodySequences, CombatGuardedScheduler<TActor> scheduler, ICombatRoutePlaybackHost<TActor> host)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_profile = profile ?? throw new ArgumentNullException("profile");
		_bodySequences = bodySequences ?? throw new ArgumentNullException("bodySequences");
		_scheduler = scheduler ?? throw new ArgumentNullException("scheduler");
		_getAnimationToken = host.GetAnimationToken;
		_beginNativeBodyTriggerSuppression = host.BeginNativeBodyTriggerSuppression;
		_clearNativeBodyTriggerSuppression = host.ClearNativeBodyTriggerSuppression;
		_warn = host.Warn;
	}

	public CombatRoutePlaybackSession<TActor> BeginAfterBody(TActor? actor, CombatPresentationRoutePlan plan)
	{
		int num = _getAnimationToken(actor);
		float guardReleaseDelaySeconds = GetGuardReleaseDelaySeconds(plan);
		_beginNativeBodyTriggerSuppression(actor, plan, num);
		ScheduleNativeBodyTriggerSuppressionClear(actor, plan, num, guardReleaseDelaySeconds);
		return new CombatRoutePlaybackSession<TActor>(actor, plan, num, guardReleaseDelaySeconds, HasNativeBodyTriggerSuppression(plan));
	}

	public float GetGuardReleaseDelaySeconds(CombatPresentationRoutePlan plan)
	{
		if (TryGetBodySteps(plan, out IReadOnlyList<CombatPresentationBodyStep> steps) && steps != null)
		{
			return _bodySequences.GetEffectSafeDelaySeconds(steps);
		}
		return 0.25f;
	}

	private void ScheduleNativeBodyTriggerSuppressionClear(TActor? actor, CombatPresentationRoutePlan plan, int token, float delaySeconds)
	{
		if (actor == null || token <= 0 || !HasNativeBodyTriggerSuppression(plan))
		{
			return;
		}
		try
		{
			_scheduler.Delay(actor, token, delaySeconds, () =>
			{
				_clearNativeBodyTriggerSuppression(actor, token);
			}, "Combat route native body trigger suppression clear scheduling failed: ", null, () =>
			{
				_clearNativeBodyTriggerSuppression(actor, token);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat route native body trigger suppression clear scheduling failed: " + SkinLog.FormatException(ex), 2);
			_clearNativeBodyTriggerSuppression(actor, token);
		}
	}

	private bool TryGetBodySteps(CombatPresentationRoutePlan plan, out IReadOnlyList<CombatPresentationBodyStep>? steps)
	{
		if (plan.BodyMode == CombatPresentationRouteBodyMode.State && !string.IsNullOrEmpty(plan.StateId) && _profile.TryGetState(plan.StateId, out CombatPresentationState state) && state.HasBodyAnimation)
		{
			steps = state.BodySteps;
			return true;
		}
		if (plan.BodyMode == CombatPresentationRouteBodyMode.Sequence && plan.BodySteps != null && plan.BodySteps.Count > 0)
		{
			steps = plan.BodySteps;
			return true;
		}
		steps = null;
		return false;
	}

	private static bool HasNativeBodyTriggerSuppression(CombatPresentationRoutePlan plan)
	{
		if (plan.SuppressedNativeBodyTriggers != null)
		{
			return plan.SuppressedNativeBodyTriggers.Count > 0;
		}
		return false;
	}
}
