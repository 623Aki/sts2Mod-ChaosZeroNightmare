using System;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal sealed class RunStartPreloadItem
{
	public string Key { get; }

	public string Path { get; }

	public string Label { get; }

	public Func<Node, Task>? PreloadAsync { get; }

	private RunStartPreloadItem(string key, string path, string label, Func<Node, Task>? preloadAsync)
	{
		Key = key;
		Path = path;
		Label = label;
		PreloadAsync = preloadAsync;
	}

	public static RunStartPreloadItem Resource(string path, string label)
	{
		return new RunStartPreloadItem(path, path, label, null);
	}

	public static RunStartPreloadItem Custom(string key, string path, string label, Func<Node, Task> preloadAsync)
	{
		return new RunStartPreloadItem(key, path, label, preloadAsync ?? throw new ArgumentNullException("preloadAsync"));
	}
}
