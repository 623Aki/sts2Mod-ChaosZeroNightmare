using System;
using System.Runtime.CompilerServices;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatBodyReadyStateRuntime<TActor, TCard> where TActor : class where TCard : class
{
	private sealed class State
	{
		public int Token { get; private set; }

		public TCard? Card { get; set; }

		public TCard? PendingCard { get; set; }

		public CombatBodyReadyPresentationState PresentationState { get; set; }

		public ulong LastCardPlayCommittedMsec { get; set; }

		public int NextToken()
		{
			Token++;
			return Token;
		}
	}

	private const float MinRestoreDelaySeconds = 0.05f;

	private readonly ConditionalWeakTable<TActor, State> _states = new ConditionalWeakTable<TActor, State>();

	private readonly float _restoreGraceSeconds;

	private readonly float _cardPlayIdleDebounceSeconds;

	private readonly Func<TCard?, bool> _isTargetCard;

	private readonly Func<TCard, TActor?> _resolveActor;

	private readonly Func<TActor, bool> _canEnterReady;

	private readonly Action<TActor, TCard, int> _playReady;

	private readonly Action<TActor, int> _playRestore;

	private readonly Action<string, int> _warn;

	public CombatBodyReadyStateRuntime(float restoreGraceSeconds, float cardPlayIdleDebounceSeconds, Func<TCard?, bool> isTargetCard, Func<TCard, TActor?> resolveActor, Func<TActor, bool> canEnterReady, Action<TActor, TCard, int> playReady, Action<TActor, int> playRestore, Action<string, int> warn)
	{
		_restoreGraceSeconds = restoreGraceSeconds;
		_cardPlayIdleDebounceSeconds = Math.Max(0f, cardPlayIdleDebounceSeconds);
		_isTargetCard = isTargetCard ?? throw new ArgumentNullException("isTargetCard");
		_resolveActor = resolveActor ?? throw new ArgumentNullException("resolveActor");
		_canEnterReady = canEnterReady ?? throw new ArgumentNullException("canEnterReady");
		_playReady = playReady ?? throw new ArgumentNullException("playReady");
		_playRestore = playRestore ?? throw new ArgumentNullException("playRestore");
		_warn = warn ?? throw new ArgumentNullException("warn");
	}

	public bool ShowForCard(TCard? card)
	{
		if (!_isTargetCard(card) || card == null)
		{
			return false;
		}
		TActor val = _resolveActor(card);
		if (val == null || !_canEnterReady(val))
		{
			return false;
		}
		State orCreateValue = _states.GetOrCreateValue(val);
		if (orCreateValue.PresentationState == CombatBodyReadyPresentationState.CardReady)
		{
			orCreateValue.Card = card;
			orCreateValue.PendingCard = null;
			return false;
		}
		if (orCreateValue.PresentationState == CombatBodyReadyPresentationState.RestorePending)
		{
			orCreateValue.NextToken();
			orCreateValue.Card = card;
			orCreateValue.PendingCard = null;
			orCreateValue.PresentationState = CombatBodyReadyPresentationState.CardReady;
			return false;
		}
		if (orCreateValue.PresentationState == CombatBodyReadyPresentationState.ActionPlaying)
		{
			orCreateValue.PendingCard = card;
			return false;
		}
		int arg = orCreateValue.NextToken();
		orCreateValue.Card = card;
		orCreateValue.PendingCard = null;
		orCreateValue.PresentationState = CombatBodyReadyPresentationState.CardReady;
		_playReady(val, card, arg);
		return true;
	}

	public bool HideForCard(TCard? card)
	{
		return ScheduleRestoreForCard(card, respectCardPlayDebounce: true);
	}

	public bool CancelForCard(TCard? card)
	{
		return ScheduleRestoreForCard(card, respectCardPlayDebounce: false);
	}

	public bool HoldForCardPlay(TCard? card)
	{
		if (!_isTargetCard(card) || card == null)
		{
			return false;
		}
		TActor val = _resolveActor(card);
		if (val == null || !_states.TryGetValue(val, out State value))
		{
			return false;
		}
		if (!IsReadyPresentationState(value.PresentationState) || card != value.Card)
		{
			return false;
		}
		value.LastCardPlayCommittedMsec = Time.GetTicksMsec();
		return ScheduleRestoreForCard(card, respectCardPlayDebounce: true);
	}

	public void BeginAction(TCard? card)
	{
		if (_isTargetCard(card) && card != null)
		{
			TActor val = _resolveActor(card);
			if (val != null)
			{
				ConsumeReadyForAction(val);
			}
		}
	}

	public void BeginAction(TActor? actor)
	{
		ConsumeReadyForAction(actor);
	}

	public bool ConsumeReadyForAction(TCard? card)
	{
		if (!_isTargetCard(card) || card == null)
		{
			return false;
		}
		return ConsumeReadyForAction(_resolveActor(card));
	}

	public bool ConsumeReadyForAction(TActor? actor)
	{
		if (actor == null)
		{
			return false;
		}
		State orCreateValue = _states.GetOrCreateValue(actor);
		bool result = IsReadyPresentationState(orCreateValue.PresentationState);
		orCreateValue.NextToken();
		orCreateValue.Card = null;
		orCreateValue.PendingCard = null;
		orCreateValue.LastCardPlayCommittedMsec = 0uL;
		orCreateValue.PresentationState = CombatBodyReadyPresentationState.ActionPlaying;
		return result;
	}

	public void CompleteAction(TActor? actor)
	{
		if (actor == null)
		{
			return;
		}
		State orCreateValue = _states.GetOrCreateValue(actor);
		if (orCreateValue.PresentationState == CombatBodyReadyPresentationState.ActionPlaying)
		{
			TCard pendingCard = orCreateValue.PendingCard;
			orCreateValue.Card = null;
			orCreateValue.PendingCard = null;
			orCreateValue.LastCardPlayCommittedMsec = 0uL;
			orCreateValue.PresentationState = CombatBodyReadyPresentationState.Idle;
			if (pendingCard != null)
			{
				ShowForCard(pendingCard);
			}
		}
	}

	public void Clear(TActor? actor)
	{
		if (actor != null)
		{
			State orCreateValue = _states.GetOrCreateValue(actor);
			orCreateValue.NextToken();
			orCreateValue.Card = null;
			orCreateValue.PendingCard = null;
			orCreateValue.LastCardPlayCommittedMsec = 0uL;
			orCreateValue.PresentationState = CombatBodyReadyPresentationState.Idle;
		}
	}

	public bool IsReady(TActor? actor)
	{
		if (actor != null && _states.TryGetValue(actor, out State value))
		{
			return IsReadyPresentationState(value.PresentationState);
		}
		return false;
	}

	public bool IsPresentationActive(TActor? actor)
	{
		if (actor != null && _states.TryGetValue(actor, out State value))
		{
			return value.PresentationState != CombatBodyReadyPresentationState.Idle;
		}
		return false;
	}

	public bool IsReadyForCard(TCard? card)
	{
		if (!_isTargetCard(card) || card == null)
		{
			return false;
		}
		TActor val = _resolveActor(card);
		if (val != null && _states.TryGetValue(val, out State value) && IsReadyPresentationState(value.PresentationState))
		{
			return card == value.Card;
		}
		return false;
	}

	public bool IsTokenCurrent(TActor? actor, int token)
	{
		return IsTokenCurrent(actor, token, null);
	}

	public bool IsTokenCurrent(TActor? actor, int token, CombatBodyReadyPresentationState? expectedState)
	{
		if (actor == null || !_states.TryGetValue(actor, out State value))
		{
			return false;
		}
		if (value.Token == token)
		{
			if (expectedState.HasValue)
			{
				return value.PresentationState == expectedState.Value;
			}
			return true;
		}
		return false;
	}

	public void CompleteRestore(TActor? actor, int token)
	{
		if (IsTokenCurrent(actor, token, CombatBodyReadyPresentationState.RestoringIdle) && actor != null && _states.TryGetValue(actor, out State value))
		{
			value.Card = null;
			value.LastCardPlayCommittedMsec = 0uL;
			value.PresentationState = CombatBodyReadyPresentationState.Idle;
		}
	}

	private bool ScheduleRestoreForCard(TCard? card, bool respectCardPlayDebounce)
	{
		if (!_isTargetCard(card) || card == null)
		{
			return false;
		}
		TActor actor = _resolveActor(card);
		if (actor == null || !_states.TryGetValue(actor, out State value))
		{
			return false;
		}
		if (value.PresentationState == CombatBodyReadyPresentationState.ActionPlaying)
		{
			if (card != value.PendingCard)
			{
				return false;
			}
			value.PendingCard = null;
			return true;
		}
		if (!IsReadyPresentationState(value.PresentationState) || card != value.Card)
		{
			return false;
		}
		int token = value.NextToken();
		value.PresentationState = CombatBodyReadyPresentationState.RestorePending;
		float seconds = ResolveRestoreDelaySeconds(value, respectCardPlayDebounce);
		try
		{
			GodotScheduler.Delay(seconds, () =>
			{
				FinalizeRestore(actor, token);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat body ready restore scheduling failed: " + SkinLog.FormatException(ex), 2);
			FinalizeRestore(actor, token);
		}
		return true;
	}

	private float ResolveRestoreDelaySeconds(State state, bool respectCardPlayDebounce)
	{
		float num = _restoreGraceSeconds;
		if (respectCardPlayDebounce && _cardPlayIdleDebounceSeconds > 0f && state.LastCardPlayCommittedMsec != 0)
		{
			ulong ticksMsec = Time.GetTicksMsec();
			float num2 = ((ticksMsec >= state.LastCardPlayCommittedMsec) ? ((float)(ticksMsec - state.LastCardPlayCommittedMsec) / 1000f) : 0f);
			float val = _cardPlayIdleDebounceSeconds - num2;
			num = Math.Max(num, val);
		}
		return Math.Max(0.05f, num);
	}

	private void FinalizeRestore(TActor actor, int token)
	{
		if (!IsTokenCurrent(actor, token, CombatBodyReadyPresentationState.RestorePending) || !_states.TryGetValue(actor, out State value))
		{
			return;
		}
		value.Card = null;
		value.PendingCard = null;
		value.LastCardPlayCommittedMsec = 0uL;
		value.PresentationState = CombatBodyReadyPresentationState.RestoringIdle;
		try
		{
			_playRestore(actor, token);
		}
		catch (Exception ex)
		{
			CompleteRestore(actor, token);
			_warn("Combat body ready restore failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private static bool IsReadyPresentationState(CombatBodyReadyPresentationState state)
	{
		if ((uint)(state - 1) <= 1u)
		{
			return true;
		}
		return false;
	}
}
