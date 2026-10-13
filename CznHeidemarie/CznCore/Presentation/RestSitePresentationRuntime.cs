using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace CznCore.Presentation;

internal sealed class RestSitePresentationRuntime(RestSitePresentationProfile profile, IRestSitePresentationRuntimeContext context)
{
	public RestSitePresentationResult Apply(NRestSiteCharacter restSiteCharacter)
	{
		ApplySurface(restSiteCharacter);
		bool applied = false;
		Node2D node2D = null;
		string text = string.Empty;
		foreach (Node item in context.FindSpineSprites(restSiteCharacter))
		{
			if (!(item is Node2D node2D2) || item.GetClass() != "SpineSprite")
			{
				continue;
			}
			MegaSprite megaSprite = new MegaSprite(node2D2);
			string text2 = ResolveAnimation(megaSprite);
			if (string.IsNullOrEmpty(text2))
			{
				continue;
			}
			MegaAnimationState animationState = megaSprite.GetAnimationState();
			if (animationState == null)
			{
				continue;
			}
			MegaTrackEntry megaTrackEntry = SpineAnimationCompat.SetAnimationTracked(animationState, text2);
			try
			{
				megaTrackEntry?.SetTrackTime(megaTrackEntry.GetAnimationEnd() * GD.Randf());
			}
			finally
			{
				SpineAnimationCompat.ReleaseTracked(megaTrackEntry);
			}
			if (string.Equals(text2, profile.PrimaryAnimationName, StringComparison.Ordinal))
			{
				ApplyBodyOffset(node2D2);
				ApplyHiddenSlots(megaSprite);
				ApplyHiddenSlotsDeferred(restSiteCharacter, megaSprite);
				if (node2D == null)
				{
					node2D = node2D2;
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				text = text2;
			}
			applied = true;
		}
		bool hasCampingEffects = node2D != null && AttachCampingEffects(node2D);
		return new RestSitePresentationResult(applied, text, hasCampingEffects);
	}

	private void ApplySurface(Node root)
	{
		RestSiteSurfaceProfile restSiteSurfaceProfile = profile.Surface ?? CharacterSkinSurfaceDefaults.CznRestSiteSurface;
		if (!restSiteSurfaceProfile.ApplySceneLayout)
		{
			return;
		}
		foreach (Node item in context.FindSpineSprites(root))
		{
			if (item is Node2D node2D && !(item.GetClass() != "SpineSprite"))
			{
				node2D.Position = restSiteSurfaceProfile.SpinePosition;
				node2D.Scale = restSiteSurfaceProfile.SpineScale;
			}
		}
		ApplyControlRect(root, "ControlRoot", restSiteSurfaceProfile.ControlRoot);
		ApplyControlRect(root, "ControlRoot/SelectionReticle", restSiteSurfaceProfile.SelectionReticle);
		ApplyControlRect(root, "ControlRoot/Hitbox", restSiteSurfaceProfile.Hitbox);
		ApplyControlRect(root, "ControlRoot/ThoughtBubbleRight", restSiteSurfaceProfile.ThoughtBubbleRight);
		ApplyControlRect(root, "ControlRoot/ThoughtBubbleLeft", restSiteSurfaceProfile.ThoughtBubbleLeft);
	}

	private static void ApplyControlRect(Node root, string path, RestSiteControlRectProfile? rect)
	{
		if (!(rect == null))
		{
			Control nodeOrNull = root.GetNodeOrNull<Control>(path);
			if (nodeOrNull != null)
			{
				nodeOrNull.Set("offset_left", Variant.From<float>(rect.Left));
				nodeOrNull.Set("offset_top", Variant.From<float>(rect.Top));
				nodeOrNull.Set("offset_right", Variant.From<float>(rect.Right));
				nodeOrNull.Set("offset_bottom", Variant.From<float>(rect.Bottom));
			}
		}
	}

	public void Normalize(Node root)
	{
		foreach (Node item in FindCampingEffectNodes(root))
		{
			if (item is Node2D node2D)
			{
				string nodeName = node2D.Name.ToString();
				if (TryGetCampingLayerProfile(nodeName, out RestSiteCampingLayerProfile layerProfile))
				{
					NormalizeCampingLayer(node2D, layerProfile.BehindParent, node2D.GetParent() as CanvasItem);
				}
			}
		}
	}

	private string ResolveAnimation(MegaSprite sprite)
	{
		if (sprite.HasAnimation(profile.PrimaryAnimationName))
		{
			return profile.PrimaryAnimationName;
		}
		foreach (string fallbackAnimationName in profile.FallbackAnimationNames)
		{
			if (sprite.HasAnimation(fallbackAnimationName))
			{
				return fallbackAnimationName;
			}
		}
		return string.Empty;
	}

	private void ApplyBodyOffset(Node2D bodyNode)
	{
		Vector2 bodyOffset = profile.BodyOffset;
		if (!(bodyOffset == Vector2.Zero))
		{
			Vector2 from;
			if (bodyNode.HasMeta(profile.BodyBasePositionMeta))
			{
				from = bodyNode.GetMeta(profile.BodyBasePositionMeta).AsVector2();
			}
			else
			{
				from = bodyNode.Position;
				bodyNode.SetMeta(profile.BodyBasePositionMeta, Variant.From(in from));
			}
			bodyNode.Position = from + bodyOffset;
		}
	}

	private void ApplyHiddenSlots(MegaSprite sprite)
	{
		if (profile.HiddenSlotNames == null || profile.HiddenSlotNames.Count == 0)
		{
			return;
		}
		MegaSkeleton skeleton = sprite.GetSkeleton();
		if (skeleton == null)
		{
			return;
		}
		GodotObject boundObject = skeleton.BoundObject;
		foreach (string hiddenSlotName in profile.HiddenSlotNames)
		{
			string from = hiddenSlotName;
			if (string.IsNullOrWhiteSpace(from))
			{
				continue;
			}
			try
			{
				GodotObject godotObject = boundObject.Call("find_slot", Variant.From(in from)).AsGodotObject();
				if (godotObject == null)
				{
					context.LogWarn("Rest-site hidden slot not found: " + from);
					continue;
				}
				Color from2 = (godotObject.HasMethod("get_color") ? godotObject.Call("get_color").AsColor() : Colors.White);
				from2.A = 0f;
				godotObject.Call("set_color", Variant.From(in from2));
			}
			catch (Exception ex)
			{
				context.LogWarn("Rest-site hidden slot apply failed: " + from + " " + SkinLog.FormatException(ex));
			}
		}
	}

	private void ApplyHiddenSlotsDeferred(Node host, MegaSprite sprite)
	{
		if (profile.HiddenSlotNames == null || profile.HiddenSlotNames.Count == 0)
		{
			return;
		}
		GodotScheduler.Deferred(() =>
		{
			if (GodotObject.IsInstanceValid(host) && GodotObject.IsInstanceValid(sprite.BoundObject))
			{
				ApplyHiddenSlots(sprite);
			}
		});
	}

	private bool AttachCampingEffects(Node2D bodyNode)
	{
		if (profile.CampingLayers.Count == 0)
		{
			return false;
		}
		bool flag = false;
		foreach (RestSiteCampingLayerProfile campingLayer in profile.CampingLayers)
		{
			if (bodyNode.GetNodeOrNull<Node2D>(campingLayer.NodeName) != null)
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			Normalize(bodyNode);
			return true;
		}
		bool result = false;
		foreach (RestSiteCampingLayerProfile campingLayer2 in profile.CampingLayers)
		{
			if (CreateCampingLayer(campingLayer2) is Node2D node2D)
			{
				bodyNode.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
				NormalizeCampingLayer(node2D, campingLayer2.BehindParent, bodyNode);
				context.ApplyLayerMaterial(node2D);
				context.ApplyLayerMaterialDeferred(node2D, 4);
				context.StartLayerEffects(node2D, visible: true);
				result = true;
			}
		}
		return result;
	}

	private void NormalizeCampingLayer(Node2D node, bool behindParent, CanvasItem? bodyItem)
	{
		node.Position = Vector2.Zero;
		node.Scale = Vector2.One;
		node.Rotation = 0f;
		node.TopLevel = false;
		node.ZAsRelative = true;
		node.ZIndex = 0;
		node.ShowBehindParent = behindParent;
		if (bodyItem != null)
		{
			node.Visible = bodyItem.Visible;
			node.VisibilityLayer = bodyItem.VisibilityLayer;
			node.LightMask = bodyItem.LightMask;
		}
	}

	private List<Node> FindCampingEffectNodes(Node root)
	{
		List<Node> list = new List<Node>();
		Stack<Node> stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			string nodeName = node.Name.ToString();
			if (TryGetCampingLayerProfile(nodeName, out RestSiteCampingLayerProfile _))
			{
				list.Add(node);
				continue;
			}
			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
		}
		return list;
	}

