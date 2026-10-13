using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal readonly record struct PlayerDeathResolvedEvent(Player Player, bool IsGameOver, float DeathAnimationLength);
