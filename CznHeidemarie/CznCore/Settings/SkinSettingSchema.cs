using System;

namespace CznCore.Settings;

internal static class SkinSettingSchema
{
	public const string DefaultCharacterId = "default";

	private const string GlobalPrefix = "global.";

	private const string CharactersPrefix = "characters.";

	public static string Global(string settingName)
	{
		return "global." + settingName;
	}

	public static string Character(string characterId, string settingName)
	{
		return "characters." + characterId + "." + settingName;
	}

	public static bool MatchesGlobal(string key, string settingName, params string[] legacyAliases)
	{
		if (string.Equals(key, Global(settingName), StringComparison.Ordinal) || string.Equals(key, settingName, StringComparison.Ordinal))
		{
			return true;
		}
		return MatchesAny(key, legacyAliases);
	}

	public static bool MatchesCharacter(string key, string characterId, string settingName, params string[] legacyAliases)
	{
		if (string.Equals(key, Character(characterId, settingName), StringComparison.Ordinal) || string.Equals(key, characterId + "." + settingName, StringComparison.Ordinal))
		{
			return true;
		}
		return MatchesAny(key, legacyAliases);
	}

	public static bool TryParseCharacterSetting(string key, out string characterId, out string settingName)
	{
		characterId = string.Empty;
		settingName = string.Empty;
		if (!key.StartsWith("characters.", StringComparison.Ordinal))
		{
			return false;
		}
		string text = key;
		int length = "characters.".Length;
		string text2 = text.Substring(length, text.Length - length);
		int num = text2.IndexOf('.');
		if (num <= 0 || num >= text2.Length - 1)
		{
			return false;
		}
		characterId = text2.Substring(0, num);
		text = text2;
		length = num + 1;
		settingName = text.Substring(length, text.Length - length);
		return true;
	}

	private static bool MatchesAny(string key, string[] aliases)
	{
		foreach (string b in aliases)
		{
			if (string.Equals(key, b, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
