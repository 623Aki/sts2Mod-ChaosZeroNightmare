using System.Collections.Generic;

namespace CznCore.Presentation;

internal interface ICombatBodySequencePlaybackHost<TActor, TEffectTarget> where TActor : class where TEffectTarget : class
{
	bool ConsumeBodyReady(TActor? actor);

	void BeginHitResponseGuard(TActor? actor, int token, CombatPresentationHitResponsePolicy policy, string? reason);

	void ScheduleHitResponseGuardClear(TActor? actor, int token, float delaySeconds);

	void PlayBodySequence(TActor? actor, IReadOnlyList<CombatPresentationBodyStep> steps, string? loopAnimation, int token, TEffectTarget? effectTarget);

	void ScheduleLoopRestore(TActor? actor, string animation, int token, float delaySeconds, bool completeAction);

	void ScheduleIdleRestoreAfterAction(TActor? actor, int token, float delaySeconds);

	void ScheduleIdleEffectsRestore(TActor? actor, int token, float delaySeconds, string? animation);

	bool OwnsIdleEffect(string? animation);

	bool RouteIdleThroughReadyLoop(string? loopAnimation, string? readyLoopAnimation);
}
