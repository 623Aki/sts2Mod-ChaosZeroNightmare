namespace CznCore.Presentation;

internal sealed record CombatBodySequenceRestorePlan(float BodyDelaySeconds, float EffectSafeDelaySeconds, string? LoopAnimation, bool ShouldScheduleIdleEffects, float IdleEffectsDelaySeconds)
{
	public bool ShouldScheduleLoopRestore => !string.IsNullOrEmpty(LoopAnimation);
}
