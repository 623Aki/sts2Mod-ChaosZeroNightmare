using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CombatPresentationCommonDefense
{
	private const float SystemBuffShieldBackLifetimeSeconds = 0.6667f;

	private const float SystemBuffShieldFront1LifetimeSeconds = 0.9167f;

	private const float SystemBuffShieldFront2LifetimeSeconds = 0.8f;

	private const float SystemBuffShieldLayerTimeScale = 2f;

	private const float SystemBuffShieldParticleRepeatDelaySeconds = 0.4f;

	private const float SystemBuffShieldParticle2LifetimeSeconds = 0.3f;

	private const float SystemBuffShieldParticle2TailSeconds = 0.3f;

	private const float SystemBuffShieldParticle3LifetimeSeconds = 0.5f;

	private const float SystemBuffShieldParticle3TailSeconds = 0.6f;

	private const float SystemBuffShieldParticle3OffsetY = -160f;

	public const float SystemBuffShieldDefaultScale = 0.62f;

	public const float DefenseSfxAudibilityAdapterGainLinear = 2f;

	public static IReadOnlyList<CombatPresentationEffectCue> BuildSystemBuffShieldEffectCues(string sceneRoot, float scale = 0.62f, CombatPresentationEffectAnchor anchor = CombatPresentationEffectAnchor.Owner, float lifetimeScale = 1f, float timeScaleMultiplier = 1f, string groupNamePrefix = "system_buff_shield")
	{
		string text = NormalizeSceneRoot(sceneRoot);
		float num = Math.Max(0.0001f, lifetimeScale);
		float num2 = Math.Max(0.0001f, timeScaleMultiplier);
		string text2 = (string.IsNullOrWhiteSpace(groupNamePrefix) ? "system_buff_shield" : groupNamePrefix);
		return new _003C_003Ez__ReadOnlyArray<CombatPresentationEffectCue>(new CombatPresentationEffectCue[3]
		{
			SystemBuffShieldLayer(text + "system_buff_shield_b.tscn", 0.6667f * num, anchor, scale, -10, text2 + "_b", 2f * num2),
			SystemBuffShieldLayer(text + "system_buff_shield_f1.tscn", 0.9167f * num, anchor, scale, 10, text2 + "_f1", 2f * num2),
			SystemBuffShieldLayer(text + "system_buff_shield_f2.tscn", 0.8f * num, anchor, scale, 11, text2 + "_f2", num2)
		});
	}

	public static IReadOnlyList<CombatPresentationEffectCue> BuildSystemBuffShieldFullEffectCues(string sceneRoot, float scale = 0.62f, CombatPresentationEffectAnchor anchor = CombatPresentationEffectAnchor.Owner, string groupNamePrefix = "system_buff_shield")
	{
		string sceneRoot2 = NormalizeSceneRoot(sceneRoot);
		string groupNamePrefix2 = (string.IsNullOrWhiteSpace(groupNamePrefix) ? "system_buff_shield" : groupNamePrefix);
		IReadOnlyList<CombatPresentationEffectCue> readOnlyList = BuildSystemBuffShieldEffectCues(sceneRoot2, scale, anchor, 1f, 1f, groupNamePrefix2);
		IReadOnlyList<CombatPresentationEffectCue> readOnlyList2 = BuildSystemBuffShieldFullAdditionalEffectCues(sceneRoot2, scale, anchor, groupNamePrefix2);
		int num = 0;
		CombatPresentationEffectCue[] array = new CombatPresentationEffectCue[readOnlyList.Count + readOnlyList2.Count];
		foreach (CombatPresentationEffectCue item in readOnlyList)
		{
			array[num] = item;
			num++;
		}
		foreach (CombatPresentationEffectCue item2 in readOnlyList2)
		{
			array[num] = item2;
			num++;
		}
		return new _003C_003Ez__ReadOnlyArray<CombatPresentationEffectCue>(array);
	}

	public static IReadOnlyList<CombatPresentationEffectCue> BuildSystemBuffShieldFullAdditionalEffectCues(string sceneRoot, float scale = 0.62f, CombatPresentationEffectAnchor anchor = CombatPresentationEffectAnchor.Owner, string groupNamePrefix = "system_buff_shield")
	{
		string text = NormalizeSceneRoot(sceneRoot);
		string text2 = (string.IsNullOrWhiteSpace(groupNamePrefix) ? "system_buff_shield" : groupNamePrefix);
		return new _003C_003Ez__ReadOnlyArray<CombatPresentationEffectCue>(new CombatPresentationEffectCue[4]
		{
			SystemBuffShieldParticle(text + "system_debuff_resist_shadow_pat2_particles.tscn", 0f, 0.3f, 0.3f, anchor, scale, 11, text2 + "_pat2_1"),
			SystemBuffShieldParticle(text + "system_debuff_resist_shadow_pat3_particles.tscn", 0f, 0.5f, 0.6f, anchor, scale, 12, text2 + "_pat3_1", -160f),
			SystemBuffShieldParticle(text + "system_debuff_resist_shadow_pat2_particles.tscn", 0.4f, 0.3f, 0.3f, anchor, scale, 11, text2 + "_pat2_2"),
			SystemBuffShieldParticle(text + "system_debuff_resist_shadow_pat3_particles.tscn", 0.4f, 0.5f, 0.6f, anchor, scale, 12, text2 + "_pat3_2", -160f)
		});
	}

	private static CombatPresentationEffectCue SystemBuffShieldParticle(string scenePath, float startDelaySeconds, float lifetimeSeconds, float particleTailSeconds, CombatPresentationEffectAnchor anchor, float scale, int zIndex, string groupName, float offsetY = 0f)
	{
		return new CombatPresentationEffectCue(scenePath, startDelaySeconds, lifetimeSeconds, ClearExisting: false, CombatPresentationEffectPalette.Idle, anchor, UseNeutralPmaMaterial: false, UseAttackBoost: true, groupName, scale, 1f, 0f, offsetY, 0f, 1f, zIndex, 0f, 0f, AllowSynchronousSceneLoad: true, 1, PreserveSceneRoot: false, CombatPresentationEffectImportance.Normal, 1f, null, NeutralizeAnchorScale: false, CombatPresentationEffectWarmupTier.Auto, null, particleTailSeconds);
	}

	public static IReadOnlyList<CombatPresentationSfxCue> BuildSystemBuffShieldSfxCues(string sfxPath)
	{
		if (string.IsNullOrWhiteSpace(sfxPath))
		{
			throw new ArgumentException("SFX path is required.", "sfxPath");
		}
		return new _003C_003Ez__ReadOnlySingleElementList<CombatPresentationSfxCue>(new CombatPresentationSfxCue(sfxPath, 0f, 2f));
	}

	private static CombatPresentationEffectCue SystemBuffShieldLayer(string scenePath, float lifetimeSeconds, CombatPresentationEffectAnchor anchor, float scale, int zIndex, string groupName, float timeScale = 1f)
	{
		return new CombatPresentationEffectCue(scenePath, 0f, lifetimeSeconds, ClearExisting: false, CombatPresentationEffectPalette.Idle, anchor, UseNeutralPmaMaterial: true, UseAttackBoost: true, groupName, scale, timeScale, 0f, 0f, 0f, 1f, zIndex, 0f, 0f, AllowSynchronousSceneLoad: true, 1, PreserveSceneRoot: false, CombatPresentationEffectImportance.Normal, 1f, null, NeutralizeAnchorScale: false, CombatPresentationEffectWarmupTier.Auto, null, 0f, BypassActiveBudget: true);
	}

	private static string NormalizeSceneRoot(string sceneRoot)
	{
		if (string.IsNullOrWhiteSpace(sceneRoot))
		{
			throw new ArgumentException("Scene root is required.", "sceneRoot");
		}
		if (!sceneRoot.EndsWith("/", StringComparison.Ordinal))
		{
			return sceneRoot + "/";
		}
		return sceneRoot;
	}
}
