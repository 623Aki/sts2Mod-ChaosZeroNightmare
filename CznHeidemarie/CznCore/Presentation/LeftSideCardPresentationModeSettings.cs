using System;

namespace CznCore.Presentation;

internal static class LeftSideCardPresentationModeSettings
{
	public const string OffValue = "off";

	public const string AllSupportedCardsValue = "all_supported_cards";

	public const string CutInCardsOnlyValue = "cut_in_cards_only";

	public static bool TryParseSettingValue(string? value, out LeftSideCardPresentationMode mode)
	{
		if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
		{
			mode = LeftSideCardPresentationMode.Off;
			return true;
		}
		if (string.Equals(value, "all_supported_cards", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
		{
			mode = LeftSideCardPresentationMode.AllSupportedCards;
			return true;
		}
		if (string.Equals(value, "cut_in_cards_only", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "cut_in_only", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "cutin_only", StringComparison.OrdinalIgnoreCase))
		{
			mode = LeftSideCardPresentationMode.CutInCardsOnly;
			return true;
		}
		if (int.TryParse(value, out var result) && Enum.IsDefined(typeof(LeftSideCardPresentationMode), result))
		{
			mode = (LeftSideCardPresentationMode)result;
			return true;
		}
		mode = LeftSideCardPresentationMode.AllSupportedCards;
		return false;
	}

	public static string ToSettingValue(LeftSideCardPresentationMode mode)
	{
		return mode switch
		{
			LeftSideCardPresentationMode.Off => "off", 
			LeftSideCardPresentationMode.CutInCardsOnly => "cut_in_cards_only", 
			_ => "all_supported_cards", 
		};
	}
}
