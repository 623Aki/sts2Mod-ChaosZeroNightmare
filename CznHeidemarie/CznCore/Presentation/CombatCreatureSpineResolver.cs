using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatCreatureSpineResolver
{
	private const string SpineSpriteClassName = "SpineSprite";

	private readonly CombatNodeAnchorCache<NCreature> _anchors = new CombatNodeAnchorCache<NCreature>();

	private readonly FieldInfo? _visualsBodyField = typeof(NCreatureVisuals).GetField("_body", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private readonly PropertyInfo? _spineBodyProperty = typeof(NCreatureVisuals).GetProperty("SpineBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	public Node2D? Find(NCreature? creature)
	{
		if (creature == null || !GodotObject.IsInstanceValid(creature))
		{
			return null;
		}
		return _anchors.GetOrResolve(creature, GetSearchRoot, FindUncached);
	}

	public void Clear()
	{
		_anchors.Clear();
	}

	public bool TrySetVisualsSpineBody(NCreatureVisuals? visuals, Node2D spine, Action<string>? warn = null)
	{
		if (visuals == null || !GodotObject.IsInstanceValid(spine) || _spineBodyProperty == null)
		{
			return false;
		}
		try
		{
			_spineBodyProperty.SetValue(visuals, new MegaSprite(spine));
			return true;
		}
		catch (Exception ex)
		{
			warn?.Invoke("Failed to restore NCreatureVisuals.SpineBody: " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private static Node? GetSearchRoot(NCreature creature)
	{
		return (Node?)(((object)creature.Visuals) ?? ((object)creature));
	}

	private Node2D? FindUncached(NCreature creature)
	{
		Node searchRoot = GetSearchRoot(creature);
		if (searchRoot == null)
		{
			return null;
		}
		Node2D node2D = FindFromBody(SafeGetCreatureBody(creature));
		if (node2D != null)
		{
			return node2D;
		}
		NCreatureVisuals visuals = creature.Visuals;
		Node2D node2D2 = FindFromBody(SafeGetCurrentBody(visuals));
		if (node2D2 != null)
		{
			return node2D2;
		}
		Node2D node2D3 = FindFromBody(SafeGetBodyValue(_visualsBodyField, visuals));
		if (node2D3 != null)
		{
			return node2D3;
		}
		return FindRecursive(searchRoot);
	}

	private static Node2D? SafeGetCreatureBody(NCreature creature)
	{
		try
		{
			return creature.Body;
		}
		catch
		{
			return null;
		}
	}

	private static Node2D? SafeGetCurrentBody(NCreatureVisuals? visuals)
	{
		if (visuals == null)
		{
			return null;
		}
		try
		{
			return visuals.GetCurrentBody();
		}
		catch
		{
			return null;
		}
	}

	private static Node2D? SafeGetBodyValue(FieldInfo? field, NCreatureVisuals? visuals)
	{
		if (field == null || visuals == null)
		{
			return null;
		}
		try
		{
			return field.GetValue(visuals) as Node2D;
		}
		catch
		{
			return null;
		}
	}

	private static Node2D? FindFromBody(Node2D? body)
	{
		if (body == null || !GodotObject.IsInstanceValid(body))
		{
			return null;
		}
		if (IsSpineSprite(body))
		{
			return body;
		}
		return FindRecursive(body);
	}

	private static Node2D? FindRecursive(Node root)
	{
		if (root is Node2D result && IsSpineSprite(root))
		{
			return result;
		}
		foreach (Node child in root.GetChildren())
		{
			Node2D node2D = FindRecursive(child);
			if (node2D != null)
			{
				return node2D;
			}
		}
		return null;
	}

	private static bool IsSpineSprite(GodotObject node)
	{
		return node.GetClass() == "SpineSprite";
	}
}
