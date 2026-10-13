using System;

namespace CznCore.Presentation;

internal static class CombatPresentationModeSettings
{
	public const string FastValue = "fast";

	public const string FullValue = "full";

	public const string CustomValue = "custom";

	public static string ToSettingValue(CombatPresentationMode mode)
	{
		return mode switch
		{
			CombatPresentationMode.Fast => "fast", 
			CombatPresentationMode.Full => "full", 
			_ => "custom", 
		};
	}

	public static bool TryParseSettingValue(string? value, out CombatPresentationMode mode)
	{
		if (string.Equals(value, "fast", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "quick", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "multiplayer", StringComparison.OrdinalIgnoreCase))
		{
			mode = CombatPresentationMode.Fast;
			return true;
		}
		if (string.Equals(value, "full", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "complete", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "cinematic", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "balanced", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
		{
			mode = CombatPresentationMode.Full;
			return true;
		}
		if (string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "manual", StringComparison.OrdinalIgnoreCase))
		{
			mode = CombatPresentationMode.Custom;
			return true;
		}
		mode = CombatPresentationMode.Full;
		return false;
	}
}
