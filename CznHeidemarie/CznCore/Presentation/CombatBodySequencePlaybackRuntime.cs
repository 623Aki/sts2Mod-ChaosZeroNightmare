using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatBodySequencePlaybackRuntime<TActor, TEffectTarget> where TActor : class where TEffectTarget : class
{
	private readonly CombatBodySequenceRuntime _bodySequences;

	private readonly ICombatPresentationRuntime<TActor> _runtime;

	private readonly Func<TActor?, bool> _consumeBodyReady;

	private readonly Action<TActor?, int, CombatPresentationHitResponsePolicy, string?> _beginHitResponseGuard;

	private readonly Action<TActor?, int, float> _scheduleHitResponseGuardClear;

	private readonly Action<TActor?, IReadOnlyList<CombatPresentationBodyStep>, string?, int, TEffectTarget?> _playBodySequence;

	private readonly Action<TActor?, string, int, float, bool> _scheduleLoopRestore;

	private readonly Action<TActor?, int, float> _scheduleIdleRestoreAfterAction;

	private readonly Action<TActor?, int, float, string?> _scheduleIdleEffectsRestore;

	private readonly Func<string?, bool> _ownsIdleEffect;

	private readonly Func<string?, string?, bool> _routeIdleThroughReadyLoop;

	private readonly string _readyLoopAnimation;

	private readonly float _postActionReadyDwellSeconds;

	public CombatBodySequencePlaybackRuntime(CombatBodySequenceRuntime bodySequences, ICombatPresentationRuntime<TActor> runtime, ICombatBodySequencePlaybackHost<TActor, TEffectTarget> host, string readyLoopAnimation, float postActionReadyDwellSeconds)
	{
		_bodySequences = bodySequences ?? throw new ArgumentNullException("bodySequences");
		_runtime = runtime ?? throw new ArgumentNullException("runtime");
		ArgumentNullException.ThrowIfNull(host, "host");
		_consumeBodyReady = host.ConsumeBodyReady;
		_beginHitResponseGuard = host.BeginHitResponseGuard;
		_scheduleHitResponseGuardClear = host.ScheduleHitResponseGuardClear;
		_playBodySequence = host.PlayBodySequence;
		_scheduleLoopRestore = host.ScheduleLoopRestore;
		_scheduleIdleRestoreAfterAction = host.ScheduleIdleRestoreAfterAction;
		_scheduleIdleEffectsRestore = host.ScheduleIdleEffectsRestore;
		_ownsIdleEffect = host.OwnsIdleEffect;
		_routeIdleThroughReadyLoop = host.RouteIdleThroughReadyLoop;
		if (string.IsNullOrEmpty(readyLoopAnimation))
		{
			throw new ArgumentException("Ready loop animation is required.", "readyLoopAnimation");
		}
		_readyLoopAnimation = readyLoopAnimation;
		_postActionReadyDwellSeconds = Math.Max(0f, postActionReadyDwellSeconds);
	}

	public CombatBodySequencePlaybackSession<TActor, TEffectTarget>? Play(TActor? actor, TEffectTarget? effectTarget, IReadOnlyList<CombatPresentationBodyStep>? sequence, string? loopAnimation = null, CombatPresentationHitResponsePolicy hitResponsePolicy = CombatPresentationHitResponsePolicy.Allow, string? hitResponseGuardReason = null)
	{
		if (sequence == null || sequence.Count == 0)
		{
			return null;
		}
		bool flag = _consumeBodyReady(actor);
		_runtime.ClearManualAttackChain(actor);
		IReadOnlyList<CombatPresentationBodyStep> readOnlyList = _bodySequences.BuildPlaybackSteps(sequence, flag);
		int num = _runtime.NextToken(actor);
		_beginHitResponseGuard(actor, num, hitResponsePolicy, hitResponseGuardReason);
		_playBodySequence(actor, readOnlyList, null, num, effectTarget);
		string text = (string.IsNullOrEmpty(loopAnimation) ? _readyLoopAnimation : loopAnimation);
		ScheduleLoopRestore(actor, readOnlyList, text, num);
		return new CombatBodySequencePlaybackSession<TActor, TEffectTarget>(actor, effectTarget, readOnlyList, text, num, flag);
	}

	private void ScheduleLoopRestore(TActor? actor, IReadOnlyList<CombatPresentationBodyStep> sequence, string? loopAnimation, int token)
	{
		if (actor == null)
		{
			return;
		}
		CombatBodySequenceRestorePlan combatBodySequenceRestorePlan = _bodySequences.BuildRestorePlan(sequence, loopAnimation, _ownsIdleEffect);
		_scheduleHitResponseGuardClear(actor, token, combatBodySequenceRestorePlan.BodyDelaySeconds);
		if (!combatBodySequenceRestorePlan.ShouldScheduleLoopRestore)
		{
			return;
		}
		if (_routeIdleThroughReadyLoop(combatBodySequenceRestorePlan.LoopAnimation, null))
		{
			_scheduleLoopRestore(actor, _readyLoopAnimation, token, combatBodySequenceRestorePlan.EffectSafeDelaySeconds, arg5: false);
			_scheduleIdleRestoreAfterAction(actor, token, combatBodySequenceRestorePlan.EffectSafeDelaySeconds + _postActionReadyDwellSeconds);
			return;
		}
		bool flag = _ownsIdleEffect(combatBodySequenceRestorePlan.LoopAnimation);
		_scheduleLoopRestore(actor, combatBodySequenceRestorePlan.LoopAnimation, token, combatBodySequenceRestorePlan.EffectSafeDelaySeconds, !flag);
		if (string.Equals(combatBodySequenceRestorePlan.LoopAnimation, _readyLoopAnimation, StringComparison.Ordinal))
		{
			_scheduleIdleRestoreAfterAction(actor, token, combatBodySequenceRestorePlan.EffectSafeDelaySeconds + _postActionReadyDwellSeconds);
		}
		else if (combatBodySequenceRestorePlan.ShouldScheduleIdleEffects)
		{
			_scheduleIdleEffectsRestore(actor, token, combatBodySequenceRestorePlan.IdleEffectsDelaySeconds, combatBodySequenceRestorePlan.LoopAnimation);
		}
	}
}
