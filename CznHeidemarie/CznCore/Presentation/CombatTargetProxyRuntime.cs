using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace CznCore.Presentation;

internal sealed partial class CombatTargetProxyRuntime : Node2D
{
	internal const string HitRequestedSignal = "czn_target_proxy_hit_requested";

	internal const string TerminalHitRequestedSignal = "czn_target_proxy_terminal_hit_requested";

	internal const string HitFreezeRequestedSignal = "czn_target_proxy_hit_freeze_requested";

	private static readonly string[] IdleFallbackAnimations = new string[10] { "idle_loop", "idle", "awake_loop", "idle_normal", "normal_idle", "neutral", "normal", "standby", "stand", "wait" };

	private static readonly string[] HitFallbackAnimations = new string[3] { "hurt", "hit", "normal_hit" };

	private readonly List<GodotObject> _activeHitEntries = new List<GodotObject>();

	internal void BindRouteCommandSignals()
	{
		BindCommandSignal("czn_target_proxy_hit_requested", Callable.From(HandleHitRequested));
		BindCommandSignal("czn_target_proxy_terminal_hit_requested", Callable.From(HandleTerminalHitRequested));
		BindCommandSignal("czn_target_proxy_hit_freeze_requested", Callable.From(HandleHitFreezeRequested));
	}

	public override void _Ready()
	{
		RestoreMirroredState();
	}

	private bool RestoreMirroredState()
	{
		bool restored = false;
		VisitMirroredSpines((Node spine) =>
		{
			restored |= TryRestoreSpine(spine);
		});
		return restored;
	}

	private int PlayMirroredHit(bool restoreState)
	{
		_activeHitEntries.Clear();
		int played = 0;
		VisitMirroredSpines((Node spine) =>
		{
			if (TryPlayHit(spine, restoreState, out GodotObject hitEntry))
			{
				played++;
				if (hitEntry != null)
				{
					_activeHitEntries.Add(hitEntry);
				}
			}
		});
		return played;
	}

	private int FreezeMirroredHit()
	{
		int num = 0;
		foreach (GodotObject activeHitEntry in _activeHitEntries)
		{
			if (GodotObject.IsInstanceValid(activeHitEntry) && activeHitEntry.HasMethod("set_time_scale"))
			{
				activeHitEntry.Call("set_time_scale", 0f);
				num++;
			}
		}
		return num;
	}

	private void BindCommandSignal(StringName signalName, Callable handler)
	{
		AddUserSignal(signalName);
		Error error = Connect(signalName, handler);
		if (error != Error.Ok)
		{
			throw new InvalidOperationException(string.Concat(new string[5]
			{
				"Target proxy command signal bind failed: ",
				signalName,
				" (",
				error.ToString(),
				")"
			}));
		}
	}

	private void HandleHitRequested()
	{
		PlayMirroredHit(restoreState: true);
	}

	private void HandleTerminalHitRequested()
	{
		PlayMirroredHit(restoreState: false);
	}

	private void HandleHitFreezeRequested()
	{
		FreezeMirroredHit();
	}

	private void VisitMirroredSpines(Action<Node> visit)
	{
		VisitMirroredSpines(this, visit);
	}

	private static void VisitMirroredSpines(Node root, Action<Node> visit)
	{
		if (root.HasMeta("czn_spine_state") && root.HasMethod("get_animation_state") && root.HasMethod("get_skeleton"))
		{
			visit(root);
		}
		foreach (Node child in root.GetChildren())
		{
			VisitMirroredSpines(child, visit);
		}
	}

