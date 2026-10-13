using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatCreatureUiDepthRuntime
{
	private static readonly NodePath HealthBarPath = new NodePath("%HealthBar");

	public static Node2D? ResolveVisualSurface(NCreature? creature)
	{
		if (!EnsureAbovePresentation(creature))
		{
			return null;
		}
		try
		{
			return GodotObject.IsInstanceValid(creature.Visuals) ? creature.Visuals : null;
		}
		catch
		{
			return null;
		}
	}

	public static bool EnsureAbovePresentation(NCreature? creature)
	{
		if (creature == null || !GodotObject.IsInstanceValid(creature))
		{
			return false;
		}
		bool flag = false;
		try
		{
			CanvasItem nodeOrNull = creature.GetNodeOrNull<CanvasItem>(HealthBarPath);
			flag |= ApplyCreatureOwnedDepth(nodeOrNull);
		}
		catch
		{
		}
		try
		{
			flag |= ApplySemanticUiDepth(creature.IntentContainer);
		}
		catch
		{
		}
		return flag;
	}

	private static bool ApplyCreatureOwnedDepth(CanvasItem? item)
	{
		if (item == null || !GodotObject.IsInstanceValid(item))
		{
			return false;
		}
		item.ZAsRelative = true;
		item.ZIndex = 0;
		return true;
	}

	private static bool ApplySemanticUiDepth(CanvasItem? item)
	{
		if (item == null || !GodotObject.IsInstanceValid(item))
		{
			return false;
		}
		item.ZAsRelative = false;
		item.ZIndex = -1;
		return true;
	}
}
