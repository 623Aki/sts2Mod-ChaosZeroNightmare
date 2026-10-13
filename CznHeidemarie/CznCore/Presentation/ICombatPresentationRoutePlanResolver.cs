namespace CznCore.Presentation;

internal interface ICombatPresentationRoutePlanResolver<in TContext>
{
	bool TryGetRoutePlan(TContext context, out CombatPresentationRoutePlan plan);
}
