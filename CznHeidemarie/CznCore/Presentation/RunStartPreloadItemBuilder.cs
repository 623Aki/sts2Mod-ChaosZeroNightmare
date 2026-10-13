using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal sealed class RunStartPreloadItemBuilder
{
	private readonly Dictionary<string, RunStartPreloadItem> _items = new Dictionary<string, RunStartPreloadItem>(StringComparer.Ordinal);

	public void AddResource(string? path, string label)
	{
		if (!string.IsNullOrWhiteSpace(path) && !_items.ContainsKey(path))
		{
			_items.Add(path, RunStartPreloadItem.Resource(path, label));
		}
	}

	public void AddCustom(string key, string? path, string label, Func<Node, Task> preloadAsync)
	{
		if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(path) && !_items.ContainsKey(key))
		{
			_items.Add(key, RunStartPreloadItem.Custom(key, path, label, preloadAsync));
		}
	}

	public void AddGdScriptPrewarm(string scriptPath, string methodName, string label)
	{
		AddCustom("gdscript-prewarm:" + scriptPath + "#" + methodName, scriptPath, label, (Node _) => GdScriptPrewarmInvoker.InvokeAsync(scriptPath, methodName, label));
	}

	public void AddEffectSource(CombatPresentationEffectSource source, string label)
	{
		if (source.Kind == CombatPresentationEffectSourceKind.Scene)
		{
			AddResource(source.ScenePath, label);
			return;
		}
		foreach (CombatPresentationEffectLayer layer in source.Layers)
		{
			AddResource(layer.SkeletonPath, label);
			AddResource(layer.AtlasPath, label);
		}
	}

	public RunStartPreloadItem[] ToArray()
	{
		RunStartPreloadItem[] array = new RunStartPreloadItem[_items.Count];
		_items.Values.CopyTo(array, 0);
		return array;
	}
}
