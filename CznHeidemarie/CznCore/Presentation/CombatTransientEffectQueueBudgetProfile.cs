namespace CznCore.Presentation;

internal readonly record struct CombatTransientEffectQueueBudgetProfile(int SoftBudget, int HardBudget, int OptionalBudgetReserve = 2, bool TreatScreenTargetBackAsHighPriority = false);
