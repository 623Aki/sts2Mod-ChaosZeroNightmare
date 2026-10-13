using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatTriggeredCardComboRuntime
{
	private sealed class Scope
	{
		private readonly Sequence _sequence;

		private readonly AbstractModel _triggeringModel;

		private int _stepIndex = -1;

		private double _nextBeatMsec;

		public string Reason { get; private set; } = "route";

		public CombatTriggeredCardComboStep Step => new CombatTriggeredCardComboStep(Math.Max(0, _stepIndex));

		public Scope(Sequence sequence, AbstractModel triggeringModel)
		{
			_sequence = sequence;
			_triggeringModel = triggeringModel;
		}

		public CombatTriggeredCardComboStep Enter()
		{
			if (_stepIndex < 0)
			{
				_stepIndex = _sequence.ReserveStep(_triggeringModel);
			}
			return new CombatTriggeredCardComboStep(_stepIndex);
		}

		public void ReserveNextBeat(ulong presentationStartMsec, float nextBeatSeconds, string reason)
		{
			double num = (double)presentationStartMsec + (double)Math.Max(0f, nextBeatSeconds) * 1000.0;
			if (num > _nextBeatMsec)
			{
				_nextBeatMsec = num;
				Reason = reason;
			}
		}

		public float ResolveRemainingSeconds(ulong nowMsec)
		{
			return (float)Math.Max(0.0, (_nextBeatMsec - (double)nowMsec) / 1000.0);
		}
	}

	private sealed class Sequence
	{
		private readonly Dictionary<AbstractModel, int> _nextStepByParent = new Dictionary<AbstractModel, int>(ReferenceEqualityComparer.Instance);

		public int ReserveStep(AbstractModel triggeringModel)
		{
			_nextStepByParent.TryGetValue(triggeringModel, out var value);
			_nextStepByParent[triggeringModel] = value + 1;
			return value;
		}
	}

	[HarmonyPatch(typeof(CardCmd), "AutoPlay")]
	private static class CardAutoPlayPatch
	{
		private static void Prefix(PlayerChoiceContext choiceContext, CardModel card, out Scope? __state)
		{
			__state = Begin(choiceContext, card);
		}

		private static void Postfix(CardModel card, Scope? __state, ref Task __result)
		{
			if (__state != null)
			{
				__result = CompleteAsync(__result, card, __state);
			}
		}
	}

	private const float MinWaitSeconds = 0.001f;

	private static readonly ConditionalWeakTable<CardModel, Scope> ActiveScopes = new ConditionalWeakTable<CardModel, Scope>();

	private static readonly ConditionalWeakTable<PlayerChoiceContext, Sequence> Sequences = new ConditionalWeakTable<PlayerChoiceContext, Sequence>();

	private static Func<CardModel?, bool>? _isEligibleCard;

	internal static IReadOnlyList<Type> PatchTypes { get; } = new _003C_003Ez__ReadOnlySingleElementList<Type>(typeof(CardAutoPlayPatch));

	public static void Configure(Func<CardModel?, bool>? isEligibleCard)
	{
		_isEligibleCard = isEligibleCard;
	}

	public static bool TryEnter(CardModel? card, out CombatTriggeredCardComboStep step)
	{
		if (card != null && ActiveScopes.TryGetValue(card, out Scope value))
		{
			step = value.Enter();
			return true;
		}
		step = default;
		return false;
	}

	public static bool IsTriggeredCard(CardModel? card)
	{
		Scope value;
		if (card != null)
		{
			return ActiveScopes.TryGetValue(card, out value);
		}
		return false;
	}

	public static void ReserveNextBeat(CardModel card, ulong presentationStartMsec, float nextBeatSeconds, string reason)
	{
		if (ActiveScopes.TryGetValue(card, out Scope value))
		{
			value.ReserveNextBeat(presentationStartMsec, nextBeatSeconds, reason);
		}
	}

	private static Scope? Begin(PlayerChoiceContext choiceContext, CardModel card)
	{
		AbstractModel lastInvolvedModel = choiceContext.LastInvolvedModel;
		Func<CardModel?, bool>? isEligibleCard = _isEligibleCard;
		if (isEligibleCard == null || !isEligibleCard(card) || lastInvolvedModel == null || lastInvolvedModel == card)
		{
			return null;
		}
		Scope scope = new Scope(Sequences.GetValue(choiceContext, (PlayerChoiceContext _) => new Sequence()), lastInvolvedModel);
		ActiveScopes.Remove(card);
		ActiveScopes.Add(card, scope);
		return scope;
	}

	private static async Task CompleteAsync(Task originalTask, CardModel card, Scope scope)
	{
		try
		{
			await originalTask;
			float remainingSeconds = scope.ResolveRemainingSeconds(Time.GetTicksMsec());
			if (!(remainingSeconds <= 0.001f))
			{
				SkinLog.Debug(() => "Triggered card combo next-beat wait card=" + card.GetType().Name + " index=" + scope.Step.Index + " reason=" + scope.Reason + " seconds=" + remainingSeconds.ToString("0.###"));
				await Cmd.CustomScaledWait(remainingSeconds, remainingSeconds);
			}
		}
		finally
		{
			if (ActiveScopes.TryGetValue(card, out Scope value) && value == scope)
			{
				ActiveScopes.Remove(card);
			}
		}
	}
}
