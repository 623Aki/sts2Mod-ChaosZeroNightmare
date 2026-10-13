using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatTargetSurfaceAnchorRuntime
{
	public static Node2D? Resolve(NCreature? targetNode, string anchorName, string failurePrefix, Action<string, int>? warn = null, Action<NCreature, Node2D, Vector2, string>? diagnostic = null, CombatTargetSurfaceAnchorMode mode = CombatTargetSurfaceAnchorMode.FloorLockedVfxX)
	{
		if (targetNode == null || !GodotObject.IsInstanceValid(targetNode))
		{
			return null;
		}
		CombatCreatureUiDepthRuntime.EnsureAbovePresentation(targetNode);
		try
		{
			Node2D node2D = targetNode.GetNodeOrNull<Node2D>(new NodePath(anchorName));
			if (node2D == null)
			{
				node2D = new Node2D
				{
					Name = anchorName
				};
				targetNode.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
			}
			Vector2 vector = ResolveTargetSurfaceGlobal(targetNode, mode, out string selectedAnchor);
			node2D.Position = targetNode.GetGlobalTransform().Inverse() * vector;
			node2D.Scale = Vector2.One;
			node2D.Rotation = 0f;
			node2D.TopLevel = false;
			node2D.ZAsRelative = true;
			node2D.ZIndex = 0;
			node2D.Visible = true;
			diagnostic?.Invoke(targetNode, node2D, vector, selectedAnchor);
			return node2D;
		}
		catch (Exception ex)
		{
			warn?.Invoke(failurePrefix + SkinLog.FormatException(ex), 2);
			return null;
		}
	}

	private static bool TryGetTargetVfxSpawnGlobal(NCreature targetNode, out Vector2 vfxSpawnGlobal)
	{
		vfxSpawnGlobal = Vector2.Zero;
		try
		{
			vfxSpawnGlobal = targetNode.VfxSpawnPosition;
			return vfxSpawnGlobal != Vector2.Zero;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetTargetHitboxCenterGlobal(NCreature targetNode, out Vector2 hitboxCenterGlobal)
	{
		hitboxCenterGlobal = Vector2.Zero;
		try
		{
			Control hitbox = targetNode.Hitbox;
			if (hitbox == null || !GodotObject.IsInstanceValid(hitbox) || hitbox.Size.X <= 0f || hitbox.Size.Y <= 0f)
			{
				return false;
			}
			hitboxCenterGlobal = hitbox.GlobalPosition + hitbox.Size * 0.5f;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static Vector2 ResolveTargetSurfaceGlobal(NCreature targetNode, CombatTargetSurfaceAnchorMode mode, out string selectedAnchor)
	{
		if (mode == CombatTargetSurfaceAnchorMode.VfxSpawnPosition && TryGetTargetVfxSpawnGlobal(targetNode, out var vfxSpawnGlobal))
		{
			selectedAnchor = "vfx_spawn";
			return vfxSpawnGlobal;
		}
		if (mode == CombatTargetSurfaceAnchorMode.HitboxCenter)
		{
			if (TryGetTargetHitboxCenterGlobal(targetNode, out var hitboxCenterGlobal))
			{
				selectedAnchor = "hitbox_center";
				return hitboxCenterGlobal;
			}
			if (TryGetTargetVfxSpawnGlobal(targetNode, out vfxSpawnGlobal))
			{
				selectedAnchor = "vfx_spawn_fallback";
				return vfxSpawnGlobal;
			}
		}
		Vector2 bottomOfHitbox = targetNode.GetBottomOfHitbox();
		selectedAnchor = "hitbox_bottom";
		if (TryGetTargetVfxSpawnGlobal(targetNode, out vfxSpawnGlobal))
		{
			selectedAnchor = "floor_locked_vfx_x";
			return new Vector2(vfxSpawnGlobal.X, bottomOfHitbox.Y);
		}
		return bottomOfHitbox;
	}
}
