using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal static class CombatScreenEffectAnchorRuntime
{
	public static Node2D? Resolve(string anchorName, CombatScreenEffectLayer layer, Vector2 referenceSize, CombatScreenEffectScaleMode scaleMode = CombatScreenEffectScaleMode.Fit, Action<string>? warn = null)
	{
		try
		{
			if (Engine.GetMainLoop() is SceneTree { Root: not null } sceneTree)
			{
				NCombatRoom instance = NCombatRoom.Instance;
				if (instance != null)
				{
					Control combatVfxContainer = instance.CombatVfxContainer;
					if (combatVfxContainer != null && GodotObject.IsInstanceValid(combatVfxContainer))
					{
						Node2D node2D = combatVfxContainer.GetNodeOrNull<Node2D>(new NodePath(anchorName));
						if (node2D == null)
						{
							node2D = new Node2D
							{
								Name = anchorName
							};
							combatVfxContainer.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
						}
						Vector2 size = sceneTree.Root.GetVisibleRect().Size;
						float num = ResolveScale(size, referenceSize, scaleMode);
						node2D.TopLevel = false;
						node2D.ZAsRelative = false;
						node2D.ZIndex = ((layer == CombatScreenEffectLayer.Back) ? (-12) : (-3));
						node2D.GlobalPosition = size * 0.5f;
						node2D.GlobalRotation = 0f;
						node2D.GlobalScale = Vector2.One * num;
						node2D.Visible = true;
						return node2D;
					}
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			warn?.Invoke(ex.ToString());
			return null;
		}
	}

	private static float ResolveScale(Vector2 viewportSize, Vector2 referenceSize, CombatScreenEffectScaleMode scaleMode)
	{
		if (viewportSize.X <= 0f || viewportSize.Y <= 0f || referenceSize.X <= 0f || referenceSize.Y <= 0f)
		{
			return 1f;
		}
		float x = viewportSize.X / referenceSize.X;
		float y = viewportSize.Y / referenceSize.Y;
		float y2 = ((scaleMode == CombatScreenEffectScaleMode.Cover) ? MathF.Max(x, y) : MathF.Min(x, y));
		return MathF.Max(0.001f, y2);
	}
}
