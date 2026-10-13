using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation.Vfx;

internal sealed class OverlayStageScenePreloadSpec
{
	public IReadOnlyList<string> ScenePaths { get; }

	public float StaggerSeconds { get; init; } = 0.15f;

	public string TypeHint { get; init; } = string.Empty;

	public bool UseSubThreads { get; init; } = CznRuntimeCapabilities.UseSubThreadsForWarmupResourceLoads;

	public ResourceLoader.CacheMode CacheMode { get; init; } = ResourceLoader.CacheMode.Reuse;

	public OverlayStageScenePreloadSpec(IReadOnlyList<string> scenePaths)
	{
		ScenePaths = scenePaths ?? Array.Empty<string>();
	}
}
