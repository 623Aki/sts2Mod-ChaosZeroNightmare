using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatCreatureVisualAnchorResolver(CombatCreatureSpineResolver spines)
{
	public Node2D? Find(NCreature? creature)
	{
		if (creature == null || !GodotObject.IsInstanceValid(creature))
		{
			return null;
		}
		Node2D node2D = spines.Find(creature);
		if (GodotNodeUtil.IsAlive(node2D))
		{
			return node2D;
		}
		return FindBodyFallback(creature);
	}

	private static Node2D? FindBodyFallback(NCreature creature)
	{
		try
		{
			Node2D body = creature.Body;
			if (GodotNodeUtil.IsAlive(body))
			{
				return body;
			}
		}
		catch
		{
		}
		try
		{
			NCreatureVisuals visuals = creature.Visuals;
			return GodotNodeUtil.IsAlive(visuals) ? visuals : null;
		}
		catch
		{
			return null;
		}
	}
}
