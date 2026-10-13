using System.Linq;
using MegaCrit.Sts2.Core.Entities.Ancients;

namespace CznCore.Characters;

internal readonly record struct ArchitectDialogueDefinition(int LineCount, int? VisitIndex, bool IsRepeating = true, ArchitectAttackers StartAttackers = ArchitectAttackers.None, ArchitectAttackers EndAttackers = ArchitectAttackers.Both)
{
	internal AncientDialogue Create()
	{
		return new AncientDialogue(Enumerable.Repeat(string.Empty, LineCount).ToArray())
		{
			VisitIndex = VisitIndex,
			IsRepeating = IsRepeating,
			StartAttackers = StartAttackers,
			EndAttackers = EndAttackers
		};
	}
}
