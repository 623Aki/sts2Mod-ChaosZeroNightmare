using System;

namespace CznCore.Presentation;

internal sealed class CombatPresentationController<TActor> where TActor : class
{
	private readonly ICombatPresentationProfile _profile;

	private readonly ICombatPresentationRuntime<TActor> _runtime;

	public CombatPresentationController(ICombatPresentationProfile profile, ICombatPresentationRuntime<TActor> runtime)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		_runtime = runtime ?? throw new ArgumentNullException("runtime");
	}

	public bool Play(TActor? actor, string stateId)
	{
		if (!_profile.TryGetState(stateId, out CombatPresentationState state) || !state.HasBodyAnimation)
		{
			return false;
		}
		int token = _runtime.NextToken(actor);
		if (state.ClearManualAttackChain)
		{
			_runtime.ClearManualAttackChain(actor);
		}
		if (state.BodySteps.Count == 1)
		{
			_runtime.PlayBodyAnimation(actor, state.BodySteps[0], state.LoopAnimation, token);
		}
		else
		{
			_runtime.PlayBodySequence(actor, state.BodySteps, state.LoopAnimation, token);
		}
		return true;
	}
}
