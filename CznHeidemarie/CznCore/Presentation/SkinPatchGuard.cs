using System;

namespace CznCore.Presentation;

internal static class SkinPatchGuard
{
	public static void Run(string context, Action action, int priority = 2)
	{
		Run(context, action, SkinLog.Warn, priority);
	}

	public static void Run(string context, Action action, Action<string, int> warn, int priority = 2)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			warn(context + " failed: " + SkinLog.FormatException(ex), priority);
		}
	}
}
