using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatTargetStaging
{
	private readonly struct CombatTargetProxySource(Node2D anchor, Vector2 scaleMagnitude, Transform2D viewportBasis)
	{
		public Node2D Anchor { get; } = anchor;

		public Vector2 ScaleMagnitude { get; } = scaleMagnitude;

		public Transform2D ViewportBasis { get; } = viewportBasis;
	}

	private const int SpineVisualStateMaxTracks = 8;

	public const string SpineVisualStateMeta = "czn_spine_state";

	private const float ProxyBasisEpsilon = 0.0001f;

	public static NCreature? FirstValidCreatureNode(IReadOnlyList<NCreature>? creatureNodes)
	{
		if (creatureNodes == null)
		{
			return null;
		}
		foreach (NCreature creatureNode in creatureNodes)
		{
			if (GodotObject.IsInstanceValid(creatureNode))
			{
				return creatureNode;
			}
		}
		return null;
	}

	public static Node2D? FindFirstSpineAnchor(CombatCreatureSpineResolver spines, IReadOnlyList<NCreature>? creatureNodes)
	{
		if (creatureNodes == null)
		{
			return null;
		}
		foreach (NCreature creatureNode in creatureNodes)
		{
			if (GodotObject.IsInstanceValid(creatureNode))
			{
				Node2D node2D = spines.Find(creatureNode);
				if (node2D != null && GodotObject.IsInstanceValid(node2D))
				{
					return node2D;
				}
			}
		}
		return null;
	}

	private static Node2D? TryCreateTargetProxy(Node2D? sourceAnchor, string proxyName, int duplicateFlags, string failureContext, Action<string>? warn = null)
	{
		if (sourceAnchor == null || !GodotObject.IsInstanceValid(sourceAnchor))
		{
			return null;
		}
		try
		{
			Node node = sourceAnchor.Duplicate(duplicateFlags);
			if (!(node is Node2D node2D))
			{
				node?.QueueFree();
				return null;
			}
			node2D.Name = proxyName;
			return node2D;
		}
		catch (Exception ex)
		{
			warn?.Invoke(failureContext + ": " + SkinLog.FormatException(ex));
			return null;
		}
	}

	public static Node2D? TryCreateMirroredTargetProxy(Node2D? sourceAnchor, string proxyName, int duplicateFlags, string failureContext, Action<string>? warn = null)
	{
		Vector2 baseScale;
		return TryCreateMirroredTargetProxy(sourceAnchor, proxyName, duplicateFlags, failureContext, out baseScale, warn);
	}

	public static Node2D? TryCreateMirroredTargetProxy(Node2D? sourceAnchor, string proxyName, int duplicateFlags, string failureContext, out Vector2 baseScale, Action<string>? warn = null)
	{
		return TryCreateMirroredTargetProxy(sourceAnchor, proxyName, duplicateFlags, failureContext, out baseScale, reflectHorizontally: false, warn);
	}

	public static Node2D? TryCreateMirroredTargetProxy(Node2D? sourceAnchor, string proxyName, int duplicateFlags, string failureContext, out Vector2 baseScale, bool reflectHorizontally, Action<string>? warn = null)
	{
		baseScale = Vector2.One;
		if (!TryCaptureProxySource(sourceAnchor, out var source))
		{
			return null;
		}
		CombatTargetProxyRuntime combatTargetProxyRuntime = null;
		try
		{
			combatTargetProxyRuntime = CreateProxyRuntime(proxyName);
			baseScale = source.ScaleMagnitude;
			combatTargetProxyRuntime.Scale = baseScale;
			if (!TryAttachProxyVisual(combatTargetProxyRuntime, source, proxyName + "Visual", duplicateFlags, source.ScaleMagnitude, Vector2.Zero, 0, reflectHorizontally, failureContext, warn))
			{
				combatTargetProxyRuntime.Free();
				return null;
			}
			return combatTargetProxyRuntime;
		}
		catch (Exception ex)
		{
			combatTargetProxyRuntime?.Free();
			warn?.Invoke(failureContext + ": " + SkinLog.FormatException(ex));
			return null;
		}
	}

	public static Node2D? TryCreateMirroredTargetProxyGroup(IReadOnlyList<Node2D>? sourceAnchors, string groupName, string childNamePrefix, int duplicateFlags, int maxCopies, Vector2 groupScale, Func<Node2D, int, int, Vector2, Vector2> resolveChildPosition, string failureContext, out Vector2 baseScale, Action<string>? warn = null)
	{
		baseScale = Vector2.One;
		if (sourceAnchors == null || maxCopies <= 0)
		{
			return null;
		}
		List<CombatTargetProxySource> list = new List<CombatTargetProxySource>(Math.Min(maxCopies, sourceAnchors.Count));
		foreach (Node2D sourceAnchor in sourceAnchors)
		{
			if (list.Count >= maxCopies)
			{
				break;
			}
			if (TryCaptureProxySource(sourceAnchor, out var source))
			{
				list.Add(source);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		Vector2 basisNormalizationScale = ResolveAverageScale(list);
		baseScale = new Vector2(basisNormalizationScale.X * MathF.Abs(groupScale.X), basisNormalizationScale.Y * MathF.Abs(groupScale.Y));
		CombatTargetProxyRuntime combatTargetProxyRuntime = null;
		try
		{
			combatTargetProxyRuntime = CreateProxyRuntime(groupName);
			combatTargetProxyRuntime.Scale = baseScale;
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				CombatTargetProxySource source2 = list[i];
				Vector2 position = resolveChildPosition(source2.Anchor, i, list.Count, baseScale);
				if (TryAttachProxyVisual(combatTargetProxyRuntime, source2, childNamePrefix + (i + 1), duplicateFlags, basisNormalizationScale, position, i, reflectHorizontally: false, failureContext, warn))
				{
					num++;
				}
			}
			if (num == 0)
			{
				combatTargetProxyRuntime.Free();
				return null;
			}
			return combatTargetProxyRuntime;
		}
		catch (Exception ex)
		{
			combatTargetProxyRuntime?.Free();
			warn?.Invoke(failureContext + ": " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private static CombatTargetProxyRuntime CreateProxyRuntime(string name)
	{
		CombatTargetProxyRuntime combatTargetProxyRuntime = new CombatTargetProxyRuntime();
		combatTargetProxyRuntime.Name = name;
		combatTargetProxyRuntime.Position = Vector2.Zero;
		combatTargetProxyRuntime.Rotation = 0f;
		combatTargetProxyRuntime.Scale = Vector2.One;
		combatTargetProxyRuntime.TopLevel = false;
		combatTargetProxyRuntime.ZAsRelative = true;
		combatTargetProxyRuntime.ZIndex = 0;
		combatTargetProxyRuntime.Visible = true;
		combatTargetProxyRuntime.BindRouteCommandSignals();
		return combatTargetProxyRuntime;
	}

	private static bool TryAttachProxyVisual(CombatTargetProxyRuntime runtime, CombatTargetProxySource source, string visualName, int duplicateFlags, Vector2 basisNormalizationScale, Vector2 position, int zIndex, bool reflectHorizontally, string failureContext, Action<string>? warn)
	{
		Node2D node2D = TryCreateTargetProxy(source.Anchor, visualName, duplicateFlags, failureContext, warn);
		if (node2D == null)
		{
			return false;
		}
		Vector2 vector = DivideComponents(source.ViewportBasis.X, basisNormalizationScale);
		Vector2 vector2 = DivideComponents(source.ViewportBasis.Y, basisNormalizationScale);
		if (reflectHorizontally)
		{
			vector = ReflectViewportX(vector);
			vector2 = ReflectViewportX(vector2);
		}
		node2D.Transform = new Transform2D(vector, vector2, position);
		node2D.TopLevel = false;
		node2D.ZAsRelative = true;
		node2D.ZIndex = zIndex;
		node2D.Visible = true;
		StampMirroredSpineVisualStates(source.Anchor, node2D);
		runtime.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static void StampMirroredSpineVisualStates(Node source, Node duplicate)
	{
		if (source is Node2D node2D && duplicate is Node2D node2D2 && node2D.HasMethod("get_animation_state") && node2D.HasMethod("get_skeleton"))
		{
			Dictionary dictionary = CaptureSpineVisualState(node2D);
			node2D2.SetMeta("czn_spine_state", dictionary);
		}
		int num = Math.Min(source.GetChildCount(), duplicate.GetChildCount());
		for (int i = 0; i < num; i++)
		{
			StampMirroredSpineVisualStates(source.GetChild(i), duplicate.GetChild(i));
		}
	}

	private static bool TryCaptureProxySource(Node2D? anchor, out CombatTargetProxySource source)
	{
		source = default;
		if (anchor == null || !GodotObject.IsInstanceValid(anchor))
		{
			return false;
		}
		if (!CombatCinematicFormation.TryGetViewportTransform(anchor, out var viewportTransform))
		{
			return false;
		}
		float val = viewportTransform.X.Length();
		float val2 = viewportTransform.Y.Length();
		source = new CombatTargetProxySource(anchor, new Vector2(Math.Max(val, 0.0001f), Math.Max(val2, 0.0001f)), new Transform2D(viewportTransform.X, viewportTransform.Y, Vector2.Zero));
		return true;
	}

	private static Vector2 ResolveAverageScale(IReadOnlyList<CombatTargetProxySource> sources)
	{
		Vector2 zero = Vector2.Zero;
		foreach (CombatTargetProxySource source in sources)
		{
			zero += source.ScaleMagnitude;
		}
		return zero / sources.Count;
	}

	private static Vector2 DivideComponents(Vector2 value, Vector2 divisor)
	{
		return new Vector2(value.X / divisor.X, value.Y / divisor.Y);
	}

	private static Vector2 ReflectViewportX(Vector2 value)
	{
		return new Vector2(0f - value.X, value.Y);
	}

	public static Dictionary CaptureSpineVisualState(Node2D? sourceAnchor)
	{
		Dictionary dictionary = new Dictionary();
		if (sourceAnchor == null || !GodotObject.IsInstanceValid(sourceAnchor))
		{
			return dictionary;
		}
		try
		{
			if (sourceAnchor.HasMethod("get_skeleton"))
			{
				GodotObject godotObject = sourceAnchor.Call("get_skeleton").AsGodotObject();
				if (godotObject != null && godotObject.HasMethod("get_skin"))
				{
					GodotObject godotObject2 = godotObject.Call("get_skin").AsGodotObject();
					if (godotObject2 != null && godotObject2.HasMethod("get_name"))
					{
						string text = godotObject2.Call("get_name").AsString();
						if (!string.IsNullOrEmpty(text))
						{
							dictionary["skin"] = text;
						}
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			if (sourceAnchor.HasMethod("get_animation_state"))
			{
				GodotObject godotObject3 = sourceAnchor.Call("get_animation_state").AsGodotObject();
				if (godotObject3 != null && godotObject3.HasMethod("get_current"))
				{
					global::Godot.Collections.Array array = new global::Godot.Collections.Array();
					for (int i = 0; i < 8; i++)
					{
						GodotObject godotObject4 = godotObject3.Call("get_current", i).AsGodotObject();
						if (godotObject4 == null || !godotObject4.HasMethod("get_animation"))
						{
							continue;
						}
						GodotObject godotObject5 = godotObject4.Call("get_animation").AsGodotObject();
						if (godotObject5 == null || !godotObject5.HasMethod("get_name"))
						{
							continue;
						}
						string text2 = godotObject5.Call("get_name").AsString();
						if (!string.IsNullOrEmpty(text2) && !text2.StartsWith("<"))
						{
							Dictionary dictionary2 = new Dictionary
							{
								["track"] = i,
								["anim"] = text2
							};
							if (godotObject4.HasMethod("get_track_time"))
							{
								dictionary2["time"] = godotObject4.Call("get_track_time").AsSingle();
							}
							if (godotObject4.HasMethod("get_loop"))
							{
								dictionary2["loop"] = godotObject4.Call("get_loop").AsBool();
							}
							array.Add(dictionary2);
							if (i == 0)
							{
								dictionary["anim"] = text2;
							}
						}
					}
					if (array.Count > 0)
					{
						dictionary["tracks"] = array;
					}
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}

	public static bool TryGetAnchorSnapshot(CombatCreatureSpineResolver? spines, NCreature? creatureNode, out CombatTargetAnchorSnapshot snapshot)
	{
		snapshot = default;
		if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode))
		{
			return false;
		}
		Node2D spine = spines?.Find(creatureNode);
		Vector2 vfxSpawnGlobal = Vector2.Zero;
		try
		{
			vfxSpawnGlobal = creatureNode.VfxSpawnPosition;
		}
		catch
		{
		}
		Vector2 hitboxGlobalPosition = Vector2.Zero;
		Vector2 hitboxSize = Vector2.Zero;
		Vector2 hitboxTopGlobal = Vector2.Zero;
		Vector2 hitboxBottomGlobal = Vector2.Zero;
		try
		{
			Control hitbox = creatureNode.Hitbox;
			if (hitbox != null && GodotObject.IsInstanceValid(hitbox))
			{
				hitboxGlobalPosition = hitbox.GlobalPosition;
				hitboxSize = hitbox.Size;
				hitboxTopGlobal = creatureNode.GetTopOfHitbox();
				hitboxBottomGlobal = creatureNode.GetBottomOfHitbox();
			}
		}
		catch
		{
		}
		Vector2 boundsSize = Vector2.Zero;
		try
		{
			boundsSize = creatureNode.Visuals?.Bounds?.Size ?? Vector2.Zero;
		}
		catch
		{
		}
		snapshot = new CombatTargetAnchorSnapshot(creatureNode, spine, vfxSpawnGlobal, hitboxGlobalPosition, hitboxSize, hitboxTopGlobal, hitboxBottomGlobal, boundsSize);
		return true;
	}
}
