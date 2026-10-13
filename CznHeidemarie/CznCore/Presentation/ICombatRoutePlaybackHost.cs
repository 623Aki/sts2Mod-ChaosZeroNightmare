namespace CznCore.Presentation;

internal interface ICombatRoutePlaybackHost<TActor> where TActor : class
{
	int GetAnimationToken(TActor? actor);

	void BeginNativeBodyTriggerSuppression(TActor? actor, CombatPresentationRoutePlan plan, int token);

	void ClearNativeBodyTriggerSuppression(TActor? actor, int token);

	void Warn(string message, int priority);
}
