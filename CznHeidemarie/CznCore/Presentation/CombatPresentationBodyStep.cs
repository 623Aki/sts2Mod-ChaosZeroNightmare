namespace CznCore.Presentation;

internal sealed record CombatPresentationBodyStep(string BodyAnimation, string? CueAnimation = null, float? DelaySeconds = null)
{
	public string EffectiveCueAnimation
	{
		get
		{
			if (!string.IsNullOrEmpty(CueAnimation))
			{
				return CueAnimation;
			}
			return BodyAnimation;
		}
	}
}
