using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Forms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace CznCore.Presentation;

internal static class CombatFormVfxSurfaceRuntime
{
	private const string CombatBodySurfaceMetaSuffix = "combat_body_surface_scaled";

	public static bool OwnsSurface(CharacterSkinProfile profile, Player? owner)
	{
		if (owner != null && profile.AppliesTo(owner) && profile.Features.CombatBodyScalePatch)
		{
			return !string.IsNullOrWhiteSpace(profile.BodySkeletonDataPath);
		}
		return false;
	}

	public static void Adapt(CharacterSkinProfile profile, NFormVfx formVfx, Player owner)
	{
		if (!OwnsSurface(profile, owner) || !TryResolveBoneName(profile.FormVfx, formVfx, out string boneName))
		{
			return;
		}
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(owner.Creature);
		if (nCreature == null || !GodotObject.IsInstanceValid(nCreature))
		{
			return;
		}
		Node2D currentBody = nCreature.Visuals.GetCurrentBody();
		if (currentBody == null || !GodotObject.IsInstanceValid(currentBody) || !currentBody.HasMeta(profile.RuntimeMetaKey("combat_body_surface_scaled")))
		{
			return;
		}
		MegaSprite spineBody = nCreature.Visuals.SpineBody;
		if (spineBody == null)
		{
			return;
		}
		if (!spineBody.GetGlobalBoneTransform(boneName).HasValue)
		{
			int num = RebindFollowers(formVfx, spineBody, string.Empty);
			SkinLog.Warn("Form VFX adapter could not resolve replacement bone '" + boneName + "' for " + formVfx.GetType().Name + "; disabled " + num + " bone follower(s) and kept the holder-relative fallback.");
			return;
		}
		int rebound = RebindFollowers(formVfx, spineBody, boneName);
		if (rebound == 0)
		{
			SkinLog.Warn("Form VFX adapter found no bone follower for " + formVfx.GetType().Name + ".");
			return;
		}
		SkinLog.Debug(() => "Adapted " + formVfx.GetType().Name + " to replacement bone '" + boneName + "' followers=" + rebound);
	}

	public static void DisableFailedVisual(NFormVfx? formVfx)
	{
		if (formVfx != null && GodotObject.IsInstanceValid(formVfx))
		{
			formVfx.Visible = false;
			formVfx.SetProcess(enable: false);
			formVfx.SetPhysicsProcess(enable: false);
		}
	}

	private static bool TryResolveBoneName(CharacterSkinFormVfxProfile profile, NFormVfx formVfx, out string boneName)
	{
		string text;
		if (formVfx is NReaperFormVfx)
		{
			text = profile.ReaperBodyBoneName;
		}
		else if (!(formVfx is NEchoFormVfx) && !(formVfx is NSerpentFormVfx))
		{
			text = ((!(formVfx is NVoidFormVfx)) ? string.Empty : profile.HeadBoneName);
		}
		else
		{
			text = profile.UpperBodyBoneName;
		}
		boneName = text;
		return !string.IsNullOrWhiteSpace(boneName);
	}

	private static int RebindFollowers(Node node, MegaSprite spineSprite, string boneName)
	{
		int num = 0;
		if (node is NSpineSpriteBoneFollower nSpineSpriteBoneFollower)
		{
			nSpineSpriteBoneFollower.SetSpineSprite(spineSprite, boneName);
			num++;
		}
		foreach (Node child in node.GetChildren())
		{
			num += RebindFollowers(child, spineSprite, boneName);
		}
		return num;
	}
}
