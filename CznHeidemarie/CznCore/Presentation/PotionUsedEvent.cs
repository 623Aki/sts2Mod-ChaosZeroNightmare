using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal readonly record struct PotionUsedEvent(IRunState RunState, ICombatState? CombatState, PotionModel Potion, Creature? Target);
