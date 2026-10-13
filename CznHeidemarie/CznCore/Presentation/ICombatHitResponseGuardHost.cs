namespace CznCore.Presentation;

internal interface ICombatHitResponseGuardHost<TActor> where TActor : class
{
	void BeginHitResponseGuard(TActor? actor, int token, CombatPresentationHitResponsePolicy hitResponsePolicy, string? reason);

	void ScheduleHitResponseGuardClear(TActor? actor, int token, float delaySeconds);
}
