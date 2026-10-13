namespace CznCore.Presentation;

internal sealed record CombatPresentationEffectLayer(string Name, string AtlasPath, string SkeletonPath, CombatPresentationEffectLayerPlacement Placement = CombatPresentationEffectLayerPlacement.Normal);
