using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatBodyEntryPlaybackRuntime
{
	private const string EntryRunInGenerationMeta = "czn_combat_entry_run_in_generation";

	private const string EntryRunInTargetPositionMeta = "czn_combat_entry_run_in_target_position";

	private const float EntryRunInCompletionPadSeconds = 0.08f;

	private static int _entryRunInGeneration;

	public static IReadOnlyList<CombatPresentationBodyStep> BuildEntrySteps(CharacterSkinCombatBodyProfile profile)
	{
		return BuildBodySteps(profile.EntrySequence);
	}

	public static IReadOnlyList<CombatPresentationBodyStep> BuildBodySteps(IReadOnlyList<string> animations)
	{
		if (animations.Count == 0)
		{
			return Array.Empty<CombatPresentationBodyStep>();
		}
		CombatPresentationBodyStep[] array = new CombatPresentationBodyStep[animations.Count];
		for (int i = 0; i < animations.Count; i++)
		{
			array[i] = new CombatPresentationBodyStep(animations[i]);
		}
		return array;
	}

	public static bool IsEntrySequence(CharacterSkinCombatBodyProfile profile, IReadOnlyList<CombatPresentationBodyStep> steps)
	{
		if (profile.EntrySequence.Length == 0 || steps.Count < profile.EntrySequence.Length)
		{
			return false;
		}
		for (int i = 0; i < profile.EntrySequence.Length; i++)
		{
			if (!string.Equals(steps[i].BodyAnimation, profile.EntrySequence[i], StringComparison.Ordinal))
			{
				return false;
			}
		}
		return true;
	}

	public static CombatBodyEntryRunInStart TryBeginRunIn(CharacterSkinCombatBodyProfile profile, IReadOnlyList<CombatPresentationBodyStep> steps, NCreature actorNode, Action<string>? debug = null, Action<string>? warn = null)
	{
		try
		{
			if (!HasRunIn(profile) || actorNode == null || !GodotObject.IsInstanceValid(actorNode))
			{
				return CombatBodyEntryRunInStart.None;
			}
			if (!IsEntrySequence(profile, steps))
			{
				CompleteActiveRunIn(actorNode, "non_entry_sequence", debug, warn);
				return CombatBodyEntryRunInStart.None;
			}
			CompleteActiveRunIn(actorNode, "entry_restart", debug, warn);
			Vector2 position = actorNode.Visuals.Position;
			int generation = BeginRunIn(actorNode, position);
			if (profile.EntryRunInSegmentOffsets.Length != 0)
			{
				ApplySegmentOffset(actorNode, position, profile.EntryRunInSegmentOffsets[0]);
				debug?.Invoke("Combat entry segment offset started: " + profile.EntryRunInSegmentOffsets[0]);
				return new CombatBodyEntryRunInStart(Started: true, UsesSegmentOffsets: true, position, generation);
			}
			actorNode.Visuals.Position = position + new Vector2(profile.EntryRunInOffsetX, 0f);
			Tween tween = actorNode.Visuals.CreateTween();
			tween.SetEase(Tween.EaseType.Out);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(actorNode.Visuals, "position:x", position.X, profile.EntryRunInDurationSeconds);
			debug?.Invoke("Combat entry run-in started: " + profile.EntryRunInOffsetX + " -> 0");
			return new CombatBodyEntryRunInStart(Started: true, UsesSegmentOffsets: false, position, generation);
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat entry run-in failed: " + SkinLog.FormatException(ex));
			return CombatBodyEntryRunInStart.None;
		}
	}

	public static bool TryGetSegmentOffset(CharacterSkinCombatBodyProfile profile, int stepIndex, out Vector2 offset)
	{
		if (stepIndex <= 0 || stepIndex >= profile.EntryRunInSegmentOffsets.Length)
		{
			offset = default;
			return false;
		}
		offset = profile.EntryRunInSegmentOffsets[stepIndex];
		return true;
	}

	public static void ApplySegmentOffset(NCreature actorNode, Vector2 targetPosition, Vector2 offset)
	{
		if (actorNode != null && GodotObject.IsInstanceValid(actorNode))
		{
			actorNode.Visuals.Position = targetPosition + offset;
		}
	}

	public static void ScheduleCompletion(NCreature actorNode, CombatBodyEntryRunInStart runIn, float sequenceDelaySeconds, Action<string>? debug = null, Action<string>? warn = null)
	{
		if (!runIn.Started || actorNode == null || !GodotObject.IsInstanceValid(actorNode))
		{
			return;
		}
		float seconds = Math.Max(0.05f, sequenceDelaySeconds + 0.08f);
		try
		{
			GodotScheduler.Delay(seconds, () =>
			{
				CompleteRunInIfCurrent(actorNode, runIn, "scheduled_completion", debug, warn);
			});
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat entry run-in completion scheduling failed: " + SkinLog.FormatException(ex));
			CompleteRunInIfCurrent(actorNode, runIn, "completion_schedule_failed", debug, warn);
		}
	}

	public static void CompleteRunInIfCurrent(NCreature actorNode, CombatBodyEntryRunInStart runIn, string reason, Action<string>? debug = null, Action<string>? warn = null)
	{
		if (!runIn.Started || runIn.Generation <= 0)
		{
			return;
		}
		try
		{
			if (actorNode != null && GodotObject.IsInstanceValid(actorNode))
			{
				if (IsRunInGenerationCurrent(actorNode, runIn.Generation))
				{
					actorNode.Visuals.Position = runIn.TargetPosition;
					ClearRunInMeta(actorNode);
					debug?.Invoke("Combat entry run-in completed: " + reason);
				}
			}
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat entry run-in completion failed: " + SkinLog.FormatException(ex));
		}
	}

	public static void CompleteActiveRunIn(NCreature actorNode, string reason, Action<string>? debug = null, Action<string>? warn = null)
	{
		try
		{
			if (actorNode != null && GodotObject.IsInstanceValid(actorNode))
			{
				if (actorNode.HasMeta("czn_combat_entry_run_in_target_position"))
				{
					Vector2 position = actorNode.GetMeta("czn_combat_entry_run_in_target_position").AsVector2();
					actorNode.Visuals.Position = position;
					ClearRunInMeta(actorNode);
					debug?.Invoke("Combat entry run-in completed: " + reason);
				}
			}
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat entry run-in completion failed: " + SkinLog.FormatException(ex));
		}
	}

	public static bool HasRunIn(CharacterSkinCombatBodyProfile profile)
	{
		if (profile.EntryRunInSegmentOffsets.Length == 0)
		{
			if (profile.EntryRunInDurationSeconds > 0f)
			{
				return profile.EntryRunInOffsetX != 0f;
			}
			return false;
		}
		return true;
	}

	private static int BeginRunIn(NCreature actorNode, Vector2 targetPosition)
	{
		int from = NextRunInGeneration();
		actorNode.SetMeta("czn_combat_entry_run_in_generation", Variant.From(in from));
		actorNode.SetMeta("czn_combat_entry_run_in_target_position", Variant.From(in targetPosition));
		return from;
	}

	private static int NextRunInGeneration()
	{
		_entryRunInGeneration++;
		if (_entryRunInGeneration <= 0)
		{
			_entryRunInGeneration = 1;
		}
		return _entryRunInGeneration;
	}

	private static bool IsRunInGenerationCurrent(GodotObject actorObject, int generation)
	{
		if (actorObject.HasMeta("czn_combat_entry_run_in_generation"))
		{
			return actorObject.GetMeta("czn_combat_entry_run_in_generation").AsInt32() == generation;
		}
		return false;
	}

	private static void ClearRunInMeta(GodotObject actorObject)
	{
		if (actorObject.HasMeta("czn_combat_entry_run_in_generation"))
		{
			actorObject.RemoveMeta("czn_combat_entry_run_in_generation");
		}
		if (actorObject.HasMeta("czn_combat_entry_run_in_target_position"))
		{
			actorObject.RemoveMeta("czn_combat_entry_run_in_target_position");
		}
	}
}
