using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CznCore.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace CznCore.Settings;

internal sealed class SettingsLocalizationStore
{
	private readonly string _localizationPathFormat;

	private readonly Action<string, int> _warn;

	private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

	public SettingsLocalizationStore(string localizationPathFormat, Action<string, int>? warn = null)
	{
		_localizationPathFormat = localizationPathFormat;
		_warn = warn ?? new Action<string, int>(NoopWarn);
	}

	public string Get(string key, string fallback)
	{
		string currentLanguageCode = GetCurrentLanguageCode();
		if (TryGetLocalizedValue(currentLanguageCode, key, out string value))
		{
			return value ?? fallback;
		}
		if (!string.Equals(currentLanguageCode, "en", StringComparison.Ordinal) && TryGetLocalizedValue("en", key, out value))
		{
			return value ?? fallback;
		}
		return fallback;
	}

	private bool TryGetLocalizedValue(string language, string key, out string? value)
	{
		if (LoadLanguage(language).TryGetValue(key, out value))
		{
			return !string.IsNullOrWhiteSpace(value);
		}
		return false;
	}

	private IReadOnlyDictionary<string, string> LoadLanguage(string language)
	{
		string localizationFolderName = GetLocalizationFolderName(language);
		if (_cache.TryGetValue(localizationFolderName, out IReadOnlyDictionary<string, string> value))
		{
			return value;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		try
		{
			string path = string.Format(CultureInfo.InvariantCulture, _localizationPathFormat, localizationFolderName);
			if (global::Godot.FileAccess.FileExists(path))
			{
				using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open(path, global::Godot.FileAccess.ModeFlags.Read);
				Dictionary<string, string> dictionary2 = JsonSerializer.Deserialize<Dictionary<string, string>>(fileAccess?.GetAsText() ?? string.Empty);
				if (dictionary2 != null)
				{
					dictionary = new Dictionary<string, string>(dictionary2, StringComparer.Ordinal);
				}
			}
		}
		catch (Exception ex)
		{
			_warn("Settings localization load failed: " + localizationFolderName + " " + SkinLog.FormatException(ex), 2);
		}
		_cache[localizationFolderName] = dictionary;
		return dictionary;
	}

	private static string GetCurrentLanguageCode()
	{
		string text = null;
		try
		{
			text = LocManager.Instance?.Language;
		}
		catch
		{
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			try
			{
				text = TranslationServer.GetLocale();
			}
			catch
			{
			}
		}
		return NormalizeLanguageCode(text);
	}

	private static string NormalizeLanguageCode(string? code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return "en";
		}
		string text = code.Trim().ToLowerInvariant();
		if (text == "jpn" || text.StartsWith("ja", StringComparison.Ordinal) || text.Contains("jp", StringComparison.Ordinal))
		{
			return "jpn";
		}
		if (text == "kor" || text == "ko" || text.StartsWith("ko-", StringComparison.Ordinal) || text.StartsWith("ko_", StringComparison.Ordinal))
		{
			return "kor";
		}
		if (text == "zht" || text.Contains("hant", StringComparison.Ordinal) || text.Contains("tw", StringComparison.Ordinal) || text.Contains("hk", StringComparison.Ordinal) || text.Contains("mo", StringComparison.Ordinal))
		{
			return "zht";
		}
		if (text == "zhs" || text.StartsWith("zh", StringComparison.Ordinal))
		{
			return "zhs";
		}
		return "en";
	}

	private static string GetLocalizationFolderName(string language)
	{
		return language switch
		{
			"kor" => "kor", 
			"zhs" => "zhs", 
			"zht" => "zht", 
			"jpn" => "jpn", 
			_ => "eng", 
		};
	}

	private static void NoopWarn(string message, int priority)
	{
	}
}
