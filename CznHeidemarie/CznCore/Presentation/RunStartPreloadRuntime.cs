using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed class RunStartPreloadRuntime
{
	private static readonly object ConfigureSync = new object();

	private static RunStartPreloadRuntime? _current;

	private readonly RunStartPreloadProfile _profile;

	public static RunStartPreloadRuntime? Current
	{
		get
		{
			lock (ConfigureSync)
			{
				return _current;
			}
		}
	}

	public string ProfileId => _profile.SkinProfile.ProfileId;

	private RunStartPreloadRuntime(RunStartPreloadProfile profile)
	{
		_profile = profile;
	}

	public static void Configure(RunStartPreloadProfile profile)
	{
		lock (ConfigureSync)
		{
			_current = new RunStartPreloadRuntime(profile);
		}
	}

	public bool AppliesTo(CharacterModel? character)
	{
		if (character != null)
		{
			return _profile.SkinProfile.AppliesTo(character);
		}
		return false;
	}

	public void ScheduleCharacterSelectEntryWarmup(Node host, CharacterModel? character, string context)
	{
		try
		{
			if (host != null && GodotObject.IsInstanceValid(host) && host.IsInsideTree() && _profile.SkinProfile.Features.CombatEntryAnimation && AppliesTo(character))
			{
				TaskHelper.RunSafely(RunCharacterSelectEntryWarmup(host, context));
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn(context + " entry warmup scheduling failed: " + SkinLog.FormatException(ex));
		}
	}

	private async Task RunCharacterSelectEntryWarmup(Node host, string context)
	{
		ulong startedMs = Time.GetTicksMsec();
		try
		{
			await CombatBodyRenderWarmup.WarmAsync(host, _profile.SkinProfile);
			SkinLog.Info(context + " entry warmup completed elapsedMs=" + (Time.GetTicksMsec() - startedMs));
		}
		catch (Exception ex)
		{
			SkinLog.Warn(context + " entry warmup failed elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
	}

	public async Task RunPreload(Node host, string context)
	{
		if (ShouldUseLimitedRunStartPreload())
		{
			await RunLimitedPreload(host, context);
			return;
		}
		RunStartLoadingOverlay overlay = null;
		ulong startedMs = Time.GetTicksMsec();
		int preloadItemCount = 0;
		try
		{
			overlay = RunStartLoadingOverlay.TryCreate(host, _profile);
			overlay?.SetProgress(0f, _profile.PreparingStatus);
			await RunStartResourcePreloader.AwaitFrame(host);
			IReadOnlyList<RunStartPreloadItem> items = _profile.BuildItems();
			preloadItemCount = items.Count;
			SkinLog.Info(context + " preload started items=" + preloadItemCount);
			if (items.Count == 0)
			{
				overlay?.SetProgress(1f, _profile.ReadyStatus);
			}
			else
			{
				float itemProgressEnd = Math.Clamp(_profile.ItemProgressEnd, 0.01f, 1f);
				for (int i = 0; i < items.Count; i++)
				{
					RunStartPreloadItem item = items[i];
					float progress = itemProgressEnd * (float)i / (float)items.Count;
					overlay?.SetProgress(progress, item.Label);
					await PreloadItem(host, item);
					float progress2 = itemProgressEnd * (float)(i + 1) / (float)items.Count;
					overlay?.SetProgress(progress2, item.Label);
					await RunStartResourcePreloader.AwaitFrame(host);
				}
			}
			foreach (RunStartPreloadStep finalStep in _profile.FinalSteps)
			{
				if (finalStep.ShouldRun())
				{
					overlay?.SetProgress(finalStep.Progress, finalStep.Status);
					await finalStep.ExecuteAsync(host);
					await RunStartResourcePreloader.AwaitFrame(host);
				}
			}
			overlay?.SetProgress(1f, _profile.ReadyStatus);
			SkinLog.Info(context + " preload completed items=" + preloadItemCount + " elapsedMs=" + (Time.GetTicksMsec() - startedMs));
			await AwaitMinimumVisibility(host, startedMs);
		}
		catch (Exception ex)
		{
			SkinLog.Warn(context + " preload failed; continuing native flow items=" + preloadItemCount + " elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
		finally
		{
			overlay?.Close();
		}
	}

	private static async Task PreloadItem(Node host, RunStartPreloadItem item)
	{
		_ = 1;
		try
		{
			if (item.PreloadAsync != null)
			{
				await item.PreloadAsync(host);
			}
			else
			{
				await RunStartResourcePreloader.PreloadResource(host, item.Path);
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Run-start preload skipped item " + item.Label + " " + item.Path + ": " + SkinLog.FormatException(ex), 3);
		}
	}

	private async Task AwaitMinimumVisibility(Node host, ulong startedMs)
	{
		while ((float)(Time.GetTicksMsec() - startedMs) / 1000f < _profile.MinimumVisibleSeconds)
		{
			await RunStartResourcePreloader.AwaitFrame(host);
		}
	}

	private async Task RunLimitedPreload(Node host, string context)
	{
		RunStartLoadingOverlay overlay = null;
		ulong startedMs = Time.GetTicksMsec();
		int stepsRun = 0;
		try
		{
			overlay = RunStartLoadingOverlay.TryCreate(host, _profile);
			overlay?.SetProgress(0f, _profile.PreparingStatus);
			await RunStartResourcePreloader.AwaitFrame(host);
			foreach (RunStartPreloadStep finalStep in _profile.FinalSteps)
			{
				if (finalStep.RunDuringLimitedPreload && finalStep.ShouldRun())
				{
					overlay?.SetProgress(finalStep.Progress, finalStep.Status);
					await finalStep.ExecuteAsync(host);
					stepsRun++;
					await RunStartResourcePreloader.AwaitFrame(host);
				}
			}
			overlay?.SetProgress(1f, _profile.ReadyStatus);
			SkinLog.Info(context + " preload used limited resource path steps=" + stepsRun + " elapsedMs=" + (Time.GetTicksMsec() - startedMs));
			if (stepsRun > 0)
			{
				await AwaitMinimumVisibility(host, startedMs);
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn(context + " limited preload failed; continuing native flow steps=" + stepsRun + " elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
		}
		finally
		{
			overlay?.Close();
		}
	}

	private static bool ShouldUseLimitedRunStartPreload()
	{
		return CznRuntimeCapabilities.RequiresLowPressureSpineWarmupPolicy;
	}
}
