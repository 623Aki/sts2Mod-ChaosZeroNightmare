using System.Collections.Generic;

namespace CznCore.Presentation;

internal interface ICombatPresentationRuntime<TActor> where TActor : class
{
	int NextToken(TActor? actor);

	void ClearManualAttackChain(TActor? actor);

	void PlayBodyAnimation(TActor? actor, CombatPresentationBodyStep step, string? loopAnimation, int token);

	void PlayBodySequence(TActor? actor, IReadOnlyList<CombatPresentationBodyStep> steps, string? loopAnimation, int token);
}
