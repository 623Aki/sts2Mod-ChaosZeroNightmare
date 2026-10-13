using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatSupportFullScreenPresentationPolicy
{
	public static bool IsSupportCard(CardModel? card)
	{
		CardType? cardType = card?.Type;
		if (cardType.HasValue)
		{
			CardType valueOrDefault = cardType.GetValueOrDefault();
			if ((uint)(valueOrDefault - 2) <= 1u)
			{
				return true;
			}
		}
		return false;
	}

	public static bool Allows(CombatPresentationAudiencePolicy audiencePolicy, CardModel? card)
	{
		ArgumentNullException.ThrowIfNull(audiencePolicy, "audiencePolicy");
		if (IsSupportCard(card) && audiencePolicy.IsLocal(card?.Owner))
		{
			return audiencePolicy.Allows(card, CombatPresentationChannel.CutIn);
		}
		return false;
	}
}
