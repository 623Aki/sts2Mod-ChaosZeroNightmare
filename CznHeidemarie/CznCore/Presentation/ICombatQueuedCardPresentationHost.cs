namespace CznCore.Presentation;

internal interface ICombatQueuedCardPresentationHost<TCard> where TCard : class
{
	bool ShouldHideNativePlayedCard(TCard? card);

	bool ShouldPlayCardPresentation(TCard? card);

	void PlayCardPresentation(TCard card);
}
