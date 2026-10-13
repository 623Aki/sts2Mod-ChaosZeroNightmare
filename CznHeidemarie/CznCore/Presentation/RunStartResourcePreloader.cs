using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace CznCore.Presentation;

internal static class RunStartResourcePreloader
{
	private const ulong ThreadedLoadTimeoutMsec = 5000uL;

	public static async Task PreloadResource(Node host, string path)
	{
		try
		{
			if (!ResourceLoader.Exists(path))
			{
				WarmRawFile(path);
				return;
			}
			if (ResourceLoader.LoadThreadedRequest(path, string.Empty, CznRuntimeCapabilities.UseSubThreadsForWarmupResourceLoads, ResourceLoader.CacheMode.Reuse) != Error.Ok)
			{
				ResourceLoader.Load<Resource>(path, string.Empty, ResourceLoader.CacheMode.Reuse);
				return;
			}
			ulong startedMsec = Time.GetTicksMsec();
			ResourceLoader.ThreadLoadStatus threadLoadStatus = ResourceLoader.LoadThreadedGetStatus(path);
			while (true)
			{
				switch (threadLoadStatus)
				{
				case ResourceLoader.ThreadLoadStatus.InProgress:
					if (Time.GetTicksMsec() - startedMsec >= 5000)
					{
						SkinLog.Warn("Run-start threaded preload timed out; continuing native flow path=" + path + " timeoutMs=" + 5000uL);
						return;
					}
					await AwaitFrame(host);
					threadLoadStatus = ResourceLoader.LoadThreadedGetStatus(path);
					break;
				case ResourceLoader.ThreadLoadStatus.Loaded:
					ResourceLoader.LoadThreadedGet(path);
					return;
				default:
					ResourceLoader.Load<Resource>(path, string.Empty, ResourceLoader.CacheMode.Reuse);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Run-start preload skipped resource " + path + ": " + SkinLog.FormatException(ex), 3);
		}
	}

	public static void WarmRawFile(string path)
	{
		try
		{
			if (!global::Godot.FileAccess.FileExists(path))
			{
				return;
			}
			using global::Godot.FileAccess fileAccess = global::Godot.FileAccess.Open(path, global::Godot.FileAccess.ModeFlags.Read);
			ulong num = fileAccess?.GetLength() ?? 0;
			if (fileAccess != null && num != 0L)
			{
				fileAccess.GetBuffer((long)Math.Min(num, 2147483647uL));
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Run-start raw preload skipped file " + path + ": " + SkinLog.FormatException(ex), 3);
		}
	}

	public static async Task AwaitFrame(Node host)
	{
		try
		{
			await host.AwaitProcessFrame();
		}
		catch
		{
			await Task.Yield();
		}
	}
}
