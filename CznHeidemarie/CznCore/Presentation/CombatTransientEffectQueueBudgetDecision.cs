namespace CznCore.Presentation;

internal readonly record struct CombatTransientEffectQueueBudgetDecision(bool CanQueue, int ActiveBudget, bool HighPriority, string LodLabel);
