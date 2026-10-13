using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation;

internal static class CombatOverlayPreload
{
	private const float PollIntervalSeconds = 0.1f;

	private static readonly Dictionary<string, List<Resource>> Held = new Dictionary<string, List<Resource>>();

	private static readonly Dictionary<string, int> Generations = new Dictionary<string, int>();

	public static void ScheduleThreadedWhileEnabled(string key, float delaySeconds, Func<bool> isEnabled, IReadOnlyList<string>? directoryRoots, IReadOnlyList<string>? explicitPaths = null, Action? onCompleted = null, Action<Exception>? onFailed = null)
	{
		ArgumentNullException.ThrowIfNull(isEnabled, "isEnabled");
		Release(key);
		int scheduledGeneration = Generations[key];
		try
		{
			if (!isEnabled())
			{
				return;
			}
			GodotScheduler.Delay(Math.Max(0.01f, delaySeconds), () =>
			{
				try
				{
					if (Generations.TryGetValue(key, out var value2) && value2 == scheduledGeneration)
					{
						if (!isEnabled())
						{
							Release(key);
						}
						else
						{
							HoldThreaded(key, directoryRoots, explicitPaths, () =>
							{
								try
								{
									if (!isEnabled())
									{
										Release(key);
									}
									else
									{
										onCompleted?.Invoke();
									}
								}
								catch (Exception obj3)
								{
									Release(key);
									onFailed?.Invoke(obj3);
								}
							});
						}
					}
				}
				catch (Exception obj2)
				{
					Release(key);
					onFailed?.Invoke(obj2);
				}
			});
		}
		catch (Exception obj)
		{
			if (Generations.TryGetValue(key, out var value) && value == scheduledGeneration)
			{
				Release(key);
			}
			onFailed?.Invoke(obj);
		}
	}

	public static void HoldThreaded(string key, IReadOnlyList<string>? directoryRoots, IReadOnlyList<string>? explicitPaths = null, Action? onCompleted = null)
	{
		Release(key);
		int generation = (Generations.TryGetValue(key, out var value) ? value : 0);
		List<string> list = new List<string>();
		if (directoryRoots != null)
		{
			foreach (string directoryRoot in directoryRoots)
			{
				CollectDirectoryPaths(directoryRoot, list);
			}
		}
		if (explicitPaths != null)
		{
			list.AddRange(explicitPaths);
		}
		List<string> list2 = new List<string>();
		foreach (string item in list)
		{
			try
			{
				if (ResourceLoader.LoadThreadedRequest(item, "", useSubThreads: false, ResourceLoader.CacheMode.Reuse) == Error.Ok)
				{
					list2.Add(item);
				}
			}
			catch
			{
			}
		}
		if (list2.Count == 0)
		{
			onCompleted?.Invoke();
			return;
		}
		Held[key] = new List<Resource>(list2.Count);
		PollPending(key, generation, list2, onCompleted);
	}

	public static void Release(string key)
	{
		Held.Remove(key);
		Generations[key] = ((!Generations.TryGetValue(key, out var value)) ? 1 : (value + 1));
	}

	private static void PollPending(string key, int generation, List<string> pending, Action? onCompleted)
	{
		GodotScheduler.Delay(0.1f, () =>
		{
			bool flag = !Generations.TryGetValue(key, out var value) || value != generation;
			List<Resource> value2 = null;
			if (!flag && !Held.TryGetValue(key, out value2))
			{
				flag = true;
			}
			for (int num = pending.Count - 1; num >= 0; num--)
			{
				string path = pending[num];
				ResourceLoader.ThreadLoadStatus threadLoadStatus = ResourceLoader.LoadThreadedGetStatus(path);
				if (threadLoadStatus != ResourceLoader.ThreadLoadStatus.InProgress)
				{
					pending.RemoveAt(num);
					if (threadLoadStatus == ResourceLoader.ThreadLoadStatus.Loaded)
					{
						try
						{
							Resource resource = ResourceLoader.LoadThreadedGet(path);
							if (!flag && resource != null)
							{
								value2.Add(resource);
							}
						}
						catch
						{
						}
					}
				}
			}
			if (pending.Count > 0)
			{
				PollPending(key, generation, pending, onCompleted);
			}
			else if (!flag)
			{
				onCompleted?.Invoke();
			}
		});
	}

	private static void CollectDirectoryPaths(string root, List<string> paths)
	{
		try
		{
			using DirAccess dirAccess = DirAccess.Open(root);
			if (dirAccess == null)
			{
				return;
			}
			dirAccess.ListDirBegin();
			string next = dirAccess.GetNext();
			while (!string.IsNullOrEmpty(next))
			{
				if (!(next == ".") && !(next == ".."))
				{
					string text = root.TrimEnd('/') + "/" + next;
					if (dirAccess.CurrentIsDir())
					{
						CollectDirectoryPaths(text, paths);
					}
					else if (text.EndsWith(".tres", StringComparison.Ordinal) || text.EndsWith(".tscn", StringComparison.Ordinal))
					{
						paths.Add(text);
					}
				}
				next = dirAccess.GetNext();
			}
			dirAccess.ListDirEnd();
		}
		catch
		{
		}
	}
}
