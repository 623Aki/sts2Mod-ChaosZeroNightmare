using System;

namespace CznCore.Presentation;

internal sealed class LeftSideCardPresentationPolicy<TCard>(Func<LeftSideCardPresentationMode> getMode, Func<TCard, bool> isEligibleCard, Func<TCard, bool> hasCutInRoute) where TCard : class
{
	public bool IsEnabled
	{
		get
		{
			LeftSideCardPresentationMode leftSideCardPresentationMode = getMode();
			if ((uint)(leftSideCardPresentationMode - 1) <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	public bool CanShow(TCard? card)
	{
		LeftSideCardPresentationMode leftSideCardPresentationMode = getMode();
		bool flag = (uint)(leftSideCardPresentationMode - 1) <= 1u;
		if (!flag || card == null || !isEligibleCard(card))
		{
			return false;
		}
		if (leftSideCardPresentationMode != LeftSideCardPresentationMode.AllSupportedCards)
		{
			return hasCutInRoute(card);
		}
		return true;
	}
}
