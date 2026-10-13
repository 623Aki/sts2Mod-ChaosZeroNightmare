using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace CznCore.Presentation;

internal readonly record struct CardPlayingEvent(ICombatState CombatState, CardPlay CardPlay);
