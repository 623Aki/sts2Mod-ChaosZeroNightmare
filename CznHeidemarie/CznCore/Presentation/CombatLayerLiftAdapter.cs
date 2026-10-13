using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatLayerLiftAdapter<TTarget> where TTarget : class
{
	private readonly Func<TTarget?, Node2D?> _findSpine;

	private readonly Lazy<CombatLayerLiftGroupRuntime> _group;

	public bool HasActiveLeases
	{
		get
		{
			if (_group.IsValueCreated)
			{
				return _group.Value.ActiveCount > 0;
			}
			return false;
		}
	}

	public CombatLayerLiftAdapter(string? ownerKey, string? label, int zIndex, float durationSeconds, string? logPrefix, Func<TTarget?, Node2D?> findSpine, Action<string, int> warn, Action<Func<string>, int>? debug = null)
	{
		_findSpine = findSpine ?? throw new ArgumentNullException("findSpine");
		_group = new Lazy<CombatLayerLiftGroupRuntime>(() => new CombatLayerLiftGroupRuntime(ownerKey, label, zIndex, durationSeconds, logPrefix, warn, debug));
	}

	public void Begin(IReadOnlyList<TTarget?>? targets, int token)
	{
		_group.Value.Begin(ResolveSpines(targets), token);
	}

	public void Begin(TTarget? first, IReadOnlyList<TTarget>? rest, int token)
	{
		_group.Value.Begin(ResolveSpines(first, rest), token);
	}

	public void Ensure(TTarget? target, int token)
	{
		if (target != null)
		{
			Node2D node2D = _findSpine(target);
			if (node2D != null && GodotObject.IsInstanceValid(node2D))
			{
				_group.Value.Ensure(new _003C_003Ez__ReadOnlySingleElementList<Node2D>(node2D), token);
			}
		}
	}

	public void Cleanup()
	{
		if (_group.IsValueCreated)
		{
			_group.Value.Cleanup();
		}
	}

	private IReadOnlyList<Node2D?>? ResolveSpines(IReadOnlyList<TTarget?>? targets)
	{
		if (targets == null || targets.Count == 0)
		{
			return null;
		}
		List<Node2D> list = new List<Node2D>(targets.Count);
		foreach (TTarget target in targets)
		{
			list.Add((target == null) ? null : _findSpine(target));
		}
		return list;
	}

	private IReadOnlyList<Node2D?>? ResolveSpines(TTarget? first, IReadOnlyList<TTarget>? rest)
	{
		int num = ((first != null) ? 1 : 0) + (rest?.Count ?? 0);
		if (num == 0)
		{
			return null;
		}
		List<Node2D> list = new List<Node2D>(num);
		if (first != null)
		{
			list.Add(_findSpine(first));
		}
		if (rest != null)
		{
			foreach (TTarget item in rest)
			{
				list.Add((item == null) ? null : _findSpine(item));
			}
		}
		return list;
	}
}
