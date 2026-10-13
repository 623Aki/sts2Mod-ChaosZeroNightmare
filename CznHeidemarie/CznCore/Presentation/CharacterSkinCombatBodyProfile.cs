using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed record CharacterSkinCombatBodyProfile(string IdleAnimation, string ReadyLoopAnimation, string ReadyExitAnimation, string[] EntrySequence, float EntryRunInOffsetX, float EntryRunInDurationSeconds, Vector2[] EntryRunInSegmentOffsets, string[] VictoryStartAnimations, string? VictoryLoopAnimation, string[] DeathSequence, float PostActionReadyDwellSeconds, float ReadyExitGraceSeconds, IReadOnlyDictionary<string, CharacterSkinRemoteNativeBodyAnimation>? RemoteNativeBodyAnimations = null, IReadOnlyList<string>? IncompatibleNativeVisualChildPaths = null)
{
	public string ActiveCombatIdleAnimation
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ReadyLoopAnimation))
			{
				return ReadyLoopAnimation;
			}
			return IdleAnimation;
		}
	}

	public IReadOnlyList<string> GetRequiredBindingAnimations()
	{
		List<string> list = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		AddAnimation(list, seen, IdleAnimation);
		AddAnimation(list, seen, ReadyLoopAnimation);
		AddAnimation(list, seen, ReadyExitAnimation);
		AddAnimations(list, seen, EntrySequence);
		AddAnimations(list, seen, VictoryStartAnimations);
		AddAnimation(list, seen, VictoryLoopAnimation);
		AddAnimations(list, seen, DeathSequence);
		if (RemoteNativeBodyAnimations != null)
		{
			foreach (CharacterSkinRemoteNativeBodyAnimation value in RemoteNativeBodyAnimations.Values)
			{
				AddAnimation(list, seen, value.AnimationName);
				AddAnimations(list, seen, value.AnimationSequence);
			}
		}
		return list;
	}

	public bool TryGetRemoteNativeBodyAnimation(string triggerName, out CharacterSkinRemoteNativeBodyAnimation animation)
	{
		animation = default;
		if (string.IsNullOrWhiteSpace(triggerName) || RemoteNativeBodyAnimations == null || RemoteNativeBodyAnimations.Count == 0)
		{
			return false;
		}
		if (RemoteNativeBodyAnimations.TryGetValue(triggerName, out animation))
		{
			return true;
		}
		foreach (KeyValuePair<string, CharacterSkinRemoteNativeBodyAnimation> remoteNativeBodyAnimation in RemoteNativeBodyAnimations)
		{
			if (string.Equals(remoteNativeBodyAnimation.Key, triggerName, StringComparison.OrdinalIgnoreCase))
			{
				animation = remoteNativeBodyAnimation.Value;
				return true;
			}
		}
		return false;
	}

	private static void AddAnimations(List<string> animations, HashSet<string> seen, IReadOnlyList<string>? values)
	{
		if (values == null)
		{
			return;
		}
		foreach (string value in values)
		{
			AddAnimation(animations, seen, value);
		}
	}

	private static void AddAnimation(List<string> animations, HashSet<string> seen, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
		{
			animations.Add(value);
		}
	}
}
