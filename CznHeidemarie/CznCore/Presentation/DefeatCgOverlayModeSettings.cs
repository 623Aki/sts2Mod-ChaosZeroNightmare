using System;

namespace CznCore.Presentation;

internal static class DefeatCgOverlayModeSettings
{
	public const string OffValue = "off";

	public const string Type1Value = "type1";

	public const string Type2Value = "type2";

	public const string RandomValue = "random";

	public static bool TryParseSettingValue(string? value, out DefeatCgOverlayMode mode)
	{
		if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
		{
			mode = DefeatCgOverlayMode.Off;
			return true;
		}
		if (string.Equals(value, "type1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "type_1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "first", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
		{
			mode = DefeatCgOverlayMode.Type1;
			return true;
		}
		if (string.Equals(value, "type2", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "type_2", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "second", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "2", StringComparison.OrdinalIgnoreCase))
		{
			mode = DefeatCgOverlayMode.Type2;
			return true;
		}
		if (string.Equals(value, "random", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "3", StringComparison.OrdinalIgnoreCase))
		{
			mode = DefeatCgOverlayMode.Random;
			return true;
		}
		if (int.TryParse(value, out var result) && Enum.IsDefined(typeof(DefeatCgOverlayMode), result))
		{
			mode = (DefeatCgOverlayMode)result;
			return true;
		}
		mode = DefeatCgOverlayMode.Random;
		return false;
	}

	public static string ToSettingValue(DefeatCgOverlayMode mode)
	{
		return mode switch
		{
			DefeatCgOverlayMode.Off => "off", 
			DefeatCgOverlayMode.Type1 => "type1", 
			DefeatCgOverlayMode.Type2 => "type2", 
			_ => "random", 
		};
	}
}
