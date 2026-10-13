using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal static class GameOverVisualScaleRuntime
{
	public static int ScaleStandaloneSpineSprites(Node root, string scaleMeta, float scaleMultiplier, float maxStandaloneUnscaledSpineScale)
	{
		int num = 0;
		Stack<Node> stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
			if (node is Node2D node2D && !(node.GetClass() != "SpineSprite") && !node.HasMeta(scaleMeta))
			{
				Vector2 scale = node2D.Scale;
				if (!(Math.Max(Math.Abs(scale.X), Math.Abs(scale.Y)) > maxStandaloneUnscaledSpineScale))
				{
					node2D.Scale = CharacterSkinSurfaceDefaults.ResolveCznCombatBodySpineScale(scale, scaleMultiplier);
					node.SetMeta(scaleMeta, Variant.From<bool>(true));
					num++;
				}
			}
		}
		return num;
	}

	public static void ScheduleStandaloneDeathSequence(Node root, string deathSequenceMeta, IReadOnlyList<string> deathSequence, Action<string, int> info, Action<string, int> warn)
	{
		ArgumentNullException.ThrowIfNull(root, "root");
		ArgumentNullException.ThrowIfNull(deathSequence, "deathSequence");
		ArgumentNullException.ThrowIfNull(info, "info");
		ArgumentNullException.ThrowIfNull(warn, "warn");
		GodotScheduler.Deferred(() =>
		{
			PlayStandaloneDeathSequence(root, deathSequenceMeta, deathSequence, info, warn);
		});
	}

	private static void PlayStandaloneDeathSequence(Node root, string deathSequenceMeta, IReadOnlyList<string> deathSequence, Action<string, int> info, Action<string, int> warn)
	{
		try
		{
			if (TryPlayStandaloneDeathSequence(root, deathSequenceMeta, deathSequence, warn, out var pendingSpine) > 0)
			{
				info("Player game-over death animation sequence started: " + string.Join(" -> ", deathSequence), 2);
			}
			else if (pendingSpine)
			{
				warn("Player game-over death animation skipped: standalone SpineSprite animation state not ready.", 2);
			}
		}
		catch (Exception ex)
		{
			warn("Player game-over death animation failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private static int TryPlayStandaloneDeathSequence(Node root, string deathSequenceMeta, IReadOnlyList<string> deathSequence, Action<string, int> warn, out bool pendingSpine)
	{
		pendingSpine = false;
		if (!GodotObject.IsInstanceValid(root) || deathSequence.Count == 0)
		{
			return 0;
		}
		int num = 0;
		Stack<Node> stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
			if (!(node is Node2D node2D) || node.GetClass() != "SpineSprite" || node.HasMeta(deathSequenceMeta))
			{
				continue;
			}
			MegaSprite megaSprite = new MegaSprite(node2D);
			MegaAnimationState megaAnimationState = megaSprite.TryGetAnimationState();
			if (megaAnimationState == null)
			{
				pendingSpine = true;
				continue;
			}
			if (!HasStandaloneDeathSequence(megaSprite, deathSequence, warn))
			{
				node.SetMeta(deathSequenceMeta, Variant.From<bool>(true));
				continue;
			}
			SpineAnimationCompat.SetAnimation(megaAnimationState, deathSequence[0], loop: false);
			for (int i = 1; i < deathSequence.Count; i++)
			{
				SpineAnimationCompat.AddAnimation(megaAnimationState, deathSequence[i], 0f, loop: false);
			}
			node.SetMeta(deathSequenceMeta, Variant.From<bool>(true));
			num++;
		}
		return num;
	}

	private static bool HasStandaloneDeathSequence(MegaSprite sprite, IReadOnlyList<string> deathSequence, Action<string, int> warn)
	{
		foreach (string item in deathSequence)
		{
			if (!sprite.HasAnimation(item))
			{
				warn("Player game-over death animation missing body animation: " + item, 2);
				return false;
			}
		}
		return true;
	}
}
