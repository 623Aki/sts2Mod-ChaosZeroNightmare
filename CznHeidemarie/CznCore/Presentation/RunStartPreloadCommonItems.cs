using System;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal static class RunStartPreloadCommonItems
{
	public static void Add(RunStartPreloadItemBuilder items, RunStartPreloadCommonItemSources sources)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		ArgumentNullException.ThrowIfNull(sources, "sources");
		CharacterSkinProfile skinProfile = sources.SkinProfile;
		RunStartPreloadCommonItemPolicy policy = sources.Policy;
		if (policy.IncludeBodyResources)
		{
			Func<Node, Task> preloadBodyTexture = sources.PreloadBodyTexture;
			if (preloadBodyTexture != null)
			{
				items.AddCustom(skinProfile.BodyTexturePath, skinProfile.BodyTexturePath, "Body texture", preloadBodyTexture);
				goto IL_0069;
			}
		}
		if (policy.IncludeBodyResources)
		{
			items.AddResource(skinProfile.BodyTexturePath, "Body texture");
		}
		goto IL_0069;
		IL_0069:
		if (policy.IncludeBodyResources)
		{
			items.AddResource(skinProfile.BodySkeletonDataPath, "Body skeleton");
			items.AddResource(skinProfile.PremultipliedAlphaMaterialPath, "Body material");
		}
		if (policy.IncludeIdleEffects && (sources.IdleEffectsEnabled ?? sources.EffectsEnabled)())
		{
			Func<Node, Task> preloadIdleEffectsScene = sources.PreloadIdleEffectsScene;
			if (preloadIdleEffectsScene != null)
			{
				items.AddCustom(skinProfile.CombatIdleEffectsScenePath, skinProfile.CombatIdleEffectsScenePath, "Idle effects", preloadIdleEffectsScene);
			}
			else
			{
				items.AddResource(skinProfile.CombatIdleEffectsScenePath, "Idle effects");
			}
		}
		if (!sources.EffectsEnabled())
		{
			return;
		}
		sources.AddBeforeEffectCues?.Invoke(items);
		if (!policy.IncludeTransientEffectCues)
		{
			return;
		}
		foreach (CombatPresentationEffectCue item in skinProfile.CombatPresentation.EnumerateTransientEffectCues())
		{
			Action<RunStartPreloadItemBuilder, CombatPresentationEffectCue> addEffectCue = sources.AddEffectCue;
			if (addEffectCue != null)
			{
				addEffectCue(items, item);
			}
			else
			{
				items.AddEffectSource(item.Source, "Combat effect");
			}
		}
	}
}
