using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal readonly record struct RestSiteHealedEvent(Player Player, bool IsMimicked);
