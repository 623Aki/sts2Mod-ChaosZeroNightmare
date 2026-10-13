using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatMeleeFormation
{
	public static Vector2 ResolveApproachDelta(NCreature ownerFormationAnchor, NCreature targetFormationAnchor, float horizontalTravel)
	{
		float y = targetFormationAnchor.GlobalPosition.Y - ownerFormationAnchor.GlobalPosition.Y;
		return new Vector2(horizontalTravel, y);
	}
}
