using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace CznCore.Presentation;

internal static class CombatCameraShakeRuntime
{
	private sealed class AdditivePlaybackState
	{
		public Vector2 BasePosition { get; private set; }

		public Tween? Tween { get; set; }

		public bool IsActive { get; private set; }

		private int Generation { get; set; }

		public int Begin(Vector2 basePosition)
		{
			BasePosition = basePosition;
			Tween = null;
			IsActive = true;
			return ++Generation;
		}

		public bool Complete(int generation)
		{
			if (!IsActive || generation != Generation)
			{
				return false;
			}
			Tween = null;
			IsActive = false;
			return true;
		}

		public void Cancel()
		{
			Tween?.Kill();
			Tween = null;
			IsActive = false;
			Generation++;
		}
	}

	private const float TimeEpsilonSeconds = 0.0005f;

	private const float DefaultReferenceHeight = 1080f;

	private static readonly ConditionalWeakTable<Node2D, AdditivePlaybackState> AdditivePlaybackStates = new ConditionalWeakTable<Node2D, AdditivePlaybackState>();

	public static int PlayAdditivePath(IReadOnlyList<Node2D?> anchors, IReadOnlyList<CombatCameraShakeTrack> tracks, Vector2 viewportSize, float referenceHeight = 1080f, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
	{
		float viewportScale = ((viewportSize.Y > 0f && referenceHeight > 0f) ? MathF.Max(1f, viewportSize.Y / referenceHeight) : 1f);
		List<Node2D> list = new List<Node2D>();
		int num = 0;
		foreach (Node2D anchor in anchors)
		{
			if (anchor != null && GodotObject.IsInstanceValid(anchor) && !list.Contains(anchor))
			{
				list.Add(anchor);
				if (PlayAdditivePath(anchor, anchor.Position, tracks, viewportScale, transition, ease))
				{
					num++;
				}
			}
		}
		return num;
	}

	public static bool PlayAdditivePath(Node2D? anchor, Vector2 basePosition, IReadOnlyList<CombatCameraShakeTrack> tracks, float viewportScale = 1f, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
	{
		if (anchor == null || !GodotObject.IsInstanceValid(anchor))
		{
			return false;
		}
		IReadOnlyList<CombatCameraShakeKey> readOnlyList = BuildAdditivePath(tracks);
		if (readOnlyList.Count == 0)
		{
			return false;
		}
		AdditivePlaybackState state = AdditivePlaybackStates.GetOrCreateValue(anchor);
		if (state.IsActive)
		{
			state.Tween?.Kill();
			anchor.Position = state.BasePosition;
			basePosition = state.BasePosition;
		}
		int generation = state.Begin(basePosition);
		Tween tween = PlayPathCore(anchor, basePosition, readOnlyList, viewportScale, transition, ease);
		if (tween == null)
		{
			state.Complete(generation);
			AdditivePlaybackStates.Remove(anchor);
			return true;
		}
		state.Tween = tween;
		tween.TweenCallback(Callable.From(() =>
		{
			CompleteAdditivePlayback(anchor, state, generation);
		}));
		return true;
	}

	public static bool CancelAdditivePath(Node2D? anchor)
	{
		if (anchor == null || !GodotObject.IsInstanceValid(anchor) || !AdditivePlaybackStates.TryGetValue(anchor, out AdditivePlaybackState value) || !value.IsActive)
		{
			return false;
		}
		value.Cancel();
		AdditivePlaybackStates.Remove(anchor);
		return true;
	}

	public static bool PlayPath(Node2D? anchor, Vector2 basePosition, IReadOnlyList<CombatCameraShakeKey> keys, float viewportScale = 1f, Tween.TransitionType transition = Tween.TransitionType.Cubic, Tween.EaseType ease = Tween.EaseType.InOut)
	{
		if (anchor == null || !GodotObject.IsInstanceValid(anchor) || keys.Count == 0)
		{
			return false;
		}
		PlayPathCore(anchor, basePosition, keys, viewportScale, transition, ease);
		return true;
	}

	private static Tween? PlayPathCore(Node2D anchor, Vector2 basePosition, IReadOnlyList<CombatCameraShakeKey> keys, float viewportScale, Tween.TransitionType transition, Tween.EaseType ease)
	{
		float num = MathF.Max(0f, viewportScale);
		anchor.Position = basePosition + keys[0].Offset * num;
		if (keys.Count == 1)
		{
			return null;
		}
		Tween tween = anchor.CreateTween();
		tween.SetTrans(transition);
		tween.SetEase(ease);
		for (int i = 1; i < keys.Count; i++)
		{
			float y = keys[i].TimeSeconds - keys[i - 1].TimeSeconds;
			Vector2 vector = basePosition + keys[i].Offset * num;
			tween.TweenProperty(anchor, "position", vector, MathF.Max(0.001f, y));
		}
		return tween;
	}

	private static void CompleteAdditivePlayback(Node2D anchor, AdditivePlaybackState state, int generation)
	{
		if (state.Complete(generation))
		{
			if (GodotObject.IsInstanceValid(anchor))
			{
				anchor.Position = state.BasePosition;
			}
			AdditivePlaybackStates.Remove(anchor);
		}
	}

	private static IReadOnlyList<CombatCameraShakeKey> BuildAdditivePath(IReadOnlyList<CombatCameraShakeTrack> tracks)
	{
		if (tracks.Count == 0)
		{
			return Array.Empty<CombatCameraShakeKey>();
		}
		List<float> list = new List<float>();
		foreach (CombatCameraShakeTrack track in tracks)
		{
			if (track.Keys == null || track.Keys.Count == 0)
			{
				continue;
			}
			foreach (CombatCameraShakeKey key in track.Keys)
			{
				AddUniqueTime(list, track.StartSeconds + key.TimeSeconds);
			}
		}
		if (list.Count == 0)
		{
			return Array.Empty<CombatCameraShakeKey>();
		}
		list.Sort();
		List<CombatCameraShakeKey> list2 = new List<CombatCameraShakeKey>(list.Count);
		foreach (float item in list)
		{
			Vector2 zero = Vector2.Zero;
			foreach (CombatCameraShakeTrack track2 in tracks)
			{
				zero += SampleTrack(track2, item);
			}
			list2.Add(new CombatCameraShakeKey(item, zero));
		}
		return list2;
	}

	private static void AddUniqueTime(List<float> times, float time)
	{
		for (int i = 0; i < times.Count; i++)
		{
			if (MathF.Abs(times[i] - time) <= 0.0005f)
			{
				return;
			}
		}
		times.Add(time);
	}

	private static Vector2 SampleTrack(CombatCameraShakeTrack track, float absoluteTimeSeconds)
	{
		IReadOnlyList<CombatCameraShakeKey> keys = track.Keys;
		if (keys == null || keys.Count == 0)
		{
			return Vector2.Zero;
		}
		float num = absoluteTimeSeconds - track.StartSeconds;
		if (num < keys[0].TimeSeconds - 0.0005f || num > keys[keys.Count - 1].TimeSeconds + 0.0005f)
		{
			return Vector2.Zero;
		}
		if (num <= keys[0].TimeSeconds + 0.0005f)
		{
			return keys[0].Offset;
		}
		for (int i = 1; i < keys.Count; i++)
		{
			CombatCameraShakeKey combatCameraShakeKey = keys[i - 1];
			CombatCameraShakeKey combatCameraShakeKey2 = keys[i];
			if (num <= combatCameraShakeKey2.TimeSeconds + 0.0005f)
			{
				float num2 = combatCameraShakeKey2.TimeSeconds - combatCameraShakeKey.TimeSeconds;
				if (num2 <= 0.0005f)
				{
					return combatCameraShakeKey2.Offset;
				}
				float weight = Math.Clamp((num - combatCameraShakeKey.TimeSeconds) / num2, 0f, 1f);
				return combatCameraShakeKey.Offset.Lerp(combatCameraShakeKey2.Offset, weight);
			}
		}
		return keys[keys.Count - 1].Offset;
	}
}
