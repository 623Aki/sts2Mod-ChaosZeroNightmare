using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal readonly record struct CombatStartingEvent(IRunState RunState, ICombatState? CombatState);
