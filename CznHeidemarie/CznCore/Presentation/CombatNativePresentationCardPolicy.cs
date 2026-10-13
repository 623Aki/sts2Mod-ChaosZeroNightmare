using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatNativePresentationCardPolicy
{
	public static bool UsesNativeCardPlayPresentation(CardModel? card, IReadOnlySet<string>? nativeCardPlayPresentationIdentifiers)
	{
		return MatchesAnyIdentifier(CombatCardIdentifiers.Collect(card), nativeCardPlayPresentationIdentifiers);
	}

	public static bool UsesNativeCardPlayPresentation(CardModel? card, CombatPresentationCardContext context, IReadOnlySet<string>? nativeCardPlayPresentationIdentifiers)
	{
		if (!UsesNativeCardPlayPresentation(card, nativeCardPlayPresentationIdentifiers))
		{
			return UsesNativeCardPlayPresentation(context, nativeCardPlayPresentationIdentifiers);
		}
		return true;
	}

	public static bool UsesNativeCardPlayPresentation(CombatPresentationCardContext context, IReadOnlySet<string>? nativeCardPlayPresentationIdentifiers)
	{
		if (nativeCardPlayPresentationIdentifiers == null || nativeCardPlayPresentationIdentifiers.Count == 0)
		{
			return false;
		}
		if (!IsIdentifierAccepted(context.CardId, nativeCardPlayPresentationIdentifiers) && !IsIdentifierAccepted(context.CardName, nativeCardPlayPresentationIdentifiers) && !IsIdentifierAccepted(context.CardPoolTypeName, nativeCardPlayPresentationIdentifiers) && !IsIdentifierAccepted(context.CardPoolFullTypeName, nativeCardPlayPresentationIdentifiers))
		{
			return MatchesAnyIdentifier(context.CardIdentifiers, nativeCardPlayPresentationIdentifiers);
		}
		return true;
	}

	private static bool MatchesAnyIdentifier(IEnumerable<string>? identifiers, IReadOnlySet<string>? acceptedIdentifiers)
	{
		if (identifiers == null || acceptedIdentifiers == null || acceptedIdentifiers.Count == 0)
		{
			return false;
		}
		foreach (string identifier in identifiers)
		{
			if (IsIdentifierAccepted(identifier, acceptedIdentifiers))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsIdentifierAccepted(string? identifier, IReadOnlySet<string> acceptedIdentifiers)
	{
		if (!string.IsNullOrWhiteSpace(identifier))
		{
			return acceptedIdentifiers.Contains(identifier);
		}
		return false;
	}
}
