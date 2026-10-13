using System;
using CznCore.Godot;

namespace CznCore.Presentation;

internal sealed class CombatBodyReadyPresentationRuntime<TActor, TCard> where TActor : class where TCard : class
{
	private const float MinRestoreCompleteDelaySeconds = 0.05f;

	private readonly ICombatPresentationProfile _profile;

	private readonly CombatPresentationStatePlaybackRuntime<TActor> _presentationStates;

	private readonly CombatBodySequenceRuntime _bodySequences;

	private readonly Action<string, int> _warn;

	private readonly CombatBodyReadyStateRuntime<TActor, TCard> _state;

	public CombatBodyReadyPresentationRuntime(float restoreGraceSeconds, ICombatBodyReadyHost<TActor, TCard> host, ICombatPresentationProfile profile, CombatPresentationStatePlaybackRuntime<TActor> presentationStates, CombatBodySequenceRuntime bodySequences)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_profile = profile ?? throw new ArgumentNullException("profile");
		_presentationStates = presentationStates ?? throw new ArgumentNullException("presentationStates");
		_bodySequences = bodySequences ?? throw new ArgumentNullException("bodySequences");
		_warn = host.Warn;
		_state = new CombatBodyReadyStateRuntime<TActor, TCard>(restoreGraceSeconds, 1f, host.IsTargetCard, host.ResolveActor, host.CanShow, PlayReady, PlayRestore, host.Warn);
	}

	public bool ShowForCard(TCard? card)
	{
		return _state.ShowForCard(card);
	}

	public bool HideForCard(TCard? card)
	{
		return _state.HideForCard(card);
	}

	public bool CancelForCard(TCard? card)
	{
		return _state.CancelForCard(card);
	}

	public bool HoldForCardPlay(TCard? card)
	{
		return _state.HoldForCardPlay(card);
	}

	public void BeginAction(TCard? card)
	{
		_state.BeginAction(card);
	}

	public void BeginAction(TActor? actor)
	{
		_state.BeginAction(actor);
	}

	public bool ConsumeReadyForAction(TCard? card)
	{
		return _state.ConsumeReadyForAction(card);
	}

	public bool ConsumeReadyForAction(TActor? actor)
	{
		return _state.ConsumeReadyForAction(actor);
	}

	public void CompleteAction(TActor? actor)
	{
		_state.CompleteAction(actor);
	}

	public void Clear(TActor? actor)
	{
		_state.Clear(actor);
	}

	public bool IsTokenCurrent(TActor? actor, int token)
	{
		return _state.IsTokenCurrent(actor, token);
	}

	public bool IsTokenCurrent(TActor? actor, int token, CombatBodyReadyPresentationState expectedState)
	{
		return _state.IsTokenCurrent(actor, token, expectedState);
	}

	public void CompleteRestore(TActor? actor, int token)
	{
		_state.CompleteRestore(actor, token);
	}

	public bool IsReady(TActor? actor)
	{
		return _state.IsReady(actor);
	}

	public bool IsPresentationActive(TActor? actor)
	{
		return _state.IsPresentationActive(actor);
	}

	public bool IsReadyForCard(TCard? card)
	{
		return _state.IsReadyForCard(card);
	}

	private void PlayReady(TActor actor, TCard card, int token)
	{
		if (_state.IsTokenCurrent(actor, token, CombatBodyReadyPresentationState.CardReady) && !_presentationStates.Play(actor, _profile.CommonStateIds.CardReady))
		{
			_warn("Combat body ready state playback failed: " + _profile.CommonStateIds.CardReady, 2);
			_state.Clear(actor);
		}
	}

	private void PlayRestore(TActor actor, int token)
	{
		if (!_state.IsTokenCurrent(actor, token, CombatBodyReadyPresentationState.RestoringIdle))
		{
			return;
		}
		if (!_presentationStates.Play(actor, _profile.CommonStateIds.RestoreIdle))
		{
			_warn("Combat body ready restore playback failed: " + _profile.CommonStateIds.RestoreIdle, 2);
			_state.CompleteRestore(actor, token);
			return;
		}
		float val = ResolveStateDurationSeconds(_profile.CommonStateIds.RestoreIdle);
		try
		{
			GodotScheduler.Delay(Math.Max(0.05f, val), () =>
			{
				_state.CompleteRestore(actor, token);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat body ready restore completion scheduling failed: " + SkinLog.FormatException(ex), 2);
			_state.CompleteRestore(actor, token);
		}
	}

	private float ResolveStateDurationSeconds(string stateId)
	{
		if (!_profile.TryGetState(stateId, out CombatPresentationState state) || !state.HasBodyAnimation)
		{
			return 0.05f;
		}
		return Math.Max(0.05f, _bodySequences.GetSequenceDelaySeconds(state.BodySteps));
	}
}
