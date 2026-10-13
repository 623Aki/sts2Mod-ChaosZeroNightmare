using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatNodeAnchorCache<TOwner> where TOwner : class
{
	private sealed class CachedAnchor
	{
		public int Epoch { get; }

		public Node2D Anchor { get; }

		public CachedAnchor(int epoch, Node2D anchor)
		{
			Epoch = epoch;
			Anchor = anchor;
		}
	}

	private readonly ConditionalWeakTable<TOwner, CachedAnchor> _anchors = new ConditionalWeakTable<TOwner, CachedAnchor>();

	private int _epoch;

	public Node2D? GetOrResolve(TOwner owner, Func<TOwner, Node?> getRoot, Func<TOwner, Node2D?> resolve)
	{
		Node node = getRoot(owner);
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			_anchors.Remove(owner);
			return null;
		}
		int num = Volatile.Read(in _epoch);
		if (_anchors.TryGetValue(owner, out CachedAnchor value) && value.Epoch == num && IsAnchorValid(value.Anchor, node))
		{
			return value.Anchor;
		}
		_anchors.Remove(owner);
		Node2D node2D = resolve(owner);
		if (node2D == null || !IsAnchorValid(node2D, node))
		{
			return null;
		}
		_anchors.Add(owner, new CachedAnchor(num, node2D));
		return node2D;
	}

	public void Clear()
	{
		Interlocked.Increment(ref _epoch);
	}

	private static bool IsAnchorValid(Node2D anchor, Node root)
	{
		if (!GodotObject.IsInstanceValid(anchor))
		{
			return false;
		}
		try
		{
			for (Node node = anchor; node != null; node = node.GetParent())
			{
				if (node == root)
				{
					return true;
				}
			}
		}
		catch
		{
			return false;
		}
		return false;
	}
}
