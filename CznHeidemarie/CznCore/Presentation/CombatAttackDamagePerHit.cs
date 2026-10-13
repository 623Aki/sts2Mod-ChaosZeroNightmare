using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal readonly record struct CombatAttackDamagePerHit(decimal Amount, IReadOnlyList<AbstractModel> Modifiers);
