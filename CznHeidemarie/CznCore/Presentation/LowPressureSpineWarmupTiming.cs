namespace CznCore.Presentation;

internal sealed record LowPressureSpineWarmupTiming(float CombatWarmupInitialDelaySeconds, float CombatWarmupStepDelaySeconds, float FollowUpCombatWarmupInitialDelaySeconds, float FollowUpCombatWarmupStepDelaySeconds, float DeferredCombatWarmupInitialDelaySeconds, float DeferredCombatWarmupStepDelaySeconds, float NonAttackCombatWarmupStepDelaySeconds, float CombatWarmupBusyRetryDelaySeconds, float SceneCacheWarmupInitialDelaySeconds, float SceneCacheWarmupStepDelaySeconds, float PipelineWarmupHoldSeconds, float CharacterVoiceWarmupInitialDelaySeconds, float CharacterVoiceWarmupStepDelaySeconds)
{
	public static LowPressureSpineWarmupTiming Default { get; } = new LowPressureSpineWarmupTiming(0.85f, 0.24f, 9f, 0.45f, 18f, 0.65f, 0.45f, 0.45f, 0.65f, 0.34f, 0.05f, 0.65f, 0.08f);
}
