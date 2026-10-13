using System;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation.Vfx;

internal static class RenderingServerVfxLayerMount
{
	public static RenderingServerVfxLayer? EnsureMounted(ref RenderingServerVfxLayer? layer, RenderingServerVfxLayerMountSpec spec, Action<string>? debug = null, Action<string>? warn = null)
	{
		if (spec == null)
		{
			throw new ArgumentNullException("spec");
		}
		if (GodotNodeUtil.IsAlive(layer))
		{
			return layer;
		}
		layer = null;
		Node node = ResolveParent(spec.Parent);
		if (node == null)
		{
			warn?.Invoke("RenderingServer VFX layer mount skipped: SceneTree root unavailable.");
			return null;
		}
		int num = Math.Max(0, spec.MaxCapacity);
		int prewarmCount = Math.Clamp(spec.PrewarmCount, 0, num);
		RenderingServerVfxLayer renderingServerVfxLayer = new RenderingServerVfxLayer
		{
			Name = spec.Name,
			Enabled = spec.Enabled,
			PrewarmCount = prewarmCount,
			MaxCapacity = num,
			BaseZIndex = spec.BaseZIndex,
			ZAsRelative = spec.ZAsRelative,
			StatsLogIntervalSeconds = Math.Max(0f, spec.StatsLogIntervalSeconds)
		};
		node.AddChild(renderingServerVfxLayer, forceReadableName: false, Node.InternalMode.Disabled);
		layer = renderingServerVfxLayer;
		debug?.Invoke("RenderingServer VFX layer mounted name=" + spec.Name + " prewarm=" + prewarmCount + " capacity=" + num + " z=" + spec.BaseZIndex);
		return renderingServerVfxLayer;
	}

	public static void Release(ref RenderingServerVfxLayer? layer)
	{
		RenderingServerVfxLayer? node = layer;
		layer = null;
		GodotNodeUtil.SafeQueueFree(node);
	}

	private static Node? ResolveParent(Node? parent)
	{
		if (GodotNodeUtil.IsAlive(parent))
		{
			return parent;
		}
		if (Engine.GetMainLoop() is SceneTree { Root: not null } sceneTree)
		{
			return sceneTree.Root;
		}
		return null;
	}
}
