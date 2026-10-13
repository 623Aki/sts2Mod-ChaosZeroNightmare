using System;

namespace CznCore.Presentation;

internal sealed class CombatPresentationStatePlaybackRuntime<TActor> where TActor : class
{
	private readonly ICombatPresentationProfile _profile;

	private readonly ICombatPresentationRuntime<TActor> _runtime;

	private readonly CombatPresentationController<TActor> _presentation;

	private readonly CombatBodySequenceRuntime _bodySequences;

	private readonly Action<TActor?, int, CombatPresentationHitResponsePolicy, string?> _beginHitResponseGuard;

	private readonly Action<TActor?, int, float> _scheduleHitResponseGuardClear;

	public CombatPresentationStatePlaybackRuntime(ICombatPresentationProfile profile, ICombatPresentationRuntime<TActor> runtime, CombatPresentationController<TActor> presentation, CombatBodySequenceRuntime bodySequences, ICombatHitResponseGuardHost<TActor> hitResponseGuards)
	{
		ArgumentNullException.ThrowIfNull(hitResponseGuards, "hitResponseGuards");
		_profile = profile ?? throw new ArgumentNullException("profile");
		_runtime = runtime ?? throw new ArgumentNullException("runtime");
		_presentation = presentation ?? throw new ArgumentNullException("presentation");
		_bodySequences = bodySequences ?? throw new ArgumentNullException("bodySequences");
		_beginHitResponseGuard = hitResponseGuards.BeginHitResponseGuard;
		_scheduleHitResponseGuardClear = hitResponseGuards.ScheduleHitResponseGuardClear;
	}

	public bool Play(TActor? actor, string? stateId, CombatPresentationHitResponsePolicy hitResponsePolicy = CombatPresentationHitResponsePolicy.Allow, string? hitResponseGuardReason = null)
	{
		if (string.IsNullOrEmpty(stateId) || hitResponsePolicy == CombatPresentationHitResponsePolicy.Allow || !_profile.TryGetState(stateId, out CombatPresentationState state) || !state.HasBodyAnimation)
		{
			if (!string.IsNullOrEmpty(stateId))
			{
				return _presentation.Play(actor, stateId);
			}
			return false;
		}
		int num = _runtime.NextToken(actor);
		if (state.ClearManualAttackChain)
		{
			_runtime.ClearManualAttackChain(actor);
		}
		_beginHitResponseGuard(actor, num, hitResponsePolicy, hitResponseGuardReason);
		if (state.BodySteps.Count == 1)
		{
			_runtime.PlayBodyAnimation(actor, state.BodySteps[0], state.LoopAnimation, num);
		}
		else
		{
			_runtime.PlayBodySequence(actor, state.BodySteps, state.LoopAnimation, num);
		}
		_scheduleHitResponseGuardClear(actor, num, _bodySequences.GetSequenceDelaySeconds(state.BodySteps));
		return true;
	}
}
