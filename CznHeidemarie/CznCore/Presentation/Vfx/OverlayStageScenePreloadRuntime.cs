using System;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation.Vfx;

internal static class OverlayStageScenePreloadRuntime
{
	public static void ScheduleThreadedRequests(OverlayStageScenePreloadSpec spec, Func<bool> isCurrent, Action? onStart = null, Action? onComplete = null, Action<Error, string>? onRequestFailed = null)
	{
		if (spec == null)
		{
			throw new ArgumentNullException("spec");
		}
		if (isCurrent == null)
		{
			throw new ArgumentNullException("isCurrent");
		}
		if (spec.ScenePaths.Count == 0)
		{
			return;
		}
		onStart?.Invoke();
		float num = Math.Max(0f, spec.StaggerSeconds);
		for (int i = 0; i < spec.ScenePaths.Count; i++)
		{
			string path = spec.ScenePaths[i];
			bool isLast = i == spec.ScenePaths.Count - 1;
			GodotScheduler.Delay(num * (float)(i + 1), () =>
			{
				if (isCurrent())
				{
					if (string.IsNullOrWhiteSpace(path))
					{
						if (isLast)
						{
							onComplete?.Invoke();
						}
					}
					else
					{
						Error error = ResourceLoader.LoadThreadedRequest(path, spec.TypeHint, spec.UseSubThreads, spec.CacheMode);
						if (error != Error.Ok)
						{
							onRequestFailed?.Invoke(error, path);
						}
						if (isLast)
						{
							onComplete?.Invoke();
						}
					}
				}
			});
		}
	}

	public static void ScheduleMainThreadCacheLoads(OverlayStageScenePreloadSpec spec, Func<bool> isCurrent, Action? onStart = null, Action? onComplete = null, Action<Error, string>? onLoadFailed = null)
	{
		if (spec == null)
		{
			throw new ArgumentNullException("spec");
		}
		if (isCurrent == null)
		{
			throw new ArgumentNullException("isCurrent");
		}
		if (spec.ScenePaths.Count == 0)
		{
			return;
		}
		onStart?.Invoke();
		float num = Math.Max(0f, spec.StaggerSeconds);
		for (int i = 0; i < spec.ScenePaths.Count; i++)
		{
			string path = spec.ScenePaths[i];
			bool isLast = i == spec.ScenePaths.Count - 1;
			GodotScheduler.Delay(num * (float)(i + 1), () =>
			{
				if (isCurrent())
				{
					if (string.IsNullOrWhiteSpace(path))
					{
						if (isLast)
						{
							onComplete?.Invoke();
						}
					}
					else
					{
						try
						{
							if (ResourceLoader.Load<Resource>(path, spec.TypeHint, spec.CacheMode) == null)
							{
								onLoadFailed?.Invoke(Error.Failed, path);
							}
						}
						catch (Exception)
						{
							onLoadFailed?.Invoke(Error.Failed, path);
						}
						if (isLast)
						{
							onComplete?.Invoke();
						}
					}
				}
			});
		}
	}
}
