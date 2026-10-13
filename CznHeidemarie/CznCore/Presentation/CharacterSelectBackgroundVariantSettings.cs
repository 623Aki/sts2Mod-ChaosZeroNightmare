using System;

namespace CznCore.Presentation;

internal static class CharacterSelectBackgroundVariantSettings
{
	public const string StaticPortraitValue = "static_portrait";

	public const string LiveLobbyValue = "live_lobby";

	public const string TeamJoinPortraitValue = "team_join_portrait";

	public static string ToSettingValue(CharacterSelectBackgroundVariant variant)
	{
		return variant switch
		{
			CharacterSelectBackgroundVariant.LiveLobby => "live_lobby", 
			CharacterSelectBackgroundVariant.TeamJoinPortrait => "team_join_portrait", 
			_ => "static_portrait", 
		};
	}

	public static bool TryParseSettingValue(string? value, out CharacterSelectBackgroundVariant variant)
	{
		if (string.Equals(value, "team_join_portrait", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "team_join", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "team", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "dynamic_portrait", StringComparison.OrdinalIgnoreCase))
		{
			variant = CharacterSelectBackgroundVariant.TeamJoinPortrait;
			return true;
		}
		if (string.Equals(value, "live_lobby", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "live", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "lobby", StringComparison.OrdinalIgnoreCase))
		{
			variant = CharacterSelectBackgroundVariant.LiveLobby;
			return true;
		}
		if (string.Equals(value, "static_portrait", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "static", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "portrait", StringComparison.OrdinalIgnoreCase))
		{
			variant = CharacterSelectBackgroundVariant.StaticPortrait;
			return true;
		}
		variant = CharacterSelectBackgroundVariant.StaticPortrait;
		return false;
	}
}
