using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznCore.Presentation;

internal readonly record struct DamageReceivedEvent(PlayerChoiceContext ChoiceContext, IRunState RunState, ICombatState? CombatState, Creature Target, DamageResult Result, ValueProp Props, Creature? Dealer, CardModel? CardSource);
