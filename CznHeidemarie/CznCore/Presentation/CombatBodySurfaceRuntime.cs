using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatBodySurfaceRuntime(CharacterSkinProfile profile, CombatCreatureSpineResolver spineResolver, Action<string>? warn = null, Action<string>? debug = null)
{
	private readonly string _scaleMeta = profile.RuntimeMetaKey("combat_body_surface_scaled");

	private readonly string _positionMeta = profile.RuntimeMetaKey("combat_body_surface_position_offset");

	public CombatBodySurfaceState GetState(NCreature? creature)
	{
		if (!profile.Features.CombatBodyScalePatch)
		{
			return new CombatBodySurfaceState("feature_disabled", FeatureEnabled: false, creature != null && GodotObject.IsInstanceValid(creature), SpineFound: false, Applied: false, null, null);
		}
		if (creature == null || !GodotObject.IsInstanceValid(creature))
		{
			return new CombatBodySurfaceState("creature_invalid", FeatureEnabled: true, CreatureValid: false, SpineFound: false, Applied: false, null, null);
		}
		Node2D node2D = spineResolver.Find(creature);
		if (node2D == null)
		{
			return new CombatBodySurfaceState("spine_not_found", FeatureEnabled: true, CreatureValid: true, SpineFound: false, Applied: false, null, null);
		}
		GodotObject godotObject = node2D;
		bool flag = godotObject.HasMeta(_scaleMeta);
		return new CombatBodySurfaceState(flag ? "already_applied" : "pending", FeatureEnabled: true, CreatureValid: true, SpineFound: true, flag, node2D.Scale, godotObject.GetClass());
	}

	public bool Apply(NCreature creature)
	{
		if (!profile.Features.CombatBodyScalePatch)
		{
			return false;
		}
		Node2D node2D = spineResolver.Find(creature);
		if (node2D == null)
		{
			return false;
		}
		GodotObject godotObject = node2D;
		if (godotObject.HasMeta(_scaleMeta))
		{
			return ApplyPositionOffset(node2D, godotObject);
		}
		if (!string.IsNullOrWhiteSpace(profile.BodySkeletonDataPath))
		{
			RebindSkeleton(creature, node2D);
		}
		Vector2 scale = node2D.Scale;
		node2D.Scale = CharacterSkinSurfaceDefaults.ResolveCznCombatBodySpineScale(scale, profile.BodySpineScaleMultiplier);
		ApplyPositionOffset(node2D, godotObject);
		godotObject.SetMeta(_scaleMeta, Variant.From<bool>(true));
		debug?.Invoke("Applied combat body surface scale: " + scale.ToString() + " -> " + node2D.Scale);
		return true;
	}

	private bool ApplyPositionOffset(Node2D spine, GodotObject spineObject)
	{
		if (!profile.Features.CombatPositionOffsetPatch || profile.CombatPositionOffset == Vector2.Zero || spineObject.HasMeta(_positionMeta))
		{
			return false;
		}
		Vector2 position = spine.Position;
		spine.Position += profile.CombatPositionOffset;
		spineObject.SetMeta(_positionMeta, Variant.From<bool>(true));
		debug?.Invoke("Applied combat body surface position offset: " + position.ToString() + " -> " + spine.Position);
		return true;
	}

	private void RebindSkeleton(NCreature creature, Node2D spine)
	{
		Resource resource = ResourceLoader.Load<Resource>(profile.BodySkeletonDataPath, null, ResourceLoader.CacheMode.Reuse);
		if (resource == null)
		{
			throw new InvalidOperationException("Missing combat body skeleton data: " + profile.BodySkeletonDataPath);
		}
		RemoveIncompatibleNativeVisualChildren(spine);
		MegaSprite megaSprite = new MegaSprite(spine);
		GodotObject from = resource;
		megaSprite.SetSkeletonDataRes(new MegaSkeletonDataResource(Variant.From(in from)));
		string idleAnimation = profile.CombatBody.IdleAnimation;
		if (!string.IsNullOrWhiteSpace(idleAnimation))
		{
			if (!megaSprite.HasAnimation(idleAnimation))
			{
				throw new InvalidOperationException("Combat body skeleton missing idle animation: " + idleAnimation);
			}
			SpineAnimationCompat.SetAnimation(megaSprite.GetAnimationState(), idleAnimation);
		}
		if (spine != null)
		{
			spine.Visible = true;
		}
		spineResolver.TrySetVisualsSpineBody(creature.Visuals, spine, warn);
		debug?.Invoke("Rebound combat body skeleton: " + profile.BodySkeletonDataPath);
	}

	private void RemoveIncompatibleNativeVisualChildren(Node2D spine)
	{
		IReadOnlyList<string> incompatibleNativeVisualChildPaths = profile.CombatBody.IncompatibleNativeVisualChildPaths;
		if (incompatibleNativeVisualChildPaths == null || incompatibleNativeVisualChildPaths.Count == 0)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (string item in incompatibleNativeVisualChildPaths)
		{
			if (!string.IsNullOrWhiteSpace(item))
			{
				Node nodeOrNull = spine.GetNodeOrNull<Node>(item);
				if (nodeOrNull != null && spine.IsAncestorOf(nodeOrNull))
				{
					nodeOrNull.GetParent()?.RemoveChild(nodeOrNull);
					nodeOrNull.Free();
					list.Add(item);
				}
			}
		}
		if (list.Count > 0)
		{
			debug?.Invoke("Removed incompatible native combat visual children: " + string.Join(", ", list));
		}
	}
}
