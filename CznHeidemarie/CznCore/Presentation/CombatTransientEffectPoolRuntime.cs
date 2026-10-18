using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatTransientEffectPoolRuntime
{
	private readonly Dictionary<string, PackedScene> _sceneCache = new Dictionary<string, PackedScene>(StringComparer.Ordinal);

	private readonly HashSet<string> _threadedSceneLoads = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, Stack<Node2D>> _pool = new Dictionary<string, Stack<Node2D>>(StringComparer.Ordinal);

	private readonly ConditionalWeakTable<CombatPresentationEffectCue, string> _poolKeyCache = new ConditionalWeakTable<CombatPresentationEffectCue, string>();

	private readonly string _rootMeta;

	private readonly string _groupMeta;

	private readonly string _poolKeyMeta;

	private readonly string _poolStateMeta;

	private readonly string _reservedStateMeta;

	private readonly string _visibleStateMeta;

	private readonly string _generationMeta;

	private readonly string _poolCapMeta;

	private readonly string _poolEpochMeta;

	private readonly int _defaultPoolCap;

	private readonly int _maxPoolCap;

	private readonly Func<Node2D, Node, int> _attachLayers;

	private readonly Action<Node, CombatPresentationEffectCue> _applyMaterial;

	private readonly Func<CombatPresentationEffectCue, int> _poolCapForCue;

	private readonly ISkinTimingProbe _timingProbe;

	private readonly Dictionary<string, int> _poolKeyLastPlaybackBoundary = new Dictionary<string, int>(StringComparer.Ordinal);

	private int _poolEpoch;

	private int _reservedPlaybackCount;

	private int _visiblePlaybackCount;

	private int _roomBoundary;

	private const int IdlePoolTrimRoomBoundaries = 3;

	private const int IdlePoolTrimRetainedCountPerKey = 1;

	public int ActivePlaybackCount => VisiblePlaybackCount;

	public int ReservedPlaybackCount => Math.Max(0, Volatile.Read(in _reservedPlaybackCount));

	public int VisiblePlaybackCount => Math.Max(0, Volatile.Read(in _visiblePlaybackCount));

	public CombatTransientEffectPoolRuntime(string metaKeyPrefix, int defaultPoolCap, int maxPoolCap, Func<Node2D, Node, int> attachLayers, Action<Node, CombatPresentationEffectCue> applyMaterial, Func<CombatPresentationEffectCue, int> poolCapForCue, ISkinTimingProbe timingProbe)
	{
		string text = (string.IsNullOrWhiteSpace(metaKeyPrefix) ? "character_skin" : metaKeyPrefix.Trim());
		_rootMeta = text + "_combat_transient_effects_root";
		_groupMeta = text + "_combat_transient_effects_group";
		_poolKeyMeta = text + "_combat_transient_effect_pool_key";
		_poolStateMeta = text + "_combat_transient_effect_in_pool";
		_reservedStateMeta = text + "_combat_transient_effect_reserved";
		_visibleStateMeta = text + "_combat_transient_effect_active";
		_generationMeta = text + "_combat_transient_effect_generation";
		_poolCapMeta = text + "_combat_transient_effect_pool_cap";
		_poolEpochMeta = text + "_combat_transient_effect_pool_epoch";
		_defaultPoolCap = defaultPoolCap;
		_maxPoolCap = maxPoolCap;
		_attachLayers = attachLayers;
		_applyMaterial = applyMaterial;
		_poolCapForCue = poolCapForCue;
		_timingProbe = timingProbe ?? throw new ArgumentNullException("timingProbe");
	}

	public string DescribeState()
	{
		int num = 0;
		int num2 = 0;
		foreach (Stack<Node2D> value in _pool.Values)
		{
			num += value.Count;
			foreach (Node2D item in value)
			{
				if (GodotObject.IsInstanceValid(item) && IsPoolEpochCurrent(item))
				{
					num2++;
				}
			}
		}
		return "poolKeys=" + _pool.Count + " pooledRoots=" + num + " usablePooledRoots=" + num2 + " cachedScenes=" + _sceneCache.Count + " pendingThreadedLoads=" + _threadedSceneLoads.Count + " reserved=" + ReservedPlaybackCount + " visible=" + VisiblePlaybackCount + " epoch=" + Volatile.Read(in _poolEpoch);
	}

	public PackedScene? LoadScene(string scenePath, bool allowSynchronousFallback = true)
	{
		if (TryGetCachedScene(scenePath, out PackedScene scene))
		{
			return scene;
		}
		if (_threadedSceneLoads.Contains(scenePath))
		{
			TryCompleteThreadedSceneLoad(scenePath, allowSynchronousFallback, allowSynchronousFallback ? "transient_scene_threaded_wait:" : "transient_scene_threaded_poll:");
			if (TryGetCachedScene(scenePath, out scene))
			{
				return scene;
			}
		}
		if (!allowSynchronousFallback)
		{
			PreloadScene(scenePath, allowSynchronousFallback: false);
			return null;
		}
		long startTimestamp = _timingProbe.StartTimingProbe();
		PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath, null, ResourceLoader.CacheMode.Reuse);
		if (packedScene != null)
		{
			_sceneCache[scenePath] = packedScene;
		}
		_timingProbe.LogTimingProbe("transient_scene_load:" + scenePath, startTimestamp);
		return packedScene;
	}

	public bool PreloadScene(string scenePath, bool allowSynchronousFallback = true)
	{
		if (string.IsNullOrWhiteSpace(scenePath) || TryGetCachedScene(scenePath, out PackedScene _))
		{
			return true;
		}
		if (_threadedSceneLoads.Contains(scenePath))
		{
			return TryCompleteThreadedSceneLoad(scenePath, allowBlocking: false, "transient_scene_threaded_preload:");
		}
		long startTimestamp = _timingProbe.StartTimingProbe();
		Error error = ResourceLoader.LoadThreadedRequest(scenePath, string.Empty, CznRuntimeCapabilities.UseSubThreadsForWarmupResourceLoads, ResourceLoader.CacheMode.Reuse);
		_timingProbe.LogTimingProbe("transient_scene_threaded_request:" + scenePath, startTimestamp);
		if (error == Error.Ok)
		{
			_threadedSceneLoads.Add(scenePath);
			return false;
		}
		if (!allowSynchronousFallback)
		{
			return false;
		}
		return LoadScene(scenePath) != null;
	}

	public void ClearSceneCache()
	{
		_sceneCache.Clear();
	}

	private bool TryGetCachedScene(string scenePath, out PackedScene? scene)
	{
		if (_sceneCache.TryGetValue(scenePath, out scene) && scene != null && GodotObject.IsInstanceValid(scene))
		{
			return true;
		}
		scene = null;
		_sceneCache.Remove(scenePath);
		return false;
	}

	private bool TryCompleteThreadedSceneLoad(string scenePath, bool allowBlocking, string timingName)
	{
		switch (ResourceLoader.LoadThreadedGetStatus(scenePath))
		{
		case ResourceLoader.ThreadLoadStatus.InProgress:
			if (!allowBlocking)
			{
				return false;
			}
			return CompleteThreadedSceneLoad(scenePath, timingName);
		case ResourceLoader.ThreadLoadStatus.Loaded:
			return CompleteThreadedSceneLoad(scenePath, timingName);
		default:
			_threadedSceneLoads.Remove(scenePath);
			return false;
		}
	}

	private bool CompleteThreadedSceneLoad(string scenePath, string timingName)
	{
		long startTimestamp = _timingProbe.StartTimingProbe();
		Resource resource = ResourceLoader.LoadThreadedGet(scenePath);
		_threadedSceneLoads.Remove(scenePath);
		if (resource is PackedScene packedScene && GodotObject.IsInstanceValid(packedScene))
		{
			_sceneCache[scenePath] = packedScene;
			_timingProbe.LogTimingProbe(timingName + scenePath, startTimestamp);
			return true;
		}
		_timingProbe.LogTimingProbe(timingName + scenePath, startTimestamp);
		return false;
	}

	public string GetPoolKey(CombatPresentationEffectCue cue)
	{
		return _poolKeyCache.GetValue(cue, CreatePoolKey);
	}

	private static string CreatePoolKey(CombatPresentationEffectCue cue)
	{
		return (cue.Source.ScenePath ?? string.Empty) + "|palette=" + cue.Palette.ToString() + "|neutral=" + cue.UseNeutralPmaMaterial + "|attack=" + cue.UseAttackBoost + "|alpha=" + cue.Alpha.ToString("0.###", CultureInfo.InvariantCulture) + "|preserveRoot=" + cue.PreserveSceneRoot;
	}

	public int GetUsablePooledCount(string poolKey)
	{
		if (!_pool.TryGetValue(poolKey, out Stack<Node2D> value))
		{
			return 0;
		}
		while (value.Count > 0)
		{
			Node2D node2D = value.Peek();
			if (GodotObject.IsInstanceValid(node2D) && IsPoolEpochCurrent(node2D))
			{
				break;
			}
			value.Pop();
			if (GodotObject.IsInstanceValid(node2D))
			{
				node2D.QueueFree();
			}
		}
		int num = 0;
		foreach (Node2D item in value)
		{
			if (GodotObject.IsInstanceValid(item) && IsPoolEpochCurrent(item))
			{
				num++;
			}
		}
		return num;
	}

	public Node2D? AcquireRoot(CombatPresentationEffectCue cue, PackedScene scene, bool allowPoolReuse = true)
	{
		long startTimestamp = _timingProbe.StartTimingProbe();
		string from = GetPoolKey(cue);
		if (allowPoolReuse && _pool.TryGetValue(from, out Stack<Node2D> value))
		{
			while (value.Count > 0)
			{
				Node2D node2D = value.Pop();
				if (GodotObject.IsInstanceValid(node2D))
				{
					if (IsPoolEpochCurrent(node2D))
					{
						node2D.SetMeta(_poolStateMeta, Variant.From<bool>(false));
						node2D.SetMeta(_reservedStateMeta, Variant.From<bool>(false));
						node2D.SetMeta(_visibleStateMeta, Variant.From<bool>(false));
						_applyMaterial(node2D, cue);
						_timingProbe.LogTimingProbe("effect_pool_acquire:" + (cue.GroupName ?? cue.Source.ScenePath ?? "<null>"), startTimestamp);
						return node2D;
					}
					node2D.QueueFree();
				}
			}
		}
		Node node = scene.Instantiate(PackedScene.GenEditState.Disabled);
		if (!(node is Node2D node2D2))
		{
			node.QueueFree();
			return null;
		}
		Node2D node2D3;
		if (cue.PreserveSceneRoot)
		{
			node2D3 = node2D2;
		}
		else
		{
			node2D3 = new Node2D();
			int num = _attachLayers(node2D3, node2D2);
			node2D2.QueueFree();
			if (num == 0)
			{
				node2D3.QueueFree();
				return null;
			}
		}
		CombatEffectRuntime.PrimeNodeCaches(node2D3);
		_applyMaterial(node2D3, cue);
		node2D3.SetMeta(_poolKeyMeta, Variant.From(in from));
		node2D3.SetMeta(_poolStateMeta, Variant.From<bool>(false));
		node2D3.SetMeta(_reservedStateMeta, Variant.From<bool>(false));
		node2D3.SetMeta(_visibleStateMeta, Variant.From<bool>(false));
		node2D3.SetMeta(_generationMeta, Variant.From<int>(0));
		node2D3.SetMeta(_poolCapMeta, Variant.From<int>(_poolCapForCue(cue)));
		SetPoolEpochCurrent(node2D3);
		_timingProbe.LogTimingProbe("effect_instantiate:" + (cue.GroupName ?? cue.Source.ScenePath ?? "<null>"), startTimestamp);
		return node2D3;
	}

	public int BeginPlayback(Node2D effectRoot, CombatPresentationEffectCue cue)
	{
		_poolKeyLastPlaybackBoundary[GetPoolKey(cue)] = _roomBoundary;
		effectRoot.GetParent()?.RemoveChild(effectRoot);
		int from = GetGeneration(effectRoot) + 1;
		effectRoot.SetMeta(_generationMeta, Variant.From(in from));
		effectRoot.SetMeta(_poolStateMeta, Variant.From<bool>(false));
		effectRoot.SetMeta(_visibleStateMeta, Variant.From<bool>(false));
		MarkPlaybackReserved(effectRoot);
		effectRoot.SetMeta(_rootMeta, Variant.From<bool>(true));
		effectRoot.SetMeta(_groupMeta, Variant.From<string>(cue.GroupName ?? string.Empty));
		return from;
	}

	public bool MarkPlaybackVisible(Node2D effectRoot, int expectedGeneration)
	{
		if (!IsActive(effectRoot, expectedGeneration))
		{
			return false;
		}
		MarkPlaybackVisible(effectRoot);
		return true;
	}

	public void Release(Node2D effectRoot, int? expectedGeneration = null)
	{
		long startTimestamp = _timingProbe.StartTimingProbe();
		if (!GodotObject.IsInstanceValid(effectRoot) || IsInPool(effectRoot) || (expectedGeneration.HasValue && GetGeneration(effectRoot) != expectedGeneration.Value))
		{
			return;
		}
		MarkPlaybackInactive(effectRoot);
		int from = GetGeneration(effectRoot) + 1;
		effectRoot.SetMeta(_generationMeta, Variant.From(in from));
		effectRoot.SetMeta(_poolStateMeta, Variant.From<bool>(true));
		CombatEffectRuntime.StopParticles(effectRoot);
		effectRoot.Visible = false;
		effectRoot.GetParent()?.RemoveChild(effectRoot);
		if (!IsPoolEpochCurrent(effectRoot))
		{
			effectRoot.QueueFree();
			_timingProbe.LogTimingProbe("effect_release_free:expired_epoch", startTimestamp);
			return;
		}
		string text = string.Empty;
		try
		{
			if (effectRoot.HasMeta(_poolKeyMeta))
			{
				text = effectRoot.GetMeta(_poolKeyMeta).AsString();
			}
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			effectRoot.QueueFree();
			_timingProbe.LogTimingProbe("effect_release_free:missing_pool_key", startTimestamp);
			return;
		}
		if (!_pool.TryGetValue(text, out Stack<Node2D> value))
		{
			value = new Stack<Node2D>();
			_pool[text] = value;
		}
		if (value.Count >= GetPoolCap(effectRoot))
		{
			effectRoot.QueueFree();
			_timingProbe.LogTimingProbe("effect_release_free:pool_cap:" + text, startTimestamp);
		}
		else
		{
			value.Push(effectRoot);
			_timingProbe.LogTimingProbe("effect_release_pool:" + text, startTimestamp);
		}
	}

	public void AdvancePoolEpoch()
	{
		Interlocked.Increment(ref _poolEpoch);
	}

	public void TrimIdlePools()
	{
		_roomBoundary++;
		int num = 0;
		int num2 = 0;
		List<string> emptyKeys = new List<string>();
		foreach (var (key, stack2) in _pool)
		{
			if (!_poolKeyLastPlaybackBoundary.TryGetValue(key, out var value))
			{
				_poolKeyLastPlaybackBoundary[key] = _roomBoundary;
			}
			else
			{
				if (_roomBoundary - value < 3 || stack2.Count <= 1)
				{
					continue;
				}
				num++;
				while (stack2.Count > 1)
				{
					Node2D node2D = stack2.Pop();
					if (GodotObject.IsInstanceValid(node2D))
					{
						node2D.QueueFree();
						num2++;
					}
				}
			}
			if (stack2.Count == 0)
			{
				emptyKeys.Add(key);
			}
		}
		foreach (string key in emptyKeys)
		{
			_pool.Remove(key);
			_poolKeyLastPlaybackBoundary.Remove(key);
		}
		int boundary = _roomBoundary;
		int keys = num;
		int freed = num2;
		SkinLog.Debug(() => "transient_pool_state reason=room_boundary boundary=" + boundary + " trimmedKeys=" + keys + " freedRoots=" + freed + " " + DescribeState());
	}

	public void ClearPool()
	{
		foreach (Stack<Node2D> value in _pool.Values)
		{
			while (value.Count > 0)
			{
				Node2D node2D = value.Pop();
				if (GodotObject.IsInstanceValid(node2D))
				{
					node2D.QueueFree();
				}
			}
		}
		_pool.Clear();
	}

	public void MarkQueuedForFree(Node2D effectRoot)
	{
		if (GodotObject.IsInstanceValid(effectRoot) && IsTransientRoot(effectRoot))
		{
			int from = GetGeneration(effectRoot) + 1;
			effectRoot.SetMeta(_generationMeta, Variant.From(in from));
			effectRoot.SetMeta(_poolStateMeta, Variant.From<bool>(true));
			MarkPlaybackInactive(effectRoot);
		}
	}

	public bool IsTransientRoot(Node node)
	{
		try
		{
			return node.HasMeta(_rootMeta);
		}
		catch
		{
			return false;
		}
	}

	public int GetGeneration(Node2D effectRoot)
	{
		try
		{
			if (effectRoot.HasMeta(_generationMeta))
			{
				return effectRoot.GetMeta(_generationMeta).AsInt32();
			}
		}
		catch
		{
		}
		return 0;
	}

	public bool IsInPool(Node2D effectRoot)
	{
		try
		{
			return effectRoot.HasMeta(_poolStateMeta) && effectRoot.GetMeta(_poolStateMeta).AsBool();
		}
		catch
		{
			return false;
		}
	}

	public bool IsActive(Node2D effectRoot, int generation)
	{
		if (GodotObject.IsInstanceValid(effectRoot) && !IsInPool(effectRoot))
		{
			return GetGeneration(effectRoot) == generation;
		}
		return false;
	}

	private int GetPoolCap(Node2D effectRoot)
	{
		try
		{
			if (effectRoot.HasMeta(_poolCapMeta))
			{
				int num = effectRoot.GetMeta(_poolCapMeta).AsInt32();
				if (num > 0)
				{
					return Math.Clamp(num, _defaultPoolCap, _maxPoolCap);
				}
			}
		}
		catch
		{
		}
		return _defaultPoolCap;
	}

	private bool IsPlaybackReserved(Node2D effectRoot)
	{
		return GetPlaybackState(effectRoot, _reservedStateMeta);
	}

	private bool IsPlaybackVisible(Node2D effectRoot)
	{
		return GetPlaybackState(effectRoot, _visibleStateMeta);
	}

	private bool GetPlaybackState(Node2D effectRoot, string metaKey)
	{
		try
		{
			return effectRoot.HasMeta(metaKey) && effectRoot.GetMeta(metaKey).AsBool();
		}
		catch
		{
			return false;
		}
	}

	private void MarkPlaybackReserved(Node2D effectRoot)
	{
		if (!IsPlaybackReserved(effectRoot))
		{
			effectRoot.SetMeta(_reservedStateMeta, Variant.From<bool>(true));
			Interlocked.Increment(ref _reservedPlaybackCount);
		}
	}

	private void MarkPlaybackVisible(Node2D effectRoot)
	{
		if (!IsPlaybackVisible(effectRoot))
		{
			effectRoot.SetMeta(_visibleStateMeta, Variant.From<bool>(true));
			Interlocked.Increment(ref _visiblePlaybackCount);
		}
	}

	private void MarkPlaybackInactive(Node2D effectRoot)
	{
		if (IsPlaybackVisible(effectRoot))
		{
			effectRoot.SetMeta(_visibleStateMeta, Variant.From<bool>(false));
			if (Interlocked.Decrement(ref _visiblePlaybackCount) < 0)
			{
				Interlocked.Exchange(ref _visiblePlaybackCount, 0);
			}
		}
		if (IsPlaybackReserved(effectRoot))
		{
			effectRoot.SetMeta(_reservedStateMeta, Variant.From<bool>(false));
			if (Interlocked.Decrement(ref _reservedPlaybackCount) < 0)
			{
				Interlocked.Exchange(ref _reservedPlaybackCount, 0);
			}
		}
	}

	private int GetPoolEpoch(Node2D effectRoot)
	{
		try
		{
			if (effectRoot.HasMeta(_poolEpochMeta))
			{
				return effectRoot.GetMeta(_poolEpochMeta).AsInt32();
			}
		}
		catch
		{
		}
		return Volatile.Read(in _poolEpoch);
	}

	private bool IsPoolEpochCurrent(Node2D effectRoot)
	{
		return GetPoolEpoch(effectRoot) == Volatile.Read(in _poolEpoch);
	}

	private void SetPoolEpochCurrent(Node2D effectRoot)
	{
		effectRoot.SetMeta(_poolEpochMeta, Variant.From<int>(Volatile.Read(in _poolEpoch)));
	}
}