	private static bool TryRestoreSpine(Node spine)
	{
		try
		{
			if (!TryGetSpineContext(spine, out Dictionary visualState, out GodotObject animationState, out GodotObject skeleton, out GodotObject data))
			{
				return false;
			}
			ApplySkin(visualState, skeleton, data);
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			if (visualState.TryGetValue("tracks", out var value))
			{
				foreach (Variant item in value.AsGodotArray())
				{
					Dictionary dictionary = item.AsGodotDictionary();
					string text = GetString(dictionary, "anim");
					if (!string.IsNullOrEmpty(text) && HasAnimation(data, text))
					{
						if (!flag && animationState.HasMethod("clear_tracks"))
						{
							animationState.Call("clear_tracks");
							flag = true;
						}
						int num = GetInt(dictionary, "track");
						bool flag4 = GetBool(dictionary, "loop", defaultValue: true);
						GodotObject godotObject = animationState.Call("set_animation", text, flag4, num).AsGodotObject();
						if (godotObject != null && dictionary.TryGetValue("time", out var value2) && godotObject.HasMethod("set_track_time"))
						{
							godotObject.Call("set_track_time", Math.Max(0f, value2.AsSingle()));
						}
						flag2 = true;
						flag3 |= num == 0;
					}
				}
			}
			if (!flag3)
			{
				if (!flag && animationState.HasMethod("clear_tracks"))
				{
					animationState.Call("clear_tracks");
				}
				flag3 = TryPlayIdleFallback(animationState, data);
			}
			SetPlaybackTimeScale(animationState, 1f);
			ApplyImmediately(spine, animationState, skeleton);
			return flag2 | flag3;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Target proxy state restore failed: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static bool TryPlayHit(Node spine, bool restoreState, out GodotObject? hitEntry)
	{
		hitEntry = null;
		try
		{
			if (!TryGetSpineContext(spine, out Dictionary visualState, out GodotObject animationState, out GodotObject skeleton, out GodotObject data))
			{
				return false;
			}
			string mirroredAnimation = GetString(visualState, "anim");
			string text = ResolveHitAnimation(data, mirroredAnimation);
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			SetPlaybackTimeScale(animationState, 1f);
			hitEntry = animationState.Call("set_animation", text, false, 0).AsGodotObject();
			if (restoreState && animationState.HasMethod("add_animation"))
			{
				var (text2, flag) = ResolveRestoreAnimation(visualState, data, mirroredAnimation);
				if (!string.IsNullOrEmpty(text2))
				{
					animationState.Call("add_animation", text2, flag, 0, 0f);
				}
			}
			ApplyImmediately(spine, animationState, skeleton);
			return true;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Target proxy hit playback failed: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static bool TryGetSpineContext(Node spine, out Dictionary visualState, out GodotObject animationState, out GodotObject skeleton, out GodotObject data)
	{
		visualState = new Dictionary();
		animationState = null;
		skeleton = null;
		data = null;
		if (!spine.HasMeta("czn_spine_state"))
		{
			return false;
		}
		visualState = spine.GetMeta("czn_spine_state").AsGodotDictionary();
		animationState = spine.Call("get_animation_state").AsGodotObject();
		skeleton = spine.Call("get_skeleton").AsGodotObject();
		if (animationState == null || skeleton == null || !skeleton.HasMethod("get_data"))
		{
			return false;
		}
		data = skeleton.Call("get_data").AsGodotObject();
		return data != null;
	}

	private static void ApplySkin(Dictionary visualState, GodotObject skeleton, GodotObject data)
	{
		string text = GetString(visualState, "skin");
		if (!string.IsNullOrEmpty(text) && data.HasMethod("find_skin") && data.Call("find_skin", text).AsGodotObject() != null && skeleton.HasMethod("set_skin_by_name"))
		{
			skeleton.Call("set_skin_by_name", text);
			if (skeleton.HasMethod("set_slots_to_setup_pose"))
			{
				skeleton.Call("set_slots_to_setup_pose");
			}
		}
	}

	private static string ResolveHitAnimation(GodotObject data, string mirroredAnimation)
	{
		if (mirroredAnimation.Contains("sleep", StringComparison.OrdinalIgnoreCase) && HasAnimation(data, "hurt_sleeping"))
		{
			return "hurt_sleeping";
		}
		if (mirroredAnimation.Contains("stun", StringComparison.OrdinalIgnoreCase) && HasAnimation(data, "hurt_stunned"))
		{
			return "hurt_stunned";
		}
		if (mirroredAnimation.Contains("awake", StringComparison.OrdinalIgnoreCase) && HasAnimation(data, "hurt_awake"))
		{
			return "hurt_awake";
		}
		string[] hitFallbackAnimations = HitFallbackAnimations;
		foreach (string text in hitFallbackAnimations)
		{
			if (HasAnimation(data, text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	private static (string Animation, bool Loop) ResolveRestoreAnimation(Dictionary visualState, GodotObject data, string mirroredAnimation)
	{
		if (!string.IsNullOrEmpty(mirroredAnimation) && HasAnimation(data, mirroredAnimation))
		{
			return (Animation: mirroredAnimation, Loop: ResolveTrackZeroLoop(visualState));
		}
		return (Animation: ResolveIdleFallback(data), Loop: true);
	}

	private static bool ResolveTrackZeroLoop(Dictionary visualState)
	{
		if (!visualState.TryGetValue("tracks", out var value))
		{
			return true;
		}
		foreach (Variant item in value.AsGodotArray())
		{
			Dictionary dictionary = item.AsGodotDictionary();
			if (GetInt(dictionary, "track") == 0)
			{
				return GetBool(dictionary, "loop", defaultValue: true);
			}
		}
		return true;
	}

	private static bool TryPlayIdleFallback(GodotObject animationState, GodotObject data)
	{
		string text = ResolveIdleFallback(data);
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		animationState.Call("set_animation", text, true, 0);
		return true;
	}

	private static string ResolveIdleFallback(GodotObject data)
	{
		string[] idleFallbackAnimations = IdleFallbackAnimations;
		foreach (string text in idleFallbackAnimations)
		{
			if (HasAnimation(data, text))
			{
				return text;
			}
		}
		return string.Empty;
	}

	private static bool HasAnimation(GodotObject data, string animation)
	{
		if (data.HasMethod("find_animation"))
		{
			return data.Call("find_animation", animation).AsGodotObject() != null;
		}
		return false;
	}

	private static void SetPlaybackTimeScale(GodotObject animationState, float scale)
	{
		if (animationState.HasMethod("set_time_scale"))
		{
			animationState.Call("set_time_scale", scale);
		}
	}

	private static void ApplyImmediately(Node spine, GodotObject animationState, GodotObject skeleton)
	{
		if (animationState.HasMethod("apply"))
		{
			animationState.Call("apply", skeleton);
		}
		if (spine.HasMethod("update_skeleton"))
		{
			spine.Call("update_skeleton", 0f);
		}
	}

	private static string GetString(Dictionary dictionary, string key)
	{
		if (!dictionary.TryGetValue(key, out var value))
		{
			return string.Empty;
		}
		return value.AsString();
	}

	private static int GetInt(Dictionary dictionary, string key)
	{
		if (!dictionary.TryGetValue(key, out var value))
		{
			return 0;
		}
		return value.AsInt32();
	}

	private static bool GetBool(Dictionary dictionary, string key, bool defaultValue)
	{
		if (!dictionary.TryGetValue(key, out var value))
		{
			return defaultValue;
		}
		return value.AsBool();
	}
}
