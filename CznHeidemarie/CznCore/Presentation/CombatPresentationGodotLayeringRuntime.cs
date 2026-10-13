using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal static class CombatPresentationGodotLayeringRuntime
{
	public const int CanvasItemZIndexMin = -4096;

	public const int CanvasItemZIndexMax = 4096;

	private const string OriginalZIndexMeta = "czn_core_presentation_original_z_index";

	private const string OriginalZAsRelativeMeta = "czn_core_presentation_original_z_as_relative";

	private const string EffectiveSourceZIndexMeta = "czn_core_presentation_effective_source_z_index";

	private const string EffectiveSafeZIndexMeta = "czn_core_presentation_effective_safe_z_index";

	public static int ResolveCueZIndex(CombatPresentationEffectCue cue)
	{
		return CombatPresentationDepthRuntime.ResolveAttachedEffectZIndex(cue);
	}

	public static void PlaceEffectRoot(Node2D parent, Node2D effectRoot, CombatPresentationEffectCue cue)
	{
		if (GodotObject.IsInstanceValid(parent) && GodotObject.IsInstanceValid(effectRoot))
		{
			int value = ((!CombatPresentationDepthRuntime.IsScreenAnchor(cue.Anchor)) ? (cue.GodotZIndex ?? cue.ZIndex) : 0);
			NormalizeEffectSubtree(effectRoot, 0, 0, value);
			SortNormalizedChildren(parent);
		}
	}

	public static int ClampCanvasItemZIndex(int zIndex)
	{
		return Math.Clamp(zIndex, -4096, 4096);
	}

	private static void NormalizeEffectSubtree(Node node, int parentSourceZIndex, int parentSafeZIndex, int? rootSourceZIndex = null)
	{
		int from = parentSourceZIndex;
		int from2 = parentSafeZIndex;
		if (node is CanvasItem canvasItem)
		{
			(int ZIndex, bool ZAsRelative) tuple = ReadOrCaptureOriginalDepth(canvasItem);
			int item = tuple.ZIndex;
			bool item2 = tuple.ZAsRelative;
			from = rootSourceZIndex ?? (item2 ? (parentSourceZIndex + item) : item);
			from2 = Math.Sign(from);
			canvasItem.ZAsRelative = true;
			canvasItem.ZIndex = from2 - parentSafeZIndex;
			canvasItem.SetMeta("czn_core_presentation_effective_source_z_index", Variant.From(in from));
			canvasItem.SetMeta("czn_core_presentation_effective_safe_z_index", Variant.From(in from2));
		}
		foreach (Node child in node.GetChildren())
		{
			NormalizeEffectSubtree(child, from, from2);
		}
		SortNormalizedChildren(node);
	}

	private static (int ZIndex, bool ZAsRelative) ReadOrCaptureOriginalDepth(CanvasItem item)
	{
		try
		{
			if (item.HasMeta("czn_core_presentation_original_z_index") && item.HasMeta("czn_core_presentation_original_z_as_relative"))
			{
				return (ZIndex: item.GetMeta("czn_core_presentation_original_z_index").AsInt32(), ZAsRelative: item.GetMeta("czn_core_presentation_original_z_as_relative").AsBool());
			}
		}
		catch
		{
		}
		int from = item.ZIndex;
		bool from2 = item.ZAsRelative;
		item.SetMeta("czn_core_presentation_original_z_index", Variant.From(in from));
		item.SetMeta("czn_core_presentation_original_z_as_relative", Variant.From(in from2));
		return (ZIndex: from, ZAsRelative: from2);
	}

	private static void SortNormalizedChildren(Node parent)
	{
		List<CanvasItem> list = new List<CanvasItem>();
		foreach (Node child in parent.GetChildren())
		{
			if (child is CanvasItem canvasItem && canvasItem.HasMeta("czn_core_presentation_effective_source_z_index") && canvasItem.HasMeta("czn_core_presentation_effective_safe_z_index"))
			{
				list.Add(canvasItem);
			}
		}
		list.Sort((CanvasItem left, CanvasItem right) =>
		{
			int num = ReadDepthMeta(left, "czn_core_presentation_effective_safe_z_index").CompareTo(ReadDepthMeta(right, "czn_core_presentation_effective_safe_z_index"));
			if (num != 0)
			{
				return num;
			}
			int num2 = ReadDepthMeta(left, "czn_core_presentation_effective_source_z_index").CompareTo(ReadDepthMeta(right, "czn_core_presentation_effective_source_z_index"));
			return (num2 == 0) ? left.GetIndex().CompareTo(right.GetIndex()) : num2;
		});
		foreach (CanvasItem item in list)
		{
			parent.MoveChild(item, parent.GetChildCount() - 1);
		}
	}

	private static int ReadDepthMeta(CanvasItem item, string metaName)
	{
		try
		{
			return item.GetMeta(metaName).AsInt32();
		}
		catch
		{
			return 0;
		}
	}
}
