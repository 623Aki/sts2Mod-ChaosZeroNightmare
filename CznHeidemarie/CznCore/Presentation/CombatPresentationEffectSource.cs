using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationEffectSource(CombatPresentationEffectSourceKind Kind, string? ScenePath, IReadOnlyList<CombatPresentationEffectLayer> Layers)
{
	public static CombatPresentationEffectSource Scene(string scenePath)
	{
		return new CombatPresentationEffectSource(CombatPresentationEffectSourceKind.Scene, scenePath, Array.Empty<CombatPresentationEffectLayer>());
	}

	public static CombatPresentationEffectSource RawSpineLayers(IReadOnlyList<CombatPresentationEffectLayer> layers)
	{
		return new CombatPresentationEffectSource(CombatPresentationEffectSourceKind.RawSpineLayers, null, layers);
	}
}
