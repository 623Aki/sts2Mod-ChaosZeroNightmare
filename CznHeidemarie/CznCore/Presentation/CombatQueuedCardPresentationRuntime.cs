using System;

namespace CznCore.Presentation;

internal sealed class CombatQueuedCardPresentationRuntime<TCard> where TCard : class
{
	private readonly NativePlayedCardVisibilityRuntime<TCard> _nativePlayedCards;

	private readonly Func<TCard?, bool> _shouldHideNativePlayedCard;

	private readonly Func<TCard?, bool> _shouldPlayCardPresentation;

	private readonly Action<TCard> _playCardPresentation;

	public CombatQueuedCardPresentationRuntime(NativePlayedCardVisibilityRuntime<TCard> nativePlayedCards, ICombatQueuedCardPresentationHost<TCard> host)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_nativePlayedCards = nativePlayedCards ?? throw new ArgumentNullException("nativePlayedCards");
		_shouldHideNativePlayedCard = host.ShouldHideNativePlayedCard;
		_shouldPlayCardPresentation = host.ShouldPlayCardPresentation;
		_playCardPresentation = host.PlayCardPresentation;
	}

	public void Queue(TCard? card)
	{
		if (card != null)
		{
			if (_shouldHideNativePlayedCard(card))
			{
				_nativePlayedCards.Hide(card);
			}
			if (_shouldPlayCardPresentation(card))
			{
				_playCardPresentation(card);
			}
		}
	}
}
