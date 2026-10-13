using System;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;

namespace CznCore.Presentation;

internal static class GdScriptPrewarmInvoker
{
	public static Task InvokeAsync(string scriptPath, string methodName, string label)
	{
		try
		{
			if (!ResourceLoader.Exists(scriptPath))
			{
				SkinLog.Debug(() => "GDScript prewarm skipped (script missing): " + scriptPath);
				return Task.CompletedTask;
			}
			if (!(ResourceLoader.Load(scriptPath, "", ResourceLoader.CacheMode.Reuse) is GDScript gDScript))
			{
				SkinLog.Warn("GDScript prewarm skipped (not a GDScript): " + scriptPath);
				return Task.CompletedTask;
			}
			if (!HasMethod(gDScript, methodName))
			{
				SkinLog.Debug(() => "GDScript prewarm entry not present yet: " + scriptPath + "#" + methodName);
				return Task.CompletedTask;
			}
			ulong ticksMsec = Time.GetTicksMsec();
			Variant variant = gDScript.Call(methodName);
			SkinLog.Info(label + " prewarmed=" + variant.ToString() + " elapsedMs=" + (Time.GetTicksMsec() - ticksMsec));
		}
		catch (Exception ex)
		{
			SkinLog.Warn(label + " GDScript prewarm failed: " + SkinLog.FormatException(ex));
		}
		return Task.CompletedTask;
	}

	private static bool HasMethod(GDScript script, string methodName)
	{
		foreach (Dictionary scriptMethod in script.GetScriptMethodList())
		{
			if (scriptMethod.TryGetValue("name", out var value) && value.AsString() == methodName)
			{
				return true;
			}
		}
		return false;
	}
}
