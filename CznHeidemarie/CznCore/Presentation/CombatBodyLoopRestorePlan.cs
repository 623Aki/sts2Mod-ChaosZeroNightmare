namespace CznCore.Presentation;

internal sealed record CombatBodyLoopRestorePlan(string? LoopAnimation, bool RoutesIdleThroughReadyLoop, bool ShouldScheduleIdleRestoreAfterAction, float IdleRestoreDelaySeconds, bool ShouldScheduleIdleEffectsRestore, float IdleEffectsDelaySeconds, string? IdleEffectsAnimation)
{
	public static CombatBodyLoopRestorePlan None { get; } = new CombatBodyLoopRestorePlan(null, RoutesIdleThroughReadyLoop: false, ShouldScheduleIdleRestoreAfterAction: false, 0f, ShouldScheduleIdleEffectsRestore: false, 0f, null);

	public bool ShouldQueueLoopAnimation => !string.IsNullOrEmpty(LoopAnimation);
}
