using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace CznCore.Presentation;

internal readonly record struct PlayerTurnStartedEvent(ICombatState CombatState, PlayerChoiceContext ChoiceContext, Player Player);
