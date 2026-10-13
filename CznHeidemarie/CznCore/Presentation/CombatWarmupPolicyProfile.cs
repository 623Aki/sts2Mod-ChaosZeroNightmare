using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatWarmupPolicyProfile(int TransientEffectPoolSizePerKey, int TransientTargetEffectPoolSizePerKey, int StarterTransientEffectPoolSizePerKey, int StarterTransientTargetEffectPoolSizePerKey, int FollowUpTransientEffectPoolSizePerKey, int FollowUpTransientTargetEffectPoolSizePerKey, int AttackTransientWarmupBatchSize, int NonAttackTransientWarmupBatchSize, int AttackTransientTargetWarmupMinPoolSize, float CombatWarmupInitialDelaySeconds, float CombatWarmupStepDelaySeconds, float FollowUpCombatWarmupInitialDelaySeconds, float FollowUpCombatWarmupStepDelaySeconds, float DeferredCombatWarmupInitialDelaySeconds, float DeferredCombatWarmupStepDelaySeconds, float NonAttackCombatWarmupStepDelaySeconds, float CombatWarmupBusyRetryDelaySeconds, float SceneCacheWarmupInitialDelaySeconds, float SceneCacheWarmupStepDelaySeconds, float PipelineWarmupHoldSeconds, float CharacterVoiceWarmupInitialDelaySeconds, float CharacterVoiceWarmupStepDelaySeconds, double TimingProbeThresholdMs, bool WarmFollowUpCombatEffects, bool WarmDeferredCombatEffects, bool PrewarmAllCombatSfx, string[] StarterCombatWarmupTokens, string[] FollowUpCombatWarmupTokens, string[] CombatSfxWarmupTokens, string[] SceneCacheStarterCombatWarmupTokens, string[] SceneCacheFollowUpCombatWarmupTokens)
{
	public static CombatWarmupPolicyProfile Default { get; } = new CombatWarmupPolicyProfile(3, 6, 1, 1, 2, 3, 1, 1, 3, 0.35f, 0.05f, 5.5f, 0.16f, 11f, 0.28f, 0.08f, 0.12f, 0.9f, 0.12f, 0.08f, 0.65f, 0.08f, 2.0, WarmFollowUpCombatEffects: true, WarmDeferredCombatEffects: true, PrewarmAllCombatSfx: true, new string[8] { "attack_play1", "attack_play2", "u1_attack", "idle_to_b_idle", "b_idle_to_idle", "defense", "defend", "block" }, new string[3] { "u2_buff", "u3_buff", "u5_attack" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

	public static CombatWarmupPolicyProfile LowPressureSpineWarmup(IReadOnlyList<string> starterTokens, IReadOnlyList<string> combatSfxTokens, IReadOnlyList<string>? sceneCacheStarterTokens = null, LowPressureSpineWarmupTiming? timing = null)
	{
		ArgumentNullException.ThrowIfNull(starterTokens, "starterTokens");
		ArgumentNullException.ThrowIfNull(combatSfxTokens, "combatSfxTokens");
		if ((object)timing == null)
		{
			timing = LowPressureSpineWarmupTiming.Default;
		}
		return Default with
		{
			TransientEffectPoolSizePerKey = 1,
			TransientTargetEffectPoolSizePerKey = 2,
			StarterTransientEffectPoolSizePerKey = 1,
			StarterTransientTargetEffectPoolSizePerKey = 1,
			FollowUpTransientEffectPoolSizePerKey = 1,
			FollowUpTransientTargetEffectPoolSizePerKey = 1,
			AttackTransientWarmupBatchSize = 1,
			NonAttackTransientWarmupBatchSize = 1,
			AttackTransientTargetWarmupMinPoolSize = 1,
			CombatWarmupInitialDelaySeconds = timing.CombatWarmupInitialDelaySeconds,
			CombatWarmupStepDelaySeconds = timing.CombatWarmupStepDelaySeconds,
			FollowUpCombatWarmupInitialDelaySeconds = timing.FollowUpCombatWarmupInitialDelaySeconds,
			FollowUpCombatWarmupStepDelaySeconds = timing.FollowUpCombatWarmupStepDelaySeconds,
			DeferredCombatWarmupInitialDelaySeconds = timing.DeferredCombatWarmupInitialDelaySeconds,
			DeferredCombatWarmupStepDelaySeconds = timing.DeferredCombatWarmupStepDelaySeconds,
			NonAttackCombatWarmupStepDelaySeconds = timing.NonAttackCombatWarmupStepDelaySeconds,
			CombatWarmupBusyRetryDelaySeconds = timing.CombatWarmupBusyRetryDelaySeconds,
			SceneCacheWarmupInitialDelaySeconds = timing.SceneCacheWarmupInitialDelaySeconds,
			SceneCacheWarmupStepDelaySeconds = timing.SceneCacheWarmupStepDelaySeconds,
			PipelineWarmupHoldSeconds = timing.PipelineWarmupHoldSeconds,
			CharacterVoiceWarmupInitialDelaySeconds = timing.CharacterVoiceWarmupInitialDelaySeconds,
			CharacterVoiceWarmupStepDelaySeconds = timing.CharacterVoiceWarmupStepDelaySeconds,
			WarmFollowUpCombatEffects = false,
			WarmDeferredCombatEffects = false,
			PrewarmAllCombatSfx = false,
			StarterCombatWarmupTokens = CopyWarmupTokens(starterTokens),
			FollowUpCombatWarmupTokens = Array.Empty<string>(),
			CombatSfxWarmupTokens = CopyWarmupTokens(combatSfxTokens),
			SceneCacheStarterCombatWarmupTokens = CopyWarmupTokens(sceneCacheStarterTokens ?? starterTokens),
			SceneCacheFollowUpCombatWarmupTokens = Array.Empty<string>()
		};
	}

	private static string[] CopyWarmupTokens(IReadOnlyList<string> tokens)
	{
		List<string> list = new List<string>(tokens.Count);
		foreach (string token in tokens)
		{
			if (!string.IsNullOrWhiteSpace(token))
			{
				list.Add(token);
			}
		}
		return list.ToArray();
	}
}
