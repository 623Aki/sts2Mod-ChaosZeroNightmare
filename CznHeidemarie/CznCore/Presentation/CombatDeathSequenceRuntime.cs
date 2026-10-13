using System;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class CombatDeathSequenceRuntime(CharacterSkinProfile profile)
{
	public void Schedule(Creature creature, Action<NCreature, string, bool>? applyBodyEffectState, Action<string, int> info, Action<string, int> warn, CombatBodySurfaceCoordinator? bodySurface = null)
	{
		ArgumentNullException.ThrowIfNull(creature, "creature");
		GodotScheduler.Deferred(() =>
		{
			RunScheduled(creature, applyBodyEffectState, info, warn, bodySurface);
		});
	}

	private void RunScheduled(Creature creature, Action<NCreature, string, bool>? applyBodyEffectState, Action<string, int> info, Action<string, int> warn, CombatBodySurfaceCoordinator? bodySurface)
	{
		try
		{
			Play(creature, applyBodyEffectState, info, warn, bodySurface);
		}
		catch (Exception ex)
		{
			warn("Player death animation failed: " + FormatDeathSequence() + " " + SkinLog.FormatException(ex), 2);
		}
	}

	private void Play(Creature creature, Action<NCreature, string, bool>? applyBodyEffectState, Action<string, int> info, Action<string, int> warn, CombatBodySurfaceCoordinator? bodySurface)
	{
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(creature);
		if (nCreature == null || !GodotObject.IsInstanceValid(nCreature))
		{
			warn("Player death animation skipped: combat creature node not found for " + FormatDeathSequence(), 2);
			return;
		}
		string[] deathSequence = profile.CombatBody.DeathSequence;
		if (deathSequence.Length == 0)
		{
			throw new InvalidOperationException("Death sequence is empty.");
		}
		if (bodySurface != null)
		{
			bodySurface.Ensure(nCreature, "combat_death_sequence");
			string[] array = deathSequence;
			foreach (string text in array)
			{
				if (!bodySurface.HasAnimation(nCreature, text, profile.ProfileId + " combat death"))
				{
					warn("Player death animation skipped missing body animation: " + text, 2);
					return;
				}
			}
		}
		applyBodyEffectState?.Invoke(nCreature, deathSequence[0], arg3: false);
		SpineAnimationCompat.SetAnimation(nCreature.SpineAnimation, deathSequence[0], loop: false);
		for (int j = 1; j < deathSequence.Length; j++)
		{
			SpineAnimationCompat.AddAnimation(nCreature.SpineAnimation, deathSequence[j], 0f, loop: false);
		}
		info("Player death animation sequence started: " + FormatDeathSequence(), 2);
	}

	private string FormatDeathSequence()
	{
		return string.Join(" -> ", profile.CombatBody.DeathSequence);
	}
}
