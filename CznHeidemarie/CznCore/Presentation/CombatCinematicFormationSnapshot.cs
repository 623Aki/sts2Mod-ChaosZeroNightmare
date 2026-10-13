using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal readonly struct CombatCinematicFormationSnapshot
{
	private const float SideEpsilon = 0.5f;

	private readonly Vector2[] _targetViewportPositions;

	public bool HasActorAnchor { get; }

	public Vector2 ActorViewportPosition { get; }

	public IReadOnlyList<Vector2> TargetViewportPositions => _targetViewportPositions ?? Array.Empty<Vector2>();

	public int TargetCount => _targetViewportPositions?.Length ?? 0;

	public bool HasTargets => TargetCount > 0;

	public Vector2 TargetViewportCenter { get; }

	public Rect2 ViewportRect { get; }

	public CombatCinematicFormationSnapshot(bool hasActorAnchor, Vector2 actorViewportPosition, Vector2[] targetViewportPositions, Rect2 viewportRect)
	{
		HasActorAnchor = hasActorAnchor;
		ActorViewportPosition = actorViewportPosition;
		_targetViewportPositions = targetViewportPositions ?? Array.Empty<Vector2>();
		ViewportRect = viewportRect;
		Vector2 zero = Vector2.Zero;
		Vector2[] targetViewportPositions2 = _targetViewportPositions;
		foreach (Vector2 vector in targetViewportPositions2)
		{
			zero += vector;
		}
		TargetViewportCenter = ((_targetViewportPositions.Length == 0) ? Vector2.Zero : (zero / _targetViewportPositions.Length));
	}

	public Vector2 ResolveSingleTargetOnActorRight(out bool crossedActorSide)
	{
		crossedActorSide = false;
		if (!HasActorAnchor || TargetCount != 1)
		{
			return TargetViewportCenter;
		}
		Vector2 result = _targetViewportPositions[0];
		float num = result.X - ActorViewportPosition.X;
		if (num >= -0.5f)
		{
			return result;
		}
		float num2 = ActorViewportPosition.X - num;
		if (ViewportRect.Size.X > 0.5f)
		{
			float x = ViewportRect.Position.X;
			float max = x + ViewportRect.Size.X;
			num2 = Math.Clamp(num2, x, max);
		}
		if (num2 <= ActorViewportPosition.X + 0.5f)
		{
			return result;
		}
		crossedActorSide = true;
		return new Vector2(num2, result.Y);
	}
}
