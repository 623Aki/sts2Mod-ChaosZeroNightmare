namespace CznCore.Presentation;

internal sealed record CombatPresentationMeleeApproachPlan(float OffsetX, float ApproachSeconds, float ReturnSeconds, float ReturnDelaySeconds = 0f, float MinimumTravelX = 40f, float MaximumTravelX = 760f, CombatPresentationCloseCombatMotionPolicy? MotionPolicy = null)
{
	public CombatPresentationCloseCombatMotionPolicy EffectiveMotionPolicy => MotionPolicy ?? CombatPresentationCloseCombatMotionPolicy.SharedStepEffectTeleport;
}
