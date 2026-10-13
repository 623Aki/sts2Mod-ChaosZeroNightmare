using Godot;

namespace CznCore.Presentation;

internal readonly record struct CombatBodyEntryRunInStart(bool Started, bool UsesSegmentOffsets, Vector2 TargetPosition, int Generation)
{
	public static CombatBodyEntryRunInStart None { get; } = new CombatBodyEntryRunInStart(Started: false, UsesSegmentOffsets: false, Vector2.Zero, 0);
}
