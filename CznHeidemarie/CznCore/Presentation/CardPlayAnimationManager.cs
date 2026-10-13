using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CznCore.Presentation;

internal sealed class CardPlayAnimationManager<TActor> where TActor : class
{
	private sealed class CardPlayAnimationState
	{
		public long Sequence { get; set; }

		public int Token { get; set; }

		public string Reason { get; set; } = "unknown";

		public CardPlayAnimationPhase Phase { get; set; }

		public int TransitionCount { get; set; }
	}

	private readonly Func<TActor?, bool, int> _nextToken;

	private readonly Action<TActor?, bool> _setManualAttackChain;

	private readonly ISkinTimingProbe _timingProbe;

	private readonly Action<string, int>? _debug;

	private readonly ConditionalWeakTable<TActor, CardPlayAnimationState> _states = new ConditionalWeakTable<TActor, CardPlayAnimationState>();

	private readonly CardPlayAnimationState _nullActorState = new CardPlayAnimationState();

	private long _nextSequence;

	public CardPlayAnimationManager(Func<TActor?, bool, int> nextToken, Action<TActor?, bool> setManualAttackChain, ISkinTimingProbe timingProbe, Action<string, int>? debug = null)
	{
		_nextToken = nextToken ?? throw new ArgumentNullException("nextToken");
		_setManualAttackChain = setManualAttackChain ?? throw new ArgumentNullException("setManualAttackChain");
		_timingProbe = timingProbe ?? throw new ArgumentNullException("timingProbe");
		_debug = debug;
	}

	public CardPlayAnimationStart Begin(TActor? actor, bool preserveMeleeApproachMotion, bool clearManualAttackChain, string reason)
	{
		long startTimestamp = _timingProbe.StartTimingProbe();
		int token = _nextToken(actor, preserveMeleeApproachMotion);
		if (clearManualAttackChain)
		{
			_setManualAttackChain(actor, arg2: false);
		}
		string text = NormalizeReason(reason);
		CardPlayAnimationState state = GetState(actor);
		state.Sequence = ++_nextSequence;
		state.Token = token;
		state.Reason = text;
		state.Phase = CardPlayAnimationPhase.Begin;
		state.TransitionCount = 0;
		LogTransition(state, CardPlayAnimationPhase.None, CardPlayAnimationPhase.Begin, "preserveMeleeApproachMotion=" + preserveMeleeApproachMotion);
		_timingProbe.LogTimingProbe("card_play_begin:" + text, startTimestamp);
		return new CardPlayAnimationStart(token, CardPlayAnimationPhase.Begin, text);
	}

	public bool Transition(TActor? actor, int token, CardPlayAnimationPhase phase, string? detail = null)
	{
		return Move(actor, token, phase, detail, terminal: false);
	}

	public bool Complete(TActor? actor, int token, string? detail = null)
	{
		return Move(actor, token, CardPlayAnimationPhase.Complete, detail, terminal: true);
	}

	public bool Cancel(TActor? actor, int token, string? detail = null)
	{
		return Move(actor, token, CardPlayAnimationPhase.Cancelled, detail, terminal: true);
	}

	private bool Move(TActor? actor, int token, CardPlayAnimationPhase phase, string? detail, bool terminal)
	{
		long startTimestamp = _timingProbe.StartTimingProbe();
		CardPlayAnimationState state = GetState(actor);
		if (state.Token != token)
		{
			LogIgnoredTransition(state, token, phase, detail);
			return false;
		}
		if (state.Phase == CardPlayAnimationPhase.Complete || state.Phase == CardPlayAnimationPhase.Cancelled)
		{
			LogIgnoredTransition(state, token, phase, detail);
			return false;
		}
		CardPlayAnimationPhase phase2 = state.Phase;
		state.Phase = phase;
		state.TransitionCount++;
		LogTransition(state, phase2, phase, detail);
		_timingProbe.LogTimingProbe("card_play_state:" + phase, startTimestamp);
		if (terminal)
		{
			state.Token = 0;
		}
		return true;
	}

	private CardPlayAnimationState GetState(TActor? actor)
	{
		if (actor != null)
		{
			return _states.GetValue(actor, (TActor _) => new CardPlayAnimationState());
		}
		return _nullActorState;
	}

	private void LogTransition(CardPlayAnimationState state, CardPlayAnimationPhase previous, CardPlayAnimationPhase phase, string? detail)
	{
		if (_debug != null)
		{
			_debug("Card play animation state seq=" + state.Sequence.ToString(CultureInfo.InvariantCulture) + " token=" + state.Token.ToString(CultureInfo.InvariantCulture) + " phase=" + previous.ToString() + "->" + phase.ToString() + " reason=" + state.Reason + FormatDetail(detail), 2);
		}
	}

	private void LogIgnoredTransition(CardPlayAnimationState state, int incomingToken, CardPlayAnimationPhase phase, string? detail)
	{
		if (_debug != null)
		{
			_debug("Ignored stale card play animation state seq=" + state.Sequence.ToString(CultureInfo.InvariantCulture) + " currentToken=" + state.Token.ToString(CultureInfo.InvariantCulture) + " incomingToken=" + incomingToken.ToString(CultureInfo.InvariantCulture) + " currentPhase=" + state.Phase.ToString() + " incomingPhase=" + phase.ToString() + " reason=" + state.Reason + FormatDetail(detail), 3);
		}
	}

	private static string NormalizeReason(string? reason)
	{
		if (!string.IsNullOrWhiteSpace(reason))
		{
			return reason.Trim();
		}
		return "unknown";
	}

	private static string FormatDetail(string? detail)
	{
		if (!string.IsNullOrWhiteSpace(detail))
		{
			return " detail=" + detail.Trim();
		}
		return string.Empty;
	}
}
