namespace CznCore.Presentation;

internal sealed record CombatPresentationCloseCombatMotionPolicy(bool SnapApproach, float ApproachSnapDelaySeconds, bool SnapReturn, float ReturnSnapDelaySeconds, bool LiftOwnerLayer, int OwnerLayerZIndex, int StepEffectLayerZIndex)
{
	public static CombatPresentationCloseCombatMotionPolicy SharedStepEffectTeleport { get; } = new CombatPresentationCloseCombatMotionPolicy(SnapApproach: true, 0.04f, SnapReturn: true, 0.06f, LiftOwnerLayer: true, -6, -3);
}
