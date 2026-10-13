using System;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatPlayedCardPresentationRuntimes
{
	private sealed class NativePlayedCardVisibilityHost : INativePlayedCardVisibilityHost<CardModel>
	{
		private readonly Func<CardModel?, bool> _isCardEligible;

		private readonly Action<string, int> _warn;

		public NativePlayedCardVisibilityHost(Func<CardModel?, bool> isCardEligible, Action<string, int> warn)
		{
			_isCardEligible = isCardEligible ?? throw new ArgumentNullException("isCardEligible");
			_warn = warn ?? throw new ArgumentNullException("warn");
		}

		public bool IsCardEligible(CardModel? card)
		{
			return _isCardEligible(card);
		}

		public void Warn(string message, int priority)
		{
			_warn(message, priority);
		}
	}

	private sealed class QueuedCardPresentationHost : ICombatQueuedCardPresentationHost<CardModel>
	{
		private readonly Func<CardModel?, bool> _isCardEligible;

		private readonly Func<CardModel?, bool> _isCardPlayPresentationEnabled;

		private readonly Func<CardModel?, bool> _shouldHideNativePlayedCard;

		private readonly Func<CardModel?, bool> _usesNativeCardPlayPresentation;

		private readonly Action<CardModel> _playCardPresentation;

		public QueuedCardPresentationHost(Func<CardModel?, bool> isCardEligible, Func<CardModel?, bool> isCardPlayPresentationEnabled, Func<CardModel?, bool> usesNativeCardPlayPresentation, Action<CardModel> playCardPresentation, Func<CardModel?, bool>? shouldHideNativePlayedCard)
		{
			_isCardEligible = isCardEligible ?? throw new ArgumentNullException("isCardEligible");
			_isCardPlayPresentationEnabled = isCardPlayPresentationEnabled ?? throw new ArgumentNullException("isCardPlayPresentationEnabled");
			_usesNativeCardPlayPresentation = usesNativeCardPlayPresentation ?? throw new ArgumentNullException("usesNativeCardPlayPresentation");
			_playCardPresentation = playCardPresentation ?? throw new ArgumentNullException("playCardPresentation");
			_shouldHideNativePlayedCard = shouldHideNativePlayedCard ?? ((Func<CardModel, bool>)((CardModel? _) => true));
		}

		public bool ShouldHideNativePlayedCard(CardModel? card)
		{
			if (_isCardPlayPresentationEnabled(card) && _isCardEligible(card))
			{
				return _shouldHideNativePlayedCard(card);
			}
			return false;
		}

		public bool ShouldPlayCardPresentation(CardModel? card)
		{
			if (_isCardPlayPresentationEnabled(card) && _isCardEligible(card))
			{
				return !_usesNativeCardPlayPresentation(card);
			}
			return false;
		}

		public void PlayCardPresentation(CardModel card)
		{
			_playCardPresentation(card);
		}
	}

	private sealed class QueuedNativeCardHideOnlyHost : ICombatQueuedCardPresentationHost<CardModel>
	{
		private readonly Func<CardModel?, bool> _isCardEligible;

		private readonly Func<CardModel?, bool> _isCardPlayPresentationEnabled;

		private readonly Func<CardModel?, bool> _shouldHideNativePlayedCard;

		public QueuedNativeCardHideOnlyHost(Func<CardModel?, bool> isCardEligible, Func<CardModel?, bool> isCardPlayPresentationEnabled, Func<CardModel?, bool>? shouldHideNativePlayedCard)
		{
			_isCardEligible = isCardEligible ?? throw new ArgumentNullException("isCardEligible");
			_isCardPlayPresentationEnabled = isCardPlayPresentationEnabled ?? throw new ArgumentNullException("isCardPlayPresentationEnabled");
			_shouldHideNativePlayedCard = shouldHideNativePlayedCard ?? ((Func<CardModel, bool>)((CardModel? _) => true));
		}

		public bool ShouldHideNativePlayedCard(CardModel? card)
		{
			if (_isCardPlayPresentationEnabled(card) && _isCardEligible(card))
			{
				return _shouldHideNativePlayedCard(card);
			}
			return false;
		}

		public bool ShouldPlayCardPresentation(CardModel? card)
		{
			return false;
		}

		public void PlayCardPresentation(CardModel card)
		{
		}
	}

	public static NativePlayedCardVisibilityRuntime<CardModel> CreateNativeCardVisibility(string metaKeyPrefix, Func<CardModel?, bool> isCardEligible, Action<string, int> warn)
	{
		return new NativePlayedCardVisibilityRuntime<CardModel>(metaKeyPrefix, new NativePlayedCardVisibilityHost(isCardEligible, warn));
	}

	public static CombatQueuedCardPresentationRuntime<CardModel> CreateQueuedPresentation(NativePlayedCardVisibilityRuntime<CardModel> nativePlayedCards, Func<CardModel?, bool> isCardEligible, Func<CardModel?, bool> isCardPlayPresentationEnabled, Func<CardModel?, bool> usesNativeCardPlayPresentation, Action<CardModel> playCardPresentation, Func<CardModel?, bool>? shouldHideNativePlayedCard = null)
	{
		return new CombatQueuedCardPresentationRuntime<CardModel>(nativePlayedCards, new QueuedCardPresentationHost(isCardEligible, isCardPlayPresentationEnabled, usesNativeCardPlayPresentation, playCardPresentation, shouldHideNativePlayedCard));
	}

	public static CombatQueuedCardPresentationRuntime<CardModel> CreateQueuedNativeCardHideOnly(NativePlayedCardVisibilityRuntime<CardModel> nativePlayedCards, Func<CardModel?, bool> isCardEligible, Func<CardModel?, bool> isCardPlayPresentationEnabled, Func<CardModel?, bool>? shouldHideNativePlayedCard = null)
	{
		return new CombatQueuedCardPresentationRuntime<CardModel>(nativePlayedCards, new QueuedNativeCardHideOnlyHost(isCardEligible, isCardPlayPresentationEnabled, shouldHideNativePlayedCard));
	}
}
