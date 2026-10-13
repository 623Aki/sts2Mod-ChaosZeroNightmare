using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal static class CombatEffectRuntime
{
	private sealed class EffectNodeCache
	{
		public IReadOnlyList<Node> Nodes { get; }

		public IReadOnlyList<Node> SpineSprites { get; }

		public IReadOnlyList<CanvasItem> CanvasItems { get; }

		public IReadOnlyList<Node> ParticleNodes { get; }

		public EffectNodeCache(IReadOnlyList<Node> nodes, IReadOnlyList<Node> spineSprites, IReadOnlyList<CanvasItem> canvasItems)
		{
			Nodes = nodes;
			SpineSprites = spineSprites;
			CanvasItems = canvasItems;
			ParticleNodes = Array.Empty<Node>();
		}

		public EffectNodeCache(IReadOnlyList<Node> nodes, IReadOnlyList<Node> spineSprites, IReadOnlyList<CanvasItem> canvasItems, IReadOnlyList<Node> particleNodes)
		{
			Nodes = nodes;
			SpineSprites = spineSprites;
			CanvasItems = canvasItems;
			ParticleNodes = particleNodes;
		}
	}

	public const string ParticleStartDelayMeta = "czn_particle_start_delay_seconds";

	public const string ParticleEmissionDurationMeta = "czn_particle_emission_duration_seconds";

	private const string ParticleStartTokenMeta = "czn_particle_start_token";

	private static readonly ConditionalWeakTable<Node, MegaSprite> MegaSpriteCache = new ConditionalWeakTable<Node, MegaSprite>();

	private static readonly ConditionalWeakTable<Node, EffectNodeCache> EffectNodeCaches = new ConditionalWeakTable<Node, EffectNodeCache>();

	private static readonly string[] SpineMaterialProperties = new string[4] { "normal_material", "additive_material", "multiply_material", "screen_material" };

	public static void PrimeNodeCaches(Node root)
	{
		if (GodotObject.IsInstanceValid(root))
		{
			EffectNodeCaches.Remove(root);
			EffectNodeCaches.Add(root, BuildEffectNodeCache(root));
		}
	}

	public static Node2D? DetachLayer(Node sourceRoot, string childName, Color modulate, string behindParentLayerName)
	{
		if (!(sourceRoot.GetNodeOrNull(childName) is Node2D node2D))
		{
			return null;
		}
		sourceRoot.RemoveChild(node2D);
		ResetLayerTransform(node2D, IsBehindParentLayer(childName, behindParentLayerName), modulate);
		return node2D;
	}

	public static int AttachLayersByOriginalZ(Node2D effectRoot, Node sourceRoot, Color modulate, string behindParentLayerName)
	{
		int num = 0;
		List<(Node2D, int)> list = new List<(Node2D, int)>();
		foreach (Node child in sourceRoot.GetChildren())
		{
			if (child is Node2D node2D)
			{
				sourceRoot.RemoveChild(node2D);
				int zIndex = node2D.ZIndex;
				ResetLayerTransform(node2D, IsBehindParentLayer(node2D.Name.ToString(), behindParentLayerName), modulate);
				list.Add((node2D, zIndex));
			}
		}
		list.Sort(((Node2D Layer, int OriginalZIndex) a, (Node2D Layer, int OriginalZIndex) b) => a.OriginalZIndex.CompareTo(b.OriginalZIndex));
		foreach (var item2 in list)
		{
			Node2D item = item2.Item1;
			effectRoot.AddChild(item, forceReadableName: false, Node.InternalMode.Disabled);
			num++;
		}
		return num;
	}

	public static int StartSpineAnimation(Node effectRoot, string animationName, bool loop, float timeScale = 1f, Action<string>? warn = null)
	{
		int num = 0;
		foreach (Node spineSprite in GetSpineSprites(effectRoot))
		{
			num += StartSpineAnimationNode(spineSprite, animationName, loop, timeScale, warn);
		}
		return num;
	}

	public static int StartSpineAnimationsFromMetadata(Node effectRoot, string metadataKey = "czn_spine_animation_name", string fallbackAnimationName = "animation", bool loop = false, float timeScale = 1f, Action<string>? warn = null)
	{
		int num = 0;
		foreach (Node spineSprite in GetSpineSprites(effectRoot))
		{
			string text = (spineSprite.HasMeta(metadataKey) ? spineSprite.GetMeta(metadataKey).AsString() : fallbackAnimationName);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = fallbackAnimationName;
			}
			num += StartSpineAnimationNode(spineSprite, text, loop, timeScale, warn);
		}
		return num;
	}

	public static void ApplyAuthoredRootTransform(Node2D root, Vector2 positionOffset, Vector2 scaleMultiplier, float rotationOffsetRadians = 0f)
	{
		root.Position += positionOffset;
		root.Scale = new Vector2(root.Scale.X * scaleMultiplier.X, root.Scale.Y * scaleMultiplier.Y);
		root.Rotation += rotationOffsetRadians;
	}

	private static int StartSpineAnimationNode(Node effectSpine, string animationName, bool loop, float timeScale, Action<string>? warn)
	{
		if (!GodotObject.IsInstanceValid(effectSpine))
		{
			return 0;
		}
		try
		{
			MegaAnimationState animationState = GetMegaSprite(effectSpine).GetAnimationState();
			if (animationState == null)
			{
				return 0;
			}
			MegaTrackEntry megaTrackEntry = SpineAnimationCompat.SetAnimationTracked(animationState, animationName, loop);
			try
			{
				if (megaTrackEntry != null && Math.Abs(timeScale - 1f) > 0.001f)
				{
					megaTrackEntry.SetTimeScale(timeScale);
				}
			}
			finally
			{
				SpineAnimationCompat.ReleaseTracked(megaTrackEntry);
			}
			return 1;
		}
		catch (Exception ex)
		{
			warn?.Invoke(string.Concat(new string[5]
			{
				effectSpine.Name,
				" animation=",
				animationName,
				" ",
				SkinLog.FormatException(ex)
			}));
			return 0;
		}
	}

	public static int StartParticles(Node effectRoot, Action<string>? warn = null)
	{
		int num = 0;
		foreach (Node particleNode in GetParticleNodes(effectRoot))
		{
			if (!GodotObject.IsInstanceValid(particleNode))
			{
				continue;
			}
			try
			{
				int token = NextParticleStartToken(particleNode);
				float particleStartDelaySeconds = GetParticleStartDelaySeconds(particleNode);
				if (particleStartDelaySeconds <= 0.001f)
				{
					StartParticleNode(particleNode, token, warn);
				}
				else
				{
					GodotScheduler.Delay(particleStartDelaySeconds, () =>
					{
						if (GodotObject.IsInstanceValid(particleNode) && IsParticleStartTokenCurrent(particleNode, token))
						{
							StartParticleNode(particleNode, token, warn);
						}
					});
				}
				num++;
			}
			catch (Exception ex)
			{
				warn?.Invoke(string.Concat(particleNode.Name, " ", SkinLog.FormatException(ex)));
			}
		}
		return num;
	}

	public static int StopParticles(Node effectRoot)
	{
		int num = 0;
		foreach (Node particleNode in GetParticleNodes(effectRoot))
		{
			if (GodotObject.IsInstanceValid(particleNode))
			{
				try
				{
					NextParticleStartToken(particleNode);
					particleNode.Set("emitting", Variant.From<bool>(false));
					num++;
				}
				catch
				{
				}
			}
		}
		return num;
	}

	private static MegaSprite GetMegaSprite(Node spineNode)
	{
		return MegaSpriteCache.GetValue(spineNode, (Node node) => new MegaSprite(node));
	}

	public static int ApplyCanvasMaterialRecursive(Node root, Material material)
	{
		int num = 0;
		foreach (CanvasItem canvasItem in GetCanvasItems(root))
		{
			if (GodotObject.IsInstanceValid(canvasItem))
			{
				canvasItem.Material = material;
				canvasItem.UseParentMaterial = false;
				num++;
			}
		}
		return num;
	}

	public static int ApplyCanvasAndSpineMaterials(Node root, Material material)
	{
		int result = ApplyCanvasMaterialRecursive(root, material);
		foreach (Node spineSprite in GetSpineSprites(root))
		{
			if (GodotObject.IsInstanceValid(spineSprite))
			{
				SetAllSpineMaterials(spineSprite, material);
			}
		}
		return result;
	}

	public static void SetSpineMaterial(Node spineNode, string property, Material material)
	{
		string text = "set_" + property;
		if (spineNode.HasMethod(text))
		{
			spineNode.Call(text, material);
			return;
		}
		try
		{
			spineNode.Set(property, Variant.From(in material));
		}
		catch
		{
		}
	}

	public static int ApplySpineMaterialAndResetCanvas(Node root, Material material, Func<Node, Color>? modulateForNode, string? skipRootMeta = null)
	{
		if (!string.IsNullOrEmpty(skipRootMeta) && root.HasMeta(skipRootMeta))
		{
			return 0;
		}
		int num = 0;
		foreach (Node node in GetNodes(root))
		{
			if (!GodotObject.IsInstanceValid(node))
			{
				continue;
			}
			string text = node.GetClass();
			bool flag = IsParticleClass(text);
			if (text == "SpineSprite")
			{
				SetAllSpineMaterials(node, material);
				num++;
			}
			if (!flag && node is CanvasItem canvasItem)
			{
				canvasItem.Material = null;
				canvasItem.UseParentMaterial = false;
				if (modulateForNode != null)
				{
					canvasItem.Modulate = modulateForNode(node);
				}
			}
		}
		return num;
	}

	public static int ApplySpineMaterialsAndResetCanvas(Node root, CombatSpineMaterialSet materials, Func<Node, Color>? modulateForNode, string? skipRootMeta = null)
	{
		if (!string.IsNullOrEmpty(skipRootMeta) && root.HasMeta(skipRootMeta))
		{
			return 0;
		}
		int num = 0;
		foreach (Node node in GetNodes(root))
		{
			if (!GodotObject.IsInstanceValid(node))
			{
				continue;
			}
			string text = node.GetClass();
			bool flag = IsParticleClass(text);
			if (text == "SpineSprite")
			{
				SetAllSpineMaterials(node, materials);
				num++;
			}
			if (!flag && node is CanvasItem canvasItem)
			{
				canvasItem.Material = null;
				canvasItem.UseParentMaterial = false;
				if (modulateForNode != null)
				{
					canvasItem.Modulate = modulateForNode(node);
				}
			}
		}
		return num;
	}

	public static int ApplySpineMaterialOnly(Node root, Material material, string? skipRootMeta = null)
	{
		if (!string.IsNullOrEmpty(skipRootMeta) && root.HasMeta(skipRootMeta))
		{
			return 0;
		}
		int num = 0;
		foreach (Node spineSprite in GetSpineSprites(root))
		{
			if (GodotObject.IsInstanceValid(spineSprite))
			{
				SetAllSpineMaterials(spineSprite, material);
				num++;
			}
		}
		return num;
	}

	public static int ApplySpineMaterialsOnly(Node root, CombatSpineMaterialSet materials, string? skipRootMeta = null)
	{
		if (!string.IsNullOrEmpty(skipRootMeta) && root.HasMeta(skipRootMeta))
		{
			return 0;
		}
		int num = 0;
		foreach (Node spineSprite in GetSpineSprites(root))
		{
			if (GodotObject.IsInstanceValid(spineSprite))
			{
				SetAllSpineMaterials(spineSprite, materials);
				num++;
			}
		}
		return num;
	}

	public static void ApplyCanvasMaterialAndModulate(Node root, Material material, Color modulate)
	{
		foreach (CanvasItem canvasItem in GetCanvasItems(root))
		{
			if (GodotObject.IsInstanceValid(canvasItem))
			{
				canvasItem.Material = material;
				canvasItem.UseParentMaterial = false;
				canvasItem.Modulate = modulate;
			}
		}
	}

	private static void SetAllSpineMaterials(Node spineNode, Material material)
	{
		string[] spineMaterialProperties = SpineMaterialProperties;
		foreach (string property in spineMaterialProperties)
		{
			SetSpineMaterial(spineNode, property, material);
		}
	}

	private static void SetAllSpineMaterials(Node spineNode, CombatSpineMaterialSet materials)
	{
		SetSpineMaterial(spineNode, "normal_material", materials.Normal);
		SetSpineMaterial(spineNode, "additive_material", materials.Additive);
		SetSpineMaterial(spineNode, "multiply_material", materials.Multiply);
		SetSpineMaterial(spineNode, "screen_material", materials.Screen);
	}

	private static void ResetLayerTransform(Node2D layer, bool showBehindParent, Color modulate)
	{
		layer.Position = Vector2.Zero;
		layer.Scale = Vector2.One;
		layer.Rotation = 0f;
		layer.TopLevel = false;
		layer.ZAsRelative = true;
		layer.ZIndex = 0;
		layer.ShowBehindParent = showBehindParent;
		if (layer != null)
		{
			layer.Material = null;
			layer.UseParentMaterial = false;
			layer.Modulate = modulate;
		}
	}

	private static bool IsBehindParentLayer(string layerName, string behindParentLayerName)
	{
		if (!string.IsNullOrEmpty(behindParentLayerName))
		{
			return string.Equals(layerName, behindParentLayerName, StringComparison.Ordinal);
		}
		return false;
	}

	private static IEnumerable<Node> TraverseDepthFirst(Node root)
	{
		return GodotNodeUtil.TraverseDepthFirst(root);
	}

	private static IReadOnlyList<Node> GetNodes(Node root)
	{
		if (EffectNodeCaches.TryGetValue(root, out EffectNodeCache value))
		{
			return value.Nodes;
		}
		return FindNodesLive(root);
	}

	private static IReadOnlyList<Node> GetSpineSprites(Node root)
	{
		if (EffectNodeCaches.TryGetValue(root, out EffectNodeCache value))
		{
			return value.SpineSprites;
		}
		return FindSpineSpritesLive(root);
	}

	private static IReadOnlyList<CanvasItem> GetCanvasItems(Node root)
	{
		if (EffectNodeCaches.TryGetValue(root, out EffectNodeCache value))
		{
			return value.CanvasItems;
		}
		return FindCanvasItemsLive(root);
	}

	private static IReadOnlyList<Node> GetParticleNodes(Node root)
	{
		if (EffectNodeCaches.TryGetValue(root, out EffectNodeCache value))
		{
			return value.ParticleNodes;
		}
		return FindParticleNodesLive(root);
	}

	private static EffectNodeCache BuildEffectNodeCache(Node root)
	{
		List<Node> list = new List<Node>();
		List<Node> list2 = new List<Node>();
		List<CanvasItem> list3 = new List<CanvasItem>();
		List<Node> list4 = new List<Node>();
		foreach (Node item2 in TraverseDepthFirst(root))
		{
			list.Add(item2);
			string text = item2.GetClass();
			if (text == "SpineSprite")
			{
				list2.Add(item2);
			}
			if (IsParticleClass(text))
			{
				list4.Add(item2);
			}
			if (item2 is CanvasItem item)
			{
				list3.Add(item);
			}
		}
		return new EffectNodeCache(list, list2, list3, list4);
	}

	private static IReadOnlyList<Node> FindNodesLive(Node root)
	{
		List<Node> list = new List<Node>();
		foreach (Node item in TraverseDepthFirst(root))
		{
			list.Add(item);
		}
		return list;
	}

	private static IReadOnlyList<Node> FindSpineSpritesLive(Node root)
	{
		return GodotNodeUtil.FindNodesByClass(root, "SpineSprite");
	}

	private static IReadOnlyList<CanvasItem> FindCanvasItemsLive(Node root)
	{
		List<CanvasItem> list = new List<CanvasItem>();
		foreach (Node item2 in TraverseDepthFirst(root))
		{
			if (item2 is CanvasItem item)
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static IReadOnlyList<Node> FindParticleNodesLive(Node root)
	{
		List<Node> list = new List<Node>();
		foreach (Node item in TraverseDepthFirst(root))
		{
			if (IsParticleClass(item.GetClass()))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static bool IsParticleClass(string className)
	{
		if (!(className == "GPUParticles2D"))
		{
			return className == "CPUParticles2D";
		}
		return true;
	}

	private static void StartParticleNode(Node particleNode, int token, Action<string>? warn)
	{
		particleNode.Set("emitting", Variant.From<bool>(false));
		if (particleNode.HasMethod("restart"))
		{
			particleNode.Call("restart");
		}
		particleNode.Set("emitting", Variant.From<bool>(true));
		ScheduleParticleStop(particleNode, token, warn);
	}

	private static void ScheduleParticleStop(Node particleNode, int token, Action<string>? warn)
	{
		float particleEmissionDurationSeconds = GetParticleEmissionDurationSeconds(particleNode);
		if (particleEmissionDurationSeconds <= 0.001f)
		{
			return;
		}
		try
		{
			GodotScheduler.Delay(particleEmissionDurationSeconds, () =>
			{
				try
				{
					if (GodotObject.IsInstanceValid(particleNode) && IsParticleStartTokenCurrent(particleNode, token))
					{
						particleNode.Set("emitting", Variant.From<bool>(false));
					}
				}
				catch (Exception ex2)
				{
					warn?.Invoke(string.Concat(particleNode.Name, " particle stop failed: ", SkinLog.FormatException(ex2)));
				}
			});
		}
		catch (Exception ex)
		{
			warn?.Invoke(string.Concat(particleNode.Name, " particle stop scheduling failed: ", SkinLog.FormatException(ex)));
		}
	}

	private static int NextParticleStartToken(Node particleNode)
	{
		int from = GetParticleStartToken(particleNode) + 1;
		particleNode.SetMeta("czn_particle_start_token", Variant.From(in from));
		return from;
	}

	private static int GetParticleStartToken(Node particleNode)
	{
		try
		{
			if (particleNode.HasMeta("czn_particle_start_token"))
			{
				return particleNode.GetMeta("czn_particle_start_token").AsInt32();
			}
		}
		catch
		{
		}
		return 0;
	}

	private static bool IsParticleStartTokenCurrent(Node particleNode, int token)
	{
		return GetParticleStartToken(particleNode) == token;
	}

	private static float GetParticleStartDelaySeconds(Node particleNode)
	{
		try
		{
			if (particleNode.HasMeta("czn_particle_start_delay_seconds"))
			{
				return Math.Max(0f, particleNode.GetMeta("czn_particle_start_delay_seconds").AsSingle());
			}
		}
		catch
		{
		}
		return 0f;
	}

	private static float GetParticleEmissionDurationSeconds(Node particleNode)
	{
		try
		{
			if (particleNode.HasMeta("czn_particle_emission_duration_seconds"))
			{
				float num = particleNode.GetMeta("czn_particle_emission_duration_seconds").AsSingle();
				return float.IsFinite(num) ? Math.Max(0f, num) : 0f;
			}
		}
		catch
		{
		}
		return 0f;
	}
}
