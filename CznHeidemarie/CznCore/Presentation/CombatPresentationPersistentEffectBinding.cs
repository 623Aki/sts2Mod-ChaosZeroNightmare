namespace CznCore.Presentation;

internal sealed record CombatPresentationPersistentEffectBinding(string EffectId, bool RestartsWhenRequested = true, CombatPresentationPersistentEffectPlacement? Placement = null);
