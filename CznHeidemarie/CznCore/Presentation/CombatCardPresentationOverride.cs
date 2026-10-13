using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatCardPresentationOverride(string Id, IReadOnlyList<string> CardIdentifiers, CombatPresentationCardRoute? Route = null, CombatPresentationRoutePlan? RoutePlan = null, CombatPresentationAttackPlan? AttackPlan = null, string? CutInStateId = null, CombatPresentationVoiceCue? AttackVoiceCue = null, float AttackOpeningHitDelaySeconds = 0f);
