using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatTargetLayerSandwichCues(ICombatPresentationProfile profile)
{
	private readonly ICombatPresentationProfile _profile = profile ?? throw new ArgumentNullException("profile");

	private readonly Dictionary<string, bool> _cache = new Dictionary<string, bool>(StringComparer.Ordinal);

	public bool CueUsesScreenTargetBack(string animationName)
	{
		if (string.IsNullOrEmpty(animationName))
		{
			return false;
		}
		if (_cache.TryGetValue(animationName, out var value))
		{
			return value;
		}
		value = false;
		if (_profile.TryGetTransientEffects(animationName, out IReadOnlyList<CombatPresentationEffectCue> effects))
		{
			value = CuesUseScreenTargetBack(effects);
		}
		_cache[animationName] = value;
		return value;
	}

	public bool StepsUseScreenTargetBack(IReadOnlyList<CombatPresentationBodyStep>? steps)
	{
		if (steps == null || steps.Count == 0)
		{
			return false;
		}
		foreach (CombatPresentationBodyStep step in steps)
		{
			if (CueUsesScreenTargetBack(step.EffectiveCueAnimation))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CuesUseScreenTargetBack(IReadOnlyList<CombatPresentationEffectCue>? cues)
	{
		if (cues == null || cues.Count == 0)
		{
			return false;
		}
		foreach (CombatPresentationEffectCue cue in cues)
		{
			CombatPresentationEffectAnchor anchor = cue.Anchor;
			if ((anchor == CombatPresentationEffectAnchor.ScreenTargetBack || anchor == CombatPresentationEffectAnchor.ScreenTargetSurfaceBack) ? true : false)
			{
				return true;
			}
		}
		return false;
	}
}
