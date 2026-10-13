using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationState(string Id, IReadOnlyList<CombatPresentationBodyStep> BodySteps, string? LoopAnimation, bool ClearManualAttackChain = true)
{
	public bool HasBodyAnimation => BodySteps.Count > 0;
}
