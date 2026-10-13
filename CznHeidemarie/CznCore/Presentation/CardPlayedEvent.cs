using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace CznCore.Presentation;

internal readonly record struct CardPlayedEvent(ICombatState CombatState, PlayerChoiceContext ChoiceContext, CardPlay CardPlay);
