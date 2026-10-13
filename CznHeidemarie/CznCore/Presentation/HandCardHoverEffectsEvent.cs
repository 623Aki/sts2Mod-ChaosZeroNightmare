using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace CznCore.Presentation;

internal readonly record struct HandCardHoverEffectsEvent(NHandCardHolder Holder, bool IsHovered);
