using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CznCore.Presentation;

internal sealed class CombatBodySequenceRuntime
{
	private sealed record CachedPlaybackSteps(CombatPresentationBodyStep[] Steps);

	private sealed record CachedSequenceMetrics(float DelaySeconds, float TransientEffectChainDurationSeconds);

	private sealed record CachedSequenceFormat(string FormattedSequence);

	private sealed record CachedStepSchedule(CombatBodySequenceStepSchedule[] Steps);

	private const float MinSequenceDelaySeconds = 0.05f;

	private readonly ICombatPresentationProfile _profile;

	private readonly string _readyExitAnimation;

	private readonly float _readyToIdleEffectDelaySeconds;

	private readonly Dictionary<string, float> _animationDelaySeconds = new Dictionary<string, float>(StringComparer.Ordinal);

	private readonly HashSet<string> _missingAnimationTimings = new HashSet<string>(StringComparer.Ordinal);

	private readonly ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedPlaybackSteps> _readyExitPlaybackSteps = new ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedPlaybackSteps>();

	private readonly ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedSequenceMetrics> _sequenceMetrics = new ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedSequenceMetrics>();

	private readonly ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedSequenceFormat> _sequenceFormats = new ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedSequenceFormat>();

	private readonly ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedStepSchedule> _stepSchedules = new ConditionalWeakTable<IReadOnlyList<CombatPresentationBodyStep>, CachedStepSchedule>();

	public CombatBodySequenceRuntime(ICombatPresentationProfile profile, string readyExitAnimation, float readyToIdleEffectDelaySeconds)
	{
		_profile = profile;
		_readyExitAnimation = readyExitAnimation;
		_readyToIdleEffectDelaySeconds = readyToIdleEffectDelaySeconds;
	}

	public IReadOnlyList<CombatPresentationBodyStep> BuildPlaybackSteps(IReadOnlyList<CombatPresentationBodyStep> sequence, bool prependReadyExit)
	{
		if (!prependReadyExit)
		{
			return sequence;
		}
		return _readyExitPlaybackSteps.GetValue(sequence, BuildReadyExitPlaybackSteps).Steps;
	}

	private CachedPlaybackSteps BuildReadyExitPlaybackSteps(IReadOnlyList<CombatPresentationBodyStep> sequence)
	{
		CombatPresentationBodyStep[] array = new CombatPresentationBodyStep[sequence.Count + 1];
		array[0] = new CombatPresentationBodyStep(_readyExitAnimation);
		for (int i = 0; i < sequence.Count; i++)
		{
			array[i + 1] = sequence[i];
		}
		return new CachedPlaybackSteps(array);
	}

	public CombatBodySequenceRestorePlan BuildRestorePlan(IReadOnlyList<CombatPresentationBodyStep> sequence, string? loopAnimation, Func<string?, bool> ownsIdleEffect)
	{
		CachedSequenceMetrics metrics = GetMetrics(sequence);
		float delaySeconds = metrics.DelaySeconds;
		float num = Math.Max(delaySeconds, metrics.TransientEffectChainDurationSeconds);
		bool flag = !string.IsNullOrEmpty(loopAnimation);
		bool flag2 = ownsIdleEffect(loopAnimation);
		return new CombatBodySequenceRestorePlan(IdleEffectsDelaySeconds: flag2 ? (num + _readyToIdleEffectDelaySeconds) : 0f, BodyDelaySeconds: delaySeconds, EffectSafeDelaySeconds: num, LoopAnimation: flag ? loopAnimation : null, ShouldScheduleIdleEffects: flag2);
	}

	public float GetSequenceDelaySeconds(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		return GetMetrics(steps).DelaySeconds;
	}

	public IReadOnlyList<CombatBodySequenceStepSchedule> BuildStepSchedule(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		return _stepSchedules.GetValue(steps, BuildCachedStepSchedule).Steps;
	}

	public float GetSequenceDelaySeconds(IReadOnlyList<string> animations)
	{
		float num = 0f;
		foreach (string animation in animations)
		{
			num += GetAnimationDelaySeconds(animation);
		}
		return Math.Max(0.05f, num);
	}

	public float GetAnimationDelaySeconds(string animationName)
	{
		if (_animationDelaySeconds.TryGetValue(animationName, out var value))
		{
			return value;
		}
		_profile.TryGetBodyAnimationTiming(animationName, out CombatPresentationAnimationTiming timing);
		if (timing == null)
		{
			_animationDelaySeconds[animationName] = 0.05f;
			if (_missingAnimationTimings.Add(animationName))
			{
				SkinLog.Warn("Missing combat body animation timing for '" + animationName + "'; using fail-open delay=" + 0.05f.ToString("0.###") + "s.");
			}
			return 0.05f;
		}
		_animationDelaySeconds[animationName] = timing.DurationSeconds;
		return timing.DurationSeconds;
	}

	public float GetTransientEffectChainDurationSeconds(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		return GetMetrics(steps).TransientEffectChainDurationSeconds;
	}

	public float GetEffectSafeDelaySeconds(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		CachedSequenceMetrics metrics = GetMetrics(steps);
		return Math.Max(metrics.DelaySeconds, metrics.TransientEffectChainDurationSeconds);
	}

