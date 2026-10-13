namespace CznCore.Presentation;

internal interface INativePlayedCardVisibilityHost<TCard> where TCard : class
{
	bool IsCardEligible(TCard? card);

	void Warn(string message, int priority);
}
