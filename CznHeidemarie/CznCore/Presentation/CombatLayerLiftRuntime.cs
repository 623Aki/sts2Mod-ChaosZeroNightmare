using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace CznCore.Presentation;

internal static class CombatLayerLiftRuntime
{
	private static readonly System.Collections.Generic.Dictionary<string, HashSet<Node2D>> OwnerSpines = new System.Collections.Generic.Dictionary<string, HashSet<Node2D>>(StringComparer.Ordinal);

	private const string SharedStateMeta = "czn_core_combat_layer_lift_shared_state";

	private const string OriginalZIndexKey = "original_z_index";

	private const string OriginalZAsRelativeKey = "original_z_as_relative";

	private const string LeaseKeyPrefix = "lease:";

	public static void Acquire(Node2D? spine, string ownerKey, int zIndex)
	{
		ArgumentException.ThrowIfNullOrEmpty(ownerKey, "ownerKey");
		if (spine != null && GodotObject.IsInstanceValid(spine))
		{
			Dictionary orCreateState = GetOrCreateState(spine);
			orCreateState[LeaseKey(ownerKey)] = CombatPresentationGodotLayeringRuntime.ClampCanvasItemZIndex(zIndex);
			spine.SetMeta("czn_core_combat_layer_lift_shared_state", orCreateState);
			Track(ownerKey, spine);
			Apply(spine, orCreateState);
		}
	}

	public static bool Release(Node2D? spine, string ownerKey)
	{
		ArgumentException.ThrowIfNullOrEmpty(ownerKey, "ownerKey");
		if (spine == null)
		{
			return false;
		}
		Untrack(ownerKey, spine);
		if (!GodotObject.IsInstanceValid(spine) || !TryGetState(spine, out Dictionary state))
		{
			return false;
		}
		string text = LeaseKey(ownerKey);
		if (!state.ContainsKey(text))
		{
			return false;
		}
		state.Remove(text);
		if (TryResolveHighestLeaseZIndex(state, out var highestZIndex))
		{
			spine.SetMeta("czn_core_combat_layer_lift_shared_state", state);
			Apply(spine, highestZIndex);
			return true;
		}
		try
		{
			spine.ZIndex = state["original_z_index"].AsInt32();
			spine.ZAsRelative = state["original_z_as_relative"].AsBool();
		}
		finally
		{
			spine.RemoveMeta("czn_core_combat_layer_lift_shared_state");
		}
		return true;
	}

	public static int ReleaseAll(string ownerKey)
	{
		ArgumentException.ThrowIfNullOrEmpty(ownerKey, "ownerKey");
		if (!OwnerSpines.TryGetValue(ownerKey, out HashSet<Node2D> value))
		{
			return 0;
		}
		Node2D[] array = new Node2D[value.Count];
		value.CopyTo(array);
		int num = 0;
		Node2D[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if (Release(array2[i], ownerKey))
			{
				num++;
			}
		}
		OwnerSpines.Remove(ownerKey);
		return num;
	}

	public static int ActiveCount(string ownerKey)
	{
		ArgumentException.ThrowIfNullOrEmpty(ownerKey, "ownerKey");
		if (!OwnerSpines.TryGetValue(ownerKey, out HashSet<Node2D> value))
		{
			return 0;
		}
		return value.Count;
	}

	private static Dictionary GetOrCreateState(Node2D surface)
	{
		if (TryGetState(surface, out Dictionary state))
		{
			return state;
		}
		return new Dictionary
		{
			["original_z_index"] = surface.ZIndex,
			["original_z_as_relative"] = surface.ZAsRelative
		};
	}

	private static bool TryGetState(Node2D surface, out Dictionary state)
	{
		state = new Dictionary();
		try
		{
			if (!surface.HasMeta("czn_core_combat_layer_lift_shared_state"))
			{
				return false;
			}
			state = surface.GetMeta("czn_core_combat_layer_lift_shared_state").AsGodotDictionary();
			return state.ContainsKey("original_z_index") && state.ContainsKey("original_z_as_relative");
		}
		catch
		{
			return false;
		}
	}

	private static void Apply(Node2D surface, Dictionary state)
	{
		if (TryResolveHighestLeaseZIndex(state, out var highestZIndex))
		{
			Apply(surface, highestZIndex);
		}
	}

	private static void Apply(Node2D surface, int zIndex)
	{
		if (GodotObject.IsInstanceValid(surface))
		{
			surface.ZAsRelative = false;
			surface.ZIndex = zIndex;
		}
	}

	private static string LeaseKey(string ownerKey)
	{
		return "lease:" + ownerKey;
	}

	private static bool TryResolveHighestLeaseZIndex(Dictionary state, out int highestZIndex)
	{
		highestZIndex = int.MinValue;
		foreach (Variant key in state.Keys)
		{
			if (key.AsString().StartsWith("lease:", StringComparison.Ordinal))
			{
				Variant variant = state[key];
				int val = ((variant.VariantType == Variant.Type.Int) ? variant.AsInt32() : (-6));
				highestZIndex = Math.Max(highestZIndex, val);
			}
		}
		return highestZIndex != int.MinValue;
	}

	private static void Track(string ownerKey, Node2D spine)
	{
		if (!OwnerSpines.TryGetValue(ownerKey, out HashSet<Node2D> value))
		{
			value = new HashSet<Node2D>();
			OwnerSpines[ownerKey] = value;
		}
		value.Add(spine);
	}

	private static void Untrack(string ownerKey, Node2D spine)
	{
		if (OwnerSpines.TryGetValue(ownerKey, out HashSet<Node2D> value) && value.Remove(spine) && value.Count == 0)
		{
			OwnerSpines.Remove(ownerKey);
		}
	}
}
