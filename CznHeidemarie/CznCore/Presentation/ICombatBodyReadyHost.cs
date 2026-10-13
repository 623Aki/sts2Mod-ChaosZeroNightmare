namespace CznCore.Presentation;

internal interface ICombatBodyReadyHost<TActor, TCard> where TActor : class where TCard : class
{
	bool IsTargetCard(TCard? card);

	TActor? ResolveActor(TCard card);

	bool CanShow(TActor actor);

	void Warn(string message, int priority);
}
