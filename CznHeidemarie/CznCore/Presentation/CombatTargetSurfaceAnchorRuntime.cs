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

	/// <summary>
	/// 只算出「目标表面锚点」的全局坐标，<b>不创建任何节点</b>。
	///
	/// <para>与 <see cref="Resolve"/> 分开的原因：<see cref="Resolve"/> 会把锚点挂在
	/// <c>NCreature</c>（Control）底下，并且强制 <c>Scale = One</c> —— 那会丢掉敌人
	/// 自身的缩放（<c>SetScaleAndHue</c>、Osty 的体型变化）与翻转。
	/// 而单体特效舞台挂在目标的<b>骨架节点</b>下，需要的是「锚点在骨架局部坐标系中的
	/// 位置」，只能由调用方用 <c>骨架.GetGlobalTransform().Inverse()</c> 自行换算 ——
	/// 走这条路缩放与翻转都会被正确还原，不需要任何兜底。</para>
	/// </summary>
	/// <param name="targetNode">敌人的战斗节点。</param>
	/// <param name="mode">取哪个表面点；缺失时逐级回退到底部命中框。</param>
	/// <param name="surfaceGlobal">锚点的全局坐标。</param>
	/// <returns>取到有效坐标返回 <c>true</c>；节点无效或取不到返回 <c>false</c>。</returns>
	public static bool TryResolveSurfaceGlobal(
		NCreature? targetNode,
		CombatTargetSurfaceAnchorMode mode,
		out Vector2 surfaceGlobal)
	{
		surfaceGlobal = Vector2.Zero;
		if (targetNode == null || !GodotObject.IsInstanceValid(targetNode))
		{
			return false;
		}

		try
		{
			surfaceGlobal = ResolveTargetSurfaceGlobal(targetNode, mode, out _);
			return true;
		}
		catch
		{
			// 取锚点失败只影响落点观感，交给调用方按「本帧不校正」处理
			return false;
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