	private bool TryGetCampingLayerProfile(string nodeName, out RestSiteCampingLayerProfile? layerProfile)
	{
		foreach (RestSiteCampingLayerProfile campingLayer in profile.CampingLayers)
		{
			if (string.Equals(nodeName, campingLayer.NodeName, StringComparison.Ordinal))
			{
				layerProfile = campingLayer;
				return true;
			}
		}
		layerProfile = null;
		return false;
	}

	private Node? CreateCampingLayer(RestSiteCampingLayerProfile layer)
	{
		try
		{
			GodotObject from = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
			GodotObject from2 = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
			GodotObject from3 = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
			Node node = ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node;
			if (from == null || from2 == null || from3 == null || node == null)
			{
				return null;
			}
			Variant variant = from.Call("load_from_atlas_file", layer.AtlasPath);
			Variant variant2 = from2.Call("load_from_file", layer.SkeletonPath);
			if (variant.AsInt32() != 0 || variant2.AsInt32() != 0)
			{
				context.LogWarn("Rest-site camping layer load failed: " + layer.NodeName + " atlasErr=" + variant.AsInt32() + " skelErr=" + variant2.AsInt32());
				return null;
			}
			from3.Set("atlas_res", Variant.From(in from));
			from3.Set("skeleton_file_res", Variant.From(in from2));
			node.Name = layer.NodeName;
			node.Set("skeleton_data_res", Variant.From(in from3));
			return node;
		}
		catch (Exception ex)
		{
			context.LogWarn("Rest-site camping layer instantiate failed: " + layer.NodeName + " " + SkinLog.FormatException(ex));
			return null;
		}
	}
}
