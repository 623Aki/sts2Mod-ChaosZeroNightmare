using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CznCore.Presentation;

internal readonly record struct RandomCharacterResolvedEvent(NCharacterSelectScreen Screen, CharacterModel Character);
