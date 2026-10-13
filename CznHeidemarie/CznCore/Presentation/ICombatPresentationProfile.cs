using System.Collections.Generic;

namespace CznCore.Presentation;

internal interface ICombatPresentationProfile : ICombatPresentationRoutePlanResolver<CombatPresentationCardContext>
{
	CombatPresentationCommonStateIds CommonStateIds { get; }

	bool TryGetState(string stateId, out CombatPresentationState state);

	bool TryGetRoutePlan(CombatPresentationCardRoute route, out CombatPresentationRoutePlan plan);

	bool TryGetAttackPlan(CombatPresentationCardContext context, out CombatPresentationAttackPlan plan);

	bool TryGetBodyAnimationTiming(string animationName, out CombatPresentationAnimationTiming timing);

	bool TryGetPersistentEffectBindings(string animationName, out IReadOnlyList<CombatPresentationPersistentEffectBinding> effects);

	bool TryGetTransientEffects(string animationName, out IReadOnlyList<CombatPresentationEffectCue> effects);

	bool TryGetSfxCues(string animationName, out IReadOnlyList<CombatPresentationSfxCue> sfxCues);

	IEnumerable<CombatPresentationEffectCue> EnumerateTransientEffectCues();

	IEnumerable<CombatPresentationSfxCue> EnumerateSfxCues();
}
