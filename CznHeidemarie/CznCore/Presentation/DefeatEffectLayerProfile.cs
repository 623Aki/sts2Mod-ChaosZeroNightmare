namespace CznCore.Presentation;

internal sealed record DefeatEffectLayerProfile(string Name, string AtlasPath, string SkeletonPath, string SkeletonDataPath, string PngPath, int ZIndex, float Alpha, float VisibleSeconds, float FallbackScale, float RuntimeScale = 1f);
