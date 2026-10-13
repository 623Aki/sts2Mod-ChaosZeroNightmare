using System;

namespace CznCore.Presentation;

internal static class CardSelectionPresentationTriggerSettings
{
	public const string CardHoverValue = "card_hover";

	public const string CardSelectionValue = "card_selection";

	public static bool TryParseSettingValue(string? value, out CardSelectionPresentationTrigger trigger)
	{
		if (string.Equals(value, "card_hover", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "hover", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "card_hovered", StringComparison.OrdinalIgnoreCase))
		{
			trigger = CardSelectionPresentationTrigger.CardHover;
			return true;
		}
		if (string.Equals(value, "card_selection", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "selection", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "card_selected", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "targeting_started", StringComparison.OrdinalIgnoreCase))
		{
			trigger = CardSelectionPresentationTrigger.CardSelection;
			return true;
		}
		if (int.TryParse(value, out var result) && Enum.IsDefined(typeof(CardSelectionPresentationTrigger), result))
		{
			trigger = (CardSelectionPresentationTrigger)result;
			return true;
		}
		trigger = CardSelectionPresentationTrigger.CardHover;
		return false;
	}

	public static string ToSettingValue(CardSelectionPresentationTrigger trigger)
	{
		if (trigger == CardSelectionPresentationTrigger.CardHover)
		{
			return "card_hover";
		}
		return "card_selection";
	}
}
