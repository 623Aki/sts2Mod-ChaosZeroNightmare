using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CznCore.Presentation;

internal readonly record struct CharacterSelectedEvent(NCharacterSelectScreen Screen, NCharacterSelectButton Button, CharacterModel Character);
