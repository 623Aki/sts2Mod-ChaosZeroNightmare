using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal static class CombatBodyRenderWarmup
{
	private static readonly object WarmupSync = new object();

	private static readonly HashSet<string> CompletedProfiles = new HashSet<string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Task> ActiveWarmups = new Dictionary<string, Task>(StringComparer.Ordinal);

	public static Task WarmAsync(Node host, CharacterSkinProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile, "profile");
		lock (WarmupSync)
		{
			if (CompletedProfiles.Contains(profile.ProfileId))
			{
				return Task.CompletedTask;
			}
			if (ActiveWarmups.TryGetValue(profile.ProfileId, out Task value))
			{
				return value;
			}
			Task task = WarmAndRecordAsync(host, profile);
			ActiveWarmups[profile.ProfileId] = task;
			return task;
		}
	}

	private static async Task WarmAndRecordAsync(Node host, CharacterSkinProfile profile)
	{
		bool completed = false;
		try
		{
			completed = await WarmCoreAsync(host, profile);
		}
		finally
		{
			lock (WarmupSync)
			{
				ActiveWarmups.Remove(profile.ProfileId);
				if (completed)
				{
					CompletedProfiles.Add(profile.ProfileId);
				}
			}
		}
	}

	private static async Task<bool> WarmCoreAsync(Node host, CharacterSkinProfile profile)
	{
		if (string.IsNullOrWhiteSpace(profile.BodySkeletonDataPath) || !(Engine.GetMainLoop() is SceneTree { Root: not null } sceneTree))
		{
			return false;
		}
		ulong startedMs = Time.GetTicksMsec();
		Node warmNode = null;
		try
		{
			Resource resource = ResourceLoader.Load<Resource>(profile.BodySkeletonDataPath, null, ResourceLoader.CacheMode.Reuse);
			if (resource == null || !(ClassDB.Instantiate("SpineSprite").AsGodotObject() is Node node))
			{
				return false;
			}
			warmNode = node;
			node.Name = profile.RuntimeNodeName("CombatBodyRenderWarmup");
			MegaSprite megaSprite = new MegaSprite(node);
			GodotObject from = resource;
			megaSprite.SetSkeletonDataRes(new MegaSkeletonDataResource(Variant.From(in from)));
			if (node is Node2D node2D)
			{
				node2D.Position = Vector2.Zero;
				node2D.Scale = CharacterSkinSurfaceDefaults.ResolveCznCombatBodySpineScale(Vector2.One, profile.BodySpineScaleMultiplier);
				node2D.Rotation = 0f;
				node2D.ZAsRelative = true;
				node2D.ZIndex = 0;
				node2D.Modulate = new Color(1f, 1f, 1f, 0.0015f);
				node2D.Visible = true;
			}
			sceneTree.Root.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
			MegaAnimationState animationState = megaSprite.GetAnimationState();
			int warmed = 0;
			foreach (string item in EnumerateWarmupAnimations(profile))
			{
				if (!string.IsNullOrEmpty(item) && megaSprite.HasAnimation(item))
				{
					SpineAnimationCompat.SetAnimation(animationState, item, loop: false);
					await RunStartResourcePreloader.AwaitFrame(host);
					await RunStartResourcePreloader.AwaitFrame(host);
					warmed++;
				}
			}
			SkinLog.Info("Combat body render warmup completed animations=" + warmed + " elapsedMs=" + (Time.GetTicksMsec() - startedMs));
			return warmed > 0;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Combat body render warmup failed elapsedMs=" + (Time.GetTicksMsec() - startedMs) + ": " + SkinLog.FormatException(ex));
			return false;
		}
		finally
		{
			if (warmNode != null && GodotObject.IsInstanceValid(warmNode))
			{
				warmNode.QueueFree();
			}
		}
	}

	private static IEnumerable<string> EnumerateWarmupAnimations(CharacterSkinProfile profile)
	{
		string[] entrySequence = profile.CombatBody.EntrySequence;
		for (int i = 0; i < entrySequence.Length; i++)
		{
			yield return entrySequence[i];
		}
		yield return profile.CombatBody.IdleAnimation;
	}
}
