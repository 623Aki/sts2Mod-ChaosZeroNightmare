using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal readonly record struct CardPlayEnqueuingEvent(CardModel Card, Creature? Target);
