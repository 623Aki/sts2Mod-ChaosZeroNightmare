using System;
using Godot;
using Timer = global::Godot.Timer;

namespace CznCore.Godot;

public static class GodotScheduler
{
	public static void Deferred(Action action)
	{
		Callable.From(action).CallDeferred();
	}

	public static void Delay(float seconds, Action action)
	{
		((SceneTree)Engine.GetMainLoop()).CreateTimer(seconds).Timeout += action;
	}

	public static bool OneShot(Node parent, float seconds, Action action)
	{
		if (!GodotNodeUtil.IsAlive(parent))
		{
			return false;
		}
		Timer timer = new Timer
		{
			OneShot = true,
			Autostart = false,
			WaitTime = seconds
		};
		parent.AddChild(timer, forceReadableName: false, Node.InternalMode.Disabled);
		timer.Timeout += () =>
		{
			if (GodotNodeUtil.IsAlive(timer))
			{
				timer.QueueFree();
			}
			action();
		};
		timer.Start();
		return true;
	}

	public static void DeferredFrames(Node root, int frames, Action<Node> action)
	{
		if (frames <= 0 || !GodotNodeUtil.IsAlive(root))
		{
			return;
		}
		Deferred(() =>
		{
			if (GodotNodeUtil.IsAlive(root))
			{
				action(root);
				DeferredFrames(root, frames - 1, action);
			}
		});
	}
}
