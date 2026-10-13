using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record BattleReadyOverlayProfile(string RuntimeNodeNamePrefix, string ScenePath, string SkeletonDataPath, BattleReadyOverlayAnimations Animations, float OutDelaySeconds, float CancelOutDelaySeconds, float SlideOffsetX, float SlideDurationSeconds, BattleReadyOverlayIdleEffectPlan? IdleEffect, IReadOnlyDictionary<CombatPresentationCardRoute, string> CardAnimations, float CardUseOutDelaySeconds = -1f, float? CardUseSlideDurationSeconds = null)
{
	public string RuntimeNodeName(string suffix)
	{
		return RuntimeNodeNamePrefix + suffix;
	}

	public bool TryGetCardAnimation(CombatPresentationCardRoute route, out string animationName)
	{
		return CardAnimations.TryGetValue(route, out animationName);
	}
}
