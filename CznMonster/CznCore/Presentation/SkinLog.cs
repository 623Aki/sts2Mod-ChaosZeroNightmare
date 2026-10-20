using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;

namespace CznCore.Presentation;

internal static class SkinLog
{
	private const string DefaultPrefix = "[CharacterSkin]";

	private static string _prefix = "[CharacterSkin]";

	private static Func<bool>? _isDebugEnabled;

	private static readonly object OnceSync = new object();

	private static readonly HashSet<string> InfoOnceKeys = new HashSet<string>(StringComparer.Ordinal);

	public static void Configure(string? prefix)
	{
		_prefix = (string.IsNullOrWhiteSpace(prefix) ? "[CharacterSkin]" : prefix.Trim());
	}

	public static void ConfigureDebug(Func<bool>? isDebugEnabled)
	{
		_isDebugEnabled = isDebugEnabled;
	}

	public static void Debug(string message, int priority = 2)
	{
		Func<bool>? isDebugEnabled = _isDebugEnabled;
		if (isDebugEnabled != null && isDebugEnabled())
		{
			Info(message, priority);
		}
	}

	public static void Debug(Func<string> messageFactory, int priority = 2)
	{
		Func<bool>? isDebugEnabled = _isDebugEnabled;
		if (isDebugEnabled != null && isDebugEnabled())
		{
			Info(messageFactory(), priority);
		}
	}

	public static void Info(string message, int priority = 2)
	{
		Log.Info(Format(message), priority);
	}

	public static void InfoOnce(string key, string message, int priority = 2)
	{
		if (!string.IsNullOrWhiteSpace(key) && TryMarkInfoOnce(key))
		{
			Info(message, priority);
		}
	}

	public static void Warn(string message, int priority = 2)
	{
		Log.Warn(Format(message), priority);
	}

	public static void Error(string message, int priority = 2)
	{
		Log.Error(Format(message), priority);
	}

	public static void Debug(string message, bool enabled, int priority = 2)
	{
		if (enabled)
		{
			Info(message, priority);
		}
	}

	public static void WarnException(string context, Exception ex, int priority = 2)
	{
		Warn(context + FormatException(ex), priority);
	}

	public static void ErrorException(string context, Exception ex, int priority = 2)
	{
		Error(context + FormatException(ex), priority);
	}

	public static string Format(string message)
	{
		string text = StripExistingPrefix(message);
		if (string.IsNullOrWhiteSpace(text))
		{
			return _prefix;
		}
		return _prefix + " " + text.TrimStart();
	}

	public static string FormatException(Exception ex)
	{
		return ex.GetType().Name + ": " + ex.Message;
	}

	private static bool TryMarkInfoOnce(string key)
	{
		lock (OnceSync)
		{
			return InfoOnceKeys.Add(key);
		}
	}

	private static string StripExistingPrefix(string message)
	{
		if (string.IsNullOrEmpty(message) || message[0] != '[')
		{
			return message;
		}
		int num = message.IndexOf(']');
		if (num <= 0 || num + 1 >= message.Length || message[num + 1] != ' ')
		{
			return message;
		}
		int num2 = num + 2;
		return message.Substring(num2, message.Length - num2);
	}
}
