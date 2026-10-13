namespace CznCore.Presentation;

internal interface ICombatPresentationRouteResolver<TCard> where TCard : class
{
	CombatPresentationCardRoute Resolve(TCard? card);
}
