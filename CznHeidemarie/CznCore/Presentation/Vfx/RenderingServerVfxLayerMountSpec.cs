using Godot;

namespace CznCore.Presentation.Vfx;

internal sealed class RenderingServerVfxLayerMountSpec
{
	public string Name { get; }

	public Node? Parent { get; init; }

	public bool Enabled { get; init; } = true;

	public int PrewarmCount { get; init; } = 128;

	public int MaxCapacity { get; init; } = 512;

	public int BaseZIndex { get; init; }

	public bool ZAsRelative { get; init; }

	public float StatsLogIntervalSeconds { get; init; }

	public RenderingServerVfxLayerMountSpec(string name)
	{
		Name = (string.IsNullOrWhiteSpace(name) ? "RenderingServerVfxLayer" : name);
	}
}
