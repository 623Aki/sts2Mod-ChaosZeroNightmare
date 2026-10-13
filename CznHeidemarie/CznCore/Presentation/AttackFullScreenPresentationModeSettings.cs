using System;

namespace CznCore.Presentation;

internal static class AttackFullScreenPresentationModeSettings
{
	public const string AlwaysValue = "always";

	public const string LethalOnlyValue = "lethal_only";

	public static bool TryParseSettingValue(string? value, out AttackFullScreenPresentationMode mode)
	{
		if (string.Equals(value, "lethal_only", StringComparison.OrdinalIgnoreCase))
		{
			mode = AttackFullScreenPresentationMode.LethalOnly;
			return true;
		}
		if (string.Equals(value, "always", StringComparison.OrdinalIgnoreCase))
		{
			mode = AttackFullScreenPresentationMode.Always;
			return true;
		}
		mode = AttackFullScreenPresentationMode.Always;
		return false;
	}

	public static string ToSettingValue(AttackFullScreenPresentationMode mode)
	{
		if (mode != AttackFullScreenPresentationMode.LethalOnly)
		{
			return "always";
		}
		return "lethal_only";
	}
}
