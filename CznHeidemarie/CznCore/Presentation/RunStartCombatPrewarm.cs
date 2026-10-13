using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal static class RunStartCombatPrewarm
{
	public static async Task WarmEntryBodyAsync(Node host, string characterLabel, CharacterSkinProfile profile, Func<bool> bodyPresentationEnabled)
	{
		ulong startedMs = Time.GetTicksMsec();
		try
		{
			if (bodyPresentationEnabled())
			{
				await CombatBodyRenderWarmup.WarmAsync(host, profile);
			}
			SkinLog.Info(characterLabel + " combat entry run-start prewarm completed elapsedMs=" + (Time.GetTicksMsec() - startedMs));
		}
		catch (Exception ex)
		{
			SkinLog.Warn(characterLabel + " combat entry run-start prewarm failed elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
	}

	public static async Task PrewarmSfxCuesAsync(Node host, string characterLabel, ICombatPresentationProfile presentation, IReadOnlyList<string> animationNames, ICombatWarmupHost warmupHost)
	{
		ulong startedMs = Time.GetTicksMsec();
		int warmed = 0;
		try
		{
			if (!warmupHost.AreEffectsEnabled() || !warmupHost.IsSfxEnabled())
			{
				return;
			}
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string animationName in animationNames)
			{
				if (!presentation.TryGetSfxCues(animationName, out IReadOnlyList<CombatPresentationSfxCue> sfxCues))
				{
					continue;
				}
				foreach (CombatPresentationSfxCue cue in sfxCues)
				{
					if (string.IsNullOrWhiteSpace(cue.Path) || !seen.Add(cue.Path))
					{
						continue;
					}
					await RunStartResourcePreloader.PreloadResource(host, cue.Path);
					for (int retry = 0; retry < 4; retry++)
					{
						if (warmupHost.PrewarmSfx(cue.Path))
						{
							break;
						}
						await RunStartResourcePreloader.AwaitFrame(host);
					}
					warmed++;
					await RunStartResourcePreloader.AwaitFrame(host);
				}
			}
			SkinLog.Info(characterLabel + " combat SFX run-start prewarm completed items=" + warmed + " elapsedMs=" + (Time.GetTicksMsec() - startedMs));
		}
		catch (Exception ex)
		{
			SkinLog.Warn(characterLabel + " combat SFX run-start prewarm failed elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
	}

	public static async Task PrewarmEffectPipelinesAsync(Node host, string characterLabel, ICombatPresentationProfile presentation, IReadOnlyList<string> animationNames, ICombatWarmupHost warmupHost, CombatWarmupRuntime warmups)
	{
		ulong startedMs = Time.GetTicksMsec();
		int warmed = 0;
		int skipped = 0;
		try
		{
			if (!warmupHost.AreEffectsEnabled())
			{
				return;
			}
			HashSet<string> seenPoolKeys = new HashSet<string>(StringComparer.Ordinal);
			foreach (string animationName in animationNames)
			{
				if (!warmupHost.AreEffectsEnabled())
				{
					break;
				}
				if (!presentation.TryGetTransientEffects(animationName, out IReadOnlyList<CombatPresentationEffectCue> effects))
				{
					continue;
				}
				foreach (CombatPresentationEffectCue item in effects)
				{
					if (warmupHost.AreEffectsEnabled())
					{
						if (!(await PrewarmEffectPipelineCue(host, item, seenPoolKeys, warmupHost, warmups)))
						{
							skipped++;
							continue;
						}
						warmed++;
						await RunStartResourcePreloader.AwaitFrame(host);
						continue;
					}
					break;
				}
			}
			SkinLog.Info(characterLabel + " combat effect pipeline run-start prewarm completed warmed=" + warmed + " skipped=" + skipped + " elapsedMs=" + (Time.GetTicksMsec() - startedMs));
		}
		catch (Exception ex)
		{
			SkinLog.Warn(characterLabel + " combat effect pipeline run-start prewarm failed warmed=" + warmed + " skipped=" + skipped + " elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
	}

	private static async Task<bool> PrewarmEffectPipelineCue(Node host, CombatPresentationEffectCue cue, HashSet<string> seenPoolKeys, ICombatWarmupHost warmupHost, CombatWarmupRuntime warmups)
	{
		if (cue.Source.Kind != CombatPresentationEffectSourceKind.Scene || string.IsNullOrWhiteSpace(cue.Source.ScenePath) || !warmupHost.AreEffectsEnabled())
		{
			return false;
		}
		string poolKey = warmupHost.GetPoolKey(cue);
		int targetDepth = Math.Max(1, warmups.GetStarterEffectPoolWarmupTargetCount(cue));
		if (!seenPoolKeys.Add(poolKey) || warmupHost.GetUsablePooledCount(poolKey) >= targetDepth)
		{
			return true;
		}
		for (int retry = 0; retry < 8; retry++)
		{
			if (!warmupHost.AreEffectsEnabled())
			{
				break;
			}
			if (warmupHost.PreloadScene(cue.Source.ScenePath, allowSynchronousFallback: false))
			{
				break;
			}
			await RunStartResourcePreloader.AwaitFrame(host);
		}
		if (!warmupHost.AreEffectsEnabled())
		{
			return false;
		}
		PackedScene scene = warmupHost.LoadScene(cue.Source.ScenePath);
		if (scene == null)
		{
			return false;
		}
		bool result = false;
		for (int retry = 0; retry < targetDepth + 2; retry++)
		{
			if (!warmupHost.AreEffectsEnabled())
			{
				break;
			}
			if (warmupHost.GetUsablePooledCount(poolKey) >= targetDepth)
			{
				break;
			}
			Node2D effectRoot = warmupHost.AcquireRoot(cue, scene, warmup: false);
			if (effectRoot == null)
			{
				break;
			}
			if (!warmupHost.PrimeRoot(effectRoot, cue))
			{
				warmupHost.ReleaseRoot(effectRoot);
				break;
			}
			await RunStartResourcePreloader.AwaitFrame(host);
			await RunStartResourcePreloader.AwaitFrame(host);
			warmupHost.ReleaseRoot(effectRoot);
			result = true;
		}
		return result;
	}
}
