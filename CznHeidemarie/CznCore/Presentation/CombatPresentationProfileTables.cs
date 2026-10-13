using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatPresentationProfileTables
{
	private readonly IReadOnlyDictionary<string, CombatPresentationState> _states;

	private readonly IReadOnlyDictionary<CombatPresentationCardRoute, CombatPresentationRoutePlan> _routePlans;

	private readonly IReadOnlyDictionary<CombatPresentationCardRoute, CombatPresentationAttackPlan> _attackPlans;

	private readonly IReadOnlyDictionary<string, CombatPresentationAnimationTiming> _bodyAnimationTimings;

	private readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationPersistentEffectBinding>> _persistentEffectBindings;

	private readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationEffectCue>> _transientEffects;

	private readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationSfxCue>> _sfxCues;

	private readonly Func<string, bool>? _shouldExposeTransientEffectKey;

	private readonly Func<string, bool>? _shouldExposeSfxKey;

	private static readonly IReadOnlyDictionary<CombatPresentationCardRoute, CombatPresentationAttackPlan> EmptyAttackPlans = new Dictionary<CombatPresentationCardRoute, CombatPresentationAttackPlan>();

	private static readonly IReadOnlyDictionary<string, CombatPresentationAnimationTiming> EmptyBodyAnimationTimings = new Dictionary<string, CombatPresentationAnimationTiming>(StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationPersistentEffectBinding>> EmptyPersistentEffectBindings = new Dictionary<string, IReadOnlyList<CombatPresentationPersistentEffectBinding>>(StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationEffectCue>> EmptyTransientEffects = new Dictionary<string, IReadOnlyList<CombatPresentationEffectCue>>(StringComparer.Ordinal);

	private static readonly IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationSfxCue>> EmptySfxCues = new Dictionary<string, IReadOnlyList<CombatPresentationSfxCue>>(StringComparer.Ordinal);

	public CombatPresentationProfileTables(IReadOnlyDictionary<string, CombatPresentationState> states, IReadOnlyDictionary<CombatPresentationCardRoute, CombatPresentationRoutePlan> routePlans, IReadOnlyDictionary<CombatPresentationCardRoute, CombatPresentationAttackPlan>? attackPlans = null, IReadOnlyDictionary<string, CombatPresentationAnimationTiming>? bodyAnimationTimings = null, IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationPersistentEffectBinding>>? persistentEffectBindings = null, IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationEffectCue>>? transientEffects = null, IReadOnlyDictionary<string, IReadOnlyList<CombatPresentationSfxCue>>? sfxCues = null, Func<string, bool>? shouldExposeTransientEffectKey = null, Func<string, bool>? shouldExposeSfxKey = null)
	{
		_states = states ?? throw new ArgumentNullException("states");
		_routePlans = routePlans ?? throw new ArgumentNullException("routePlans");
		_attackPlans = attackPlans ?? EmptyAttackPlans;
		_bodyAnimationTimings = bodyAnimationTimings ?? EmptyBodyAnimationTimings;
		_persistentEffectBindings = persistentEffectBindings ?? EmptyPersistentEffectBindings;
		_transientEffects = transientEffects ?? EmptyTransientEffects;
		_sfxCues = sfxCues ?? EmptySfxCues;
		_shouldExposeTransientEffectKey = shouldExposeTransientEffectKey;
		_shouldExposeSfxKey = shouldExposeSfxKey;
	}

	public bool TryGetState(string stateId, out CombatPresentationState state)
	{
		return _states.TryGetValue(stateId, out state);
	}

	public bool TryGetRoutePlan(CombatPresentationCardRoute route, out CombatPresentationRoutePlan plan)
	{
		return _routePlans.TryGetValue(route, out plan);
	}

	public bool TryGetAttackPlan(CombatPresentationCardRoute route, out CombatPresentationAttackPlan plan)
	{
		return _attackPlans.TryGetValue(route, out plan);
	}

	public bool TryGetBodyAnimationTiming(string animationName, out CombatPresentationAnimationTiming timing)
	{
		return _bodyAnimationTimings.TryGetValue(animationName, out timing);
	}

	public bool TryGetPersistentEffectBindings(string animationName, out IReadOnlyList<CombatPresentationPersistentEffectBinding> effects)
	{
		return _persistentEffectBindings.TryGetValue(animationName, out effects);
	}

	public bool TryGetTransientEffects(string animationName, out IReadOnlyList<CombatPresentationEffectCue> effects)
	{
		return _transientEffects.TryGetValue(animationName, out effects);
	}

	public bool TryGetSfxCues(string animationName, out IReadOnlyList<CombatPresentationSfxCue> sfxCues)
	{
		return _sfxCues.TryGetValue(animationName, out sfxCues);
	}

	public IEnumerable<CombatPresentationEffectCue> EnumerateTransientEffectCues()
	{
		foreach (var (arg, readOnlyList2) in _transientEffects)
		{
			if (_shouldExposeTransientEffectKey != null && !_shouldExposeTransientEffectKey(arg))
			{
				continue;
			}
			foreach (CombatPresentationEffectCue item in readOnlyList2)
			{
				yield return item;
			}
		}
	}

	public IEnumerable<CombatPresentationSfxCue> EnumerateSfxCues(bool includeRoutePlanSfx = true)
	{
		if (includeRoutePlanSfx)
		{
			foreach (CombatPresentationRoutePlan value in _routePlans.Values)
			{
				if (value.SfxCues == null)
				{
					continue;
				}
				foreach (CombatPresentationSfxCue sfxCue in value.SfxCues)
				{
					yield return sfxCue;
				}
			}
		}
		foreach (var (arg, readOnlyList2) in _sfxCues)
		{
			if (_shouldExposeSfxKey != null && !_shouldExposeSfxKey(arg))
			{
				continue;
			}
			foreach (CombatPresentationSfxCue item in readOnlyList2)
			{
				yield return item;
			}
		}
	}
}
