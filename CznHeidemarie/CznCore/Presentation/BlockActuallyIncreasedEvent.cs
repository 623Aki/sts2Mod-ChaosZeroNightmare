using MegaCrit.Sts2.Core.Entities.Creatures;

namespace CznCore.Presentation;

internal readonly record struct BlockActuallyIncreasedEvent(Creature Receiver, int PreviousBlock, int CurrentBlock);