	public CombatBodyLoopRestorePlan BuildLoopRestorePlan(IReadOnlyList<CombatPresentationBodyStep> steps, string? loopAnimation, string readyLoopAnimation, float postActionReadyDwellSeconds, float idleEffectRestorePadSeconds, Func<string?, bool> ownsIdleEffect, Func<string?, string?, bool> routeIdleThroughReadyLoop, Func<IReadOnlyList<CombatPresentationBodyStep>, bool>? useBodyDelayForIdleEffectRestore = null, bool suppressReadyLoopRoutingWhenLastIsReadyExit = true, float? readyLoopRestoreBaseDelaySeconds = null)
	{
		if (steps.Count == 0 || string.IsNullOrEmpty(loopAnimation))
		{
			return CombatBodyLoopRestorePlan.None;
		}
		string bodyAnimation = steps[0].BodyAnimation;
		string bodyAnimation2 = steps[steps.Count - 1].BodyAnimation;
		int num;
		string? text;
		if (routeIdleThroughReadyLoop(loopAnimation, bodyAnimation))
		{
			if (suppressReadyLoopRoutingWhenLastIsReadyExit)
			{
				num = ((!string.Equals(bodyAnimation2, _readyExitAnimation, StringComparison.Ordinal)) ? 1 : 0);
				if (num == 0)
				{
					goto IL_005f;
				}
			}
			else
			{
				num = 1;
			}
			text = readyLoopAnimation;
			goto IL_0063;
		}
		num = 0;
		goto IL_005f;
		IL_0063:
		string loopAnimation2 = text;
		if (num != 0)
		{
			return new CombatBodyLoopRestorePlan(loopAnimation2, RoutesIdleThroughReadyLoop: true, ShouldScheduleIdleRestoreAfterAction: true, Math.Max(0f, readyLoopRestoreBaseDelaySeconds ?? GetEffectSafeDelaySeconds(steps)) + Math.Max(0f, postActionReadyDwellSeconds), ShouldScheduleIdleEffectsRestore: false, 0f, null);
		}
		if (ownsIdleEffect(loopAnimation))
		{
			float sequenceDelaySeconds = GetSequenceDelaySeconds(steps);
			float idleEffectsDelaySeconds = ((useBodyDelayForIdleEffectRestore != null && useBodyDelayForIdleEffectRestore(steps)) ? sequenceDelaySeconds : Math.Max(_readyToIdleEffectDelaySeconds, sequenceDelaySeconds + Math.Max(0f, idleEffectRestorePadSeconds)));
			return new CombatBodyLoopRestorePlan(loopAnimation2, RoutesIdleThroughReadyLoop: false, ShouldScheduleIdleRestoreAfterAction: false, 0f, ShouldScheduleIdleEffectsRestore: true, idleEffectsDelaySeconds, loopAnimation);
		}
		return new CombatBodyLoopRestorePlan(loopAnimation2, RoutesIdleThroughReadyLoop: false, ShouldScheduleIdleRestoreAfterAction: false, 0f, ShouldScheduleIdleEffectsRestore: false, 0f, null);
		IL_005f:
		text = loopAnimation;
		goto IL_0063;
	}

	public string FormatSequence(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		return _sequenceFormats.GetValue(steps, BuildSequenceFormat).FormattedSequence;
	}

	private CachedSequenceMetrics GetMetrics(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		return _sequenceMetrics.GetValue(steps, BuildSequenceMetrics);
	}

	private CachedSequenceMetrics BuildSequenceMetrics(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		float num = 0f;
		float num2 = 0f;
		for (int i = 0; i < steps.Count; i++)
		{
			CombatPresentationBodyStep combatPresentationBodyStep = steps[i];
			if (_profile.TryGetTransientEffects(combatPresentationBodyStep.EffectiveCueAnimation, out IReadOnlyList<CombatPresentationEffectCue> effects))
			{
				foreach (CombatPresentationEffectCue item in effects)
				{
					float num3 = num + item.StartDelaySeconds + item.LifetimeSeconds;
					if (num3 > num2)
					{
						num2 = num3;
					}
				}
			}
			num += GetStepDelaySeconds(combatPresentationBodyStep);
		}
		return new CachedSequenceMetrics(Math.Max(0.05f, num), num2);
	}

	private CachedStepSchedule BuildCachedStepSchedule(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		CombatBodySequenceStepSchedule[] array = new CombatBodySequenceStepSchedule[steps.Count];
		float num = 0f;
		for (int i = 0; i < steps.Count; i++)
		{
			CombatPresentationBodyStep step = steps[i];
			array[i] = new CombatBodySequenceStepSchedule(step, num);
			num += GetStepDelaySeconds(step);
		}
		return new CachedStepSchedule(array);
	}

	private float GetStepDelaySeconds(CombatPresentationBodyStep step)
	{
		if (!step.DelaySeconds.HasValue)
		{
			return GetAnimationDelaySeconds(step.BodyAnimation);
		}
		return Math.Max(0.05f, step.DelaySeconds.Value);
	}

	private static CachedSequenceFormat BuildSequenceFormat(IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		string[] array = new string[steps.Count];
		for (int i = 0; i < steps.Count; i++)
		{
			CombatPresentationBodyStep combatPresentationBodyStep = steps[i];
			array[i] = (string.Equals(combatPresentationBodyStep.BodyAnimation, combatPresentationBodyStep.EffectiveCueAnimation, StringComparison.Ordinal) ? combatPresentationBodyStep.BodyAnimation : (combatPresentationBodyStep.BodyAnimation + "[" + combatPresentationBodyStep.EffectiveCueAnimation + "]"));
		}
		return new CachedSequenceFormat(string.Join(" -> ", array));
	}
}
