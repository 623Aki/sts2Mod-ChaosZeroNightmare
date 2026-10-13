using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal readonly struct CombatTargetAnchorSnapshot(NCreature node, Node2D? spine, Vector2 vfxSpawnGlobal, Vector2 hitboxGlobalPosition, Vector2 hitboxSize, Vector2 hitboxTopGlobal, Vector2 hitboxBottomGlobal, Vector2 boundsSize)
{
	public NCreature Node { get; } = node;

	public Node2D? Spine { get; } = spine;

	public Vector2 VfxSpawnGlobal { get; } = vfxSpawnGlobal;

	public Vector2 HitboxGlobalPosition { get; } = hitboxGlobalPosition;

	public Vector2 HitboxSize { get; } = hitboxSize;

	public Vector2 HitboxTopGlobal { get; } = hitboxTopGlobal;

	public Vector2 HitboxBottomGlobal { get; } = hitboxBottomGlobal;

	public Vector2 BoundsSize { get; } = boundsSize;

	public Vector2 HitboxCenterGlobal => HitboxGlobalPosition + HitboxSize * 0.5f;
}
