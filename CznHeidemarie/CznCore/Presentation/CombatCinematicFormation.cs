using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal static class CombatCinematicFormation
{
	public static bool TryGetViewportTransform(Node2D? anchor, out Transform2D viewportTransform)
	{
		viewportTransform = Transform2D.Identity;
		if (anchor == null || !GodotObject.IsInstanceValid(anchor))
		{
			return false;
		}
		try
		{
			Transform2D globalTransformWithCanvas = anchor.GetGlobalTransformWithCanvas();
			if (!IsFinite(globalTransformWithCanvas))
			{
				return false;
			}
			viewportTransform = globalTransformWithCanvas;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool TryGetViewportPosition(Node2D? anchor, out Vector2 viewportPosition)
	{
		viewportPosition = Vector2.Zero;
		if (!TryGetViewportTransform(anchor, out var viewportTransform))
		{
			return false;
		}
		viewportPosition = viewportTransform.Origin;
		return true;
	}

	public static CombatCinematicFormationSnapshot Capture(Node2D? actorAnchor, IReadOnlyList<Node2D>? targetAnchors)
	{
		bool flag = TryGetViewportPosition(actorAnchor, out var viewportPosition);
		List<Vector2> list = new List<Vector2>(targetAnchors?.Count ?? 0);
		if (targetAnchors != null)
		{
			foreach (Node2D targetAnchor in targetAnchors)
			{
				if (TryGetViewportPosition(targetAnchor, out var viewportPosition2))
				{
					list.Add(viewportPosition2);
				}
			}
		}
		Rect2 viewportRect = default;
		Node2D node2D = (flag ? actorAnchor : FirstValidAnchor(targetAnchors));
		if (node2D != null)
		{
			try
			{
				Viewport viewport = node2D.GetViewport();
				if (viewport != null && GodotObject.IsInstanceValid(viewport))
				{
					viewportRect = viewport.GetVisibleRect();
				}
			}
			catch
			{
			}
		}
		return new CombatCinematicFormationSnapshot(flag, viewportPosition, list.ToArray(), viewportRect);
	}

	public static CombatCinematicFormationSnapshot CaptureSingle(Node2D? actorAnchor, Node2D? targetAnchor)
	{
		if (targetAnchor != null && GodotObject.IsInstanceValid(targetAnchor))
		{
			return Capture(actorAnchor, new Node2D[1] { targetAnchor });
		}
		return Capture(actorAnchor, null);
	}

	public static Vector2 ResolvePackedTargetProxyOffset(int index, int count, Vector2 proxyBaseScale, float screenSpacing, float screenRowGap)
	{
		if (index < 0 || index >= count || count <= 1)
		{
			return Vector2.Zero;
		}
		float num = Math.Clamp((MathF.Abs(proxyBaseScale.X) + MathF.Abs(proxyBaseScale.Y)) * 0.5f, 0.42f, 1.35f);
		float num2 = screenSpacing / num;
		float num3 = screenRowGap / num;
		if (count < 5)
		{
			return count switch
			{
				2 => (index == 0) ? new Vector2((0f - num2) * 0.62f, 0f) : new Vector2(num2 * 0.62f, 0f), 
				3 => index switch
				{
					0 => new Vector2(0f - num2, num3 * 0.25f), 
					1 => new Vector2(0f, (0f - num3) * 0.35f), 
					_ => new Vector2(num2, num3 * 0.25f), 
				}, 
				4 => index switch
				{
					0 => new Vector2((0f - num2) * 0.72f, (0f - num3) * 0.45f), 
					1 => new Vector2(num2 * 0.72f, (0f - num3) * 0.45f), 
					2 => new Vector2((0f - num2) * 0.72f, num3 * 0.55f), 
					_ => new Vector2(num2 * 0.72f, num3 * 0.55f), 
				}, 
				_ => Vector2.Zero, 
			};
		}
		return index switch
		{
			0 => new Vector2(0f - num2, (0f - num3) * 0.48f), 
			1 => new Vector2(0f, (0f - num3) * 0.6f), 
			2 => new Vector2(num2, (0f - num3) * 0.48f), 
			3 => new Vector2((0f - num2) * 0.55f, num3 * 0.58f), 
			_ => new Vector2(num2 * 0.55f, num3 * 0.58f), 
		};
	}

	private static Node2D? FirstValidAnchor(IReadOnlyList<Node2D>? anchors)
	{
		if (anchors == null)
		{
			return null;
		}
		foreach (Node2D anchor in anchors)
		{
			if (GodotObject.IsInstanceValid(anchor))
			{
				return anchor;
			}
		}
		return null;
	}

	private static bool IsFinite(Transform2D transform)
	{
		if (IsFinite(transform.X) && IsFinite(transform.Y))
		{
			return IsFinite(transform.Origin);
		}
		return false;
	}

	private static bool IsFinite(Vector2 value)
	{
		if (float.IsFinite(value.X))
		{
			return float.IsFinite(value.Y);
		}
		return false;
	}
}
