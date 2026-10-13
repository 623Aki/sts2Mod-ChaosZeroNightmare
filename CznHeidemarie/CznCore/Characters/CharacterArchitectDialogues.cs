using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace CznCore.Characters;

internal static class CharacterArchitectDialogues
{
	[HarmonyPatch(typeof(TheArchitect), "DefineDialogues")]
	private static class DialoguePatch
	{
		private static void Postfix(AncientDialogueSet __result)
		{
			foreach (var (key, source) in Definitions)
			{
				__result.CharacterDialogues.TryAdd(key, source.Select((ArchitectDialogueDefinition d) => d.Create()).ToArray());
			}
		}
	}

	private static readonly Dictionary<string, IReadOnlyList<ArchitectDialogueDefinition>> Definitions = new Dictionary<string, IReadOnlyList<ArchitectDialogueDefinition>>();

	private static bool _patched;

	internal static void Register<TCharacter>(Harmony harmony, IReadOnlyList<ArchitectDialogueDefinition> dialogues) where TCharacter : CharacterModel
	{
		Definitions.Add(ModelDb.GetId(typeof(TCharacter)).Entry, dialogues);
		if (!_patched)
		{
			harmony.CreateClassProcessor(typeof(DialoguePatch)).Patch();
			_patched = true;
		}
	}
}
