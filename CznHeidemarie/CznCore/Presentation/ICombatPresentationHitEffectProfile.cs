using System.Collections.Generic;

namespace CznCore.Presentation;

internal interface ICombatPresentationHitEffectProfile
{
	bool TryGetHitEffects(CombatPresentationHitEffectContext context, out IReadOnlyList<CombatPresentationEffectCue> effects);
}
