using System;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal sealed class RunStartPreloadStep
{
	public float Progress { get; }

	public string Status { get; }

	public Func<Node, Task> ExecuteAsync { get; }

	public Func<bool>? IsEnabled { get; }

	public bool RunDuringLimitedPreload { get; }

	public RunStartPreloadStep(float progress, string status, Func<Node, Task> executeAsync, Func<bool>? isEnabled = null, bool runDuringLimitedPreload = false)
	{
		Progress = progress;
		Status = status;
		ExecuteAsync = executeAsync ?? throw new ArgumentNullException("executeAsync");
		IsEnabled = isEnabled;
		RunDuringLimitedPreload = runDuringLimitedPreload;
	}

	public bool ShouldRun()
	{
		return IsEnabled?.Invoke() ?? true;
	}

	public static RunStartPreloadStep Action(float progress, string status, Action action, Func<bool>? isEnabled = null, bool runDuringLimitedPreload = false)
	{
		return new RunStartPreloadStep(progress, status, (Node _) =>
		{
			action();
			return Task.CompletedTask;
		}, isEnabled, runDuringLimitedPreload);
	}
}
