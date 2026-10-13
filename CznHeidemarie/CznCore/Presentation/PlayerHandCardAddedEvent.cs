using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal readonly record struct PlayerHandCardAddedEvent(NPlayerHand Hand, NCard? Card, NHandCardHolder? Holder);
