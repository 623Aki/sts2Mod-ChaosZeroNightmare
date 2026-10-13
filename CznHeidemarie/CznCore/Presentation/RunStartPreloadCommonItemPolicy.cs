namespace CznCore.Presentation;

internal sealed record RunStartPreloadCommonItemPolicy(bool IncludeBodyResources, bool IncludeIdleEffects, bool IncludeTransientEffectCues)
{
	public static RunStartPreloadCommonItemPolicy Default { get; } = new RunStartPreloadCommonItemPolicy(IncludeBodyResources: true, IncludeIdleEffects: true, IncludeTransientEffectCues: true);

	public static RunStartPreloadCommonItemPolicy BodyAndIdleEffectsOnly { get; } = new RunStartPreloadCommonItemPolicy(IncludeBodyResources: true, IncludeIdleEffects: true, IncludeTransientEffectCues: false);

	public static RunStartPreloadCommonItemPolicy BodyOnly { get; } = new RunStartPreloadCommonItemPolicy(IncludeBodyResources: true, IncludeIdleEffects: false, IncludeTransientEffectCues: false);
}
