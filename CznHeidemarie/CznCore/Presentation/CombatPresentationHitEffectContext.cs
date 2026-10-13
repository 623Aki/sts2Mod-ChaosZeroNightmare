namespace CznCore.Presentation;

internal sealed record CombatPresentationHitEffectContext(CombatPresentationCardContext Card, int HitIndex, int HitCount, decimal Damage, bool IsFinalHit)
{
	public CombatPresentationCardRoute Route => Card.Route;

	public string? CardId => Card.CardId;

	public CombatPresentationTargetScope TargetScope => Card.TargetScope;
}
