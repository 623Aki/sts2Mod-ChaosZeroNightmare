using System;
using System.Collections.Generic;
using System.Threading;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatWarmupRuntime
{
	private sealed record EffectWarmupPlan(CombatPresentationEffectCue Cue, string PoolKey, EffectWarmupWave Wave);

	private sealed record EffectWarmupItem(CombatPresentationEffectCue Cue, EffectWarmupWave Wave, string? PoolKey = null);

	private enum EffectWarmupWave
	{
		Starter,
		FollowUp,
		Deferred
	}

	private sealed class WarmupRunStats
	{
		private readonly string _kind;

		private readonly int _totalItems;

		private readonly string _details;

		private readonly long _startedAtMs;

		private int _busyRetries;

		private int _resourceWaits;

		public int Token { get; }

		public WarmupRunStats(string kind, int token, int totalItems, string details)
		{
			_kind = kind;
			Token = token;
			_totalItems = totalItems;
			_details = details;
			_startedAtMs = System.Environment.TickCount64;
		}

		public void RecordBusyRetry()
		{
			_busyRetries++;
		}

		public void RecordResourceWait()
		{
			_resourceWaits++;
		}

		public string FormatStart()
		{
			return "warmup_profile_start kind=" + _kind + " token=" + Token + " total=" + _totalItems + " " + _details;
		}

		public string FormatComplete()
		{
			long num = Math.Max(0L, System.Environment.TickCount64 - _startedAtMs);
			return "warmup_profile_done kind=" + _kind + " token=" + Token + " total=" + _totalItems + " busy_retries=" + _busyRetries + " resource_waits=" + _resourceWaits + " elapsed_ms=" + num + " " + _details;
		}
	}

	private readonly ICombatPresentationProfile _profile;

	private readonly CombatWarmupPolicyProfile _policy;

	private readonly ICombatWarmupHost _host;

	private readonly Func<CombatPresentationEffectCue, bool> _shouldWarmupCue;

	private readonly ISkinTimingProbe _timingProbe;

	private readonly Action<string, int> _warn;

	private EffectWarmupItem[]? _sceneCacheWarmupItems;

	private int _sceneCacheWarmupStarted;

	private int _sceneCacheWarmupToken;

	private EffectWarmupPlan[]? _effectWarmupPlans;

	private EffectWarmupItem[]? _effectWarmupItems;

	private int _effectWarmupTargetCount;

	private int _effectWarmupToken;

	private string[]? _sfxPrewarmPaths;

	private int _sfxPrewarmToken;

	private WarmupRunStats? _sceneCacheWarmupStats;

	private WarmupRunStats? _effectWarmupStats;

	private WarmupRunStats? _sfxPrewarmStats;

	public ICombatWarmupHost Host => _host;

	public CombatWarmupRuntime(ICombatPresentationProfile profile, CombatWarmupPolicyProfile policy, ICombatWarmupHost host, ISkinTimingProbe timingProbe, Action<string, int> warn)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		_policy = policy ?? throw new ArgumentNullException("policy");
		_host = host ?? throw new ArgumentNullException("host");
		_shouldWarmupCue = ((host is ICombatWarmupCueFilterHost combatWarmupCueFilterHost) ? new Func<CombatPresentationEffectCue, bool>(combatWarmupCueFilterHost.ShouldWarmupCue) : ((Func<CombatPresentationEffectCue, bool>)((CombatPresentationEffectCue _) => true)));
		_timingProbe = timingProbe ?? throw new ArgumentNullException("timingProbe");
		_warn = warn ?? throw new ArgumentNullException("warn");
	}

	public void Stop()
	{
		Interlocked.Increment(ref _sceneCacheWarmupToken);
		Interlocked.Increment(ref _effectWarmupToken);
		Interlocked.Increment(ref _sfxPrewarmToken);
		_sceneCacheWarmupStats = null;
		_effectWarmupStats = null;
		_sfxPrewarmStats = null;
	}

	public void ResetSceneCachePreload()
	{
		Interlocked.Exchange(ref _sceneCacheWarmupStarted, 0);
		Interlocked.Increment(ref _sceneCacheWarmupToken);
		_sceneCacheWarmupStats = null;
	}

	public int GetEffectPoolCap(CombatPresentationEffectCue cue)
	{
		return GetEffectPoolCap(cue, _host.CountEffectTargets());
	}

	public int GetEffectPoolWarmupTargetCount(CombatPresentationEffectCue cue)
	{
		return GetEffectWarmupTargetCount(cue, _host.CountEffectTargets());
	}

	public int GetStarterEffectPoolWarmupTargetCount(CombatPresentationEffectCue cue)
	{
		return GetStarterEffectWarmupTargetCount(cue, _host.CountEffectTargets());
	}

	public void ScheduleTransientEffectSceneCachePreload()
	{
		if (!_host.AreEffectsEnabled() || Interlocked.Exchange(ref _sceneCacheWarmupStarted, 1) == 1)
		{
			return;
		}
		EffectWarmupItem[] items = GetSceneCacheWarmupItems();
		if (items.Length == 0)
		{
			return;
		}
		int token = Interlocked.Increment(ref _sceneCacheWarmupToken);
		_sceneCacheWarmupStats = CreateWarmupRunStats("scene_cache", token, items.Length, () => DescribeWarmupWaves(items));
		try
		{
			ScheduleSceneCacheWarmupStep(token, 0, _policy.SceneCacheWarmupInitialDelaySeconds);
		}
		catch (Exception ex)
		{
			_warn("Combat transition scene cache preload scheduling failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void ScheduleTransientEffectPoolWarmup()
	{
		if (!_host.AreEffectsEnabled())
		{
			return;
		}
		_effectWarmupItems = null;
		EffectWarmupItem[] items = GetEffectWarmupItems();
		if (items.Length == 0)
		{
			return;
		}
		int token = Interlocked.Increment(ref _effectWarmupToken);
		_effectWarmupStats = CreateWarmupRunStats("effect_pool", token, items.Length, () => DescribeWarmupWaves(items) + " target_count=" + Volatile.Read(in _effectWarmupTargetCount));
		try
		{
			ScheduleEffectWarmupStep(token, 0, _policy.CombatWarmupInitialDelaySeconds);
		}
		catch (Exception ex)
		{
			_warn("Combat transition effects warmup scheduling failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private EffectWarmupItem[] GetSceneCacheWarmupItems()
	{
		if (_sceneCacheWarmupItems != null)
		{
			return _sceneCacheWarmupItems;
		}
		List<EffectWarmupItem> list = new List<EffectWarmupItem>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CombatPresentationEffectCue item in _profile.EnumerateTransientEffectCues())
		{
			if (item.Source.Kind == CombatPresentationEffectSourceKind.Scene && !string.IsNullOrEmpty(item.Source.ScenePath) && !ShouldSkipWarmup(item) && _shouldWarmupCue(item) && hashSet.Add(item.Source.ScenePath))
			{
				EffectWarmupWave sceneCacheWarmupWave = GetSceneCacheWarmupWave(item);
				if (ShouldWarmWave(sceneCacheWarmupWave))
				{
					list.Add(new EffectWarmupItem(item, sceneCacheWarmupWave));
				}
			}
		}
		list.Sort(CompareEffectWarmupPriority);
		_sceneCacheWarmupItems = list.ToArray();
		return _sceneCacheWarmupItems;
	}

	public void ScheduleSfxPrewarm()
	{
		if (!_host.AreEffectsEnabled() || !_host.IsSfxEnabled())
		{
			return;
		}
		string[] paths = GetSfxPrewarmPaths();
		if (paths.Length == 0)
		{
			return;
		}
		int token = Interlocked.Increment(ref _sfxPrewarmToken);
		_sfxPrewarmStats = CreateWarmupRunStats("sfx", token, paths.Length, () => DescribeSfxPrewarmPriorities(paths));
		try
		{
			ScheduleSfxWarmupStep(token, 0, _policy.CombatWarmupInitialDelaySeconds);
		}
		catch (Exception ex)
		{
			_warn("Combat SFX warmup scheduling failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private EffectWarmupPlan[] GetEffectWarmupPlans()
	{
		if (_effectWarmupPlans != null)
		{
			return _effectWarmupPlans;
		}
		List<EffectWarmupPlan> list = new List<EffectWarmupPlan>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (CombatPresentationEffectCue item in _profile.EnumerateTransientEffectCues())
		{
			if (item.Source.Kind != CombatPresentationEffectSourceKind.Scene || string.IsNullOrEmpty(item.Source.ScenePath) || ShouldSkipWarmup(item) || !_shouldWarmupCue(item))
			{
				continue;
			}
			string poolKey = _host.GetPoolKey(item);
			if (hashSet.Add(poolKey))
			{
				EffectWarmupWave warmupWave = GetWarmupWave(item);
				if (ShouldWarmWave(warmupWave))
				{
					list.Add(new EffectWarmupPlan(item, poolKey, warmupWave));
				}
			}
		}
		list.Sort(CompareEffectWarmupPlanPriority);
		_effectWarmupPlans = list.ToArray();
		return _effectWarmupPlans;
	}

	private EffectWarmupItem[] GetEffectWarmupItems()
	{
		if (_effectWarmupItems != null)
		{
			return _effectWarmupItems;
		}
		List<EffectWarmupItem> list = new List<EffectWarmupItem>();
		int num = _host.CountEffectTargets();
		Volatile.Write(ref _effectWarmupTargetCount, num);
		EffectWarmupPlan[] effectWarmupPlans = GetEffectWarmupPlans();
		foreach (EffectWarmupPlan effectWarmupPlan in effectWarmupPlans)
		{
			CombatPresentationEffectCue cue = effectWarmupPlan.Cue;
			int effectWarmupTargetCount = GetEffectWarmupTargetCount(cue, num);
			int num2 = Math.Clamp(_host.GetUsablePooledCount(effectWarmupPlan.PoolKey), 0, effectWarmupTargetCount);
			if (num2 < effectWarmupTargetCount)
			{
				EffectWarmupWave wave = effectWarmupPlan.Wave;
				int num3 = ((wave == EffectWarmupWave.Starter) ? Math.Min(effectWarmupTargetCount, GetStarterEffectWarmupTargetCount(cue, num)) : 0);
				int num4 = ((_policy.WarmFollowUpCombatEffects && wave <= EffectWarmupWave.FollowUp) ? Math.Min(effectWarmupTargetCount, Math.Max(num3, GetFollowUpEffectWarmupTargetCount(cue, num))) : num3);
				int num5 = (_policy.WarmDeferredCombatEffects ? effectWarmupTargetCount : num4);
				for (int j = num2; j < num3; j++)
				{
					list.Add(new EffectWarmupItem(cue, EffectWarmupWave.Starter, effectWarmupPlan.PoolKey));
				}
				for (int k = Math.Max(num2, num3); k < num4; k++)
				{
					list.Add(new EffectWarmupItem(cue, EffectWarmupWave.FollowUp, effectWarmupPlan.PoolKey));
				}
				for (int l = Math.Max(num2, num4); l < num5; l++)
				{
					list.Add(new EffectWarmupItem(cue, EffectWarmupWave.Deferred, effectWarmupPlan.PoolKey));
				}
			}
		}
		list.Sort(CompareEffectWarmupPriority);
		_effectWarmupItems = list.ToArray();
		return _effectWarmupItems;
	}

	private int GetEffectWarmupTargetCount(CombatPresentationEffectCue cue)
	{
		return GetEffectWarmupTargetCount(cue, Volatile.Read(in _effectWarmupTargetCount));
	}

	private int GetEffectWarmupTargetCount(CombatPresentationEffectCue cue, int targetCount)
	{
		int effectPoolCap = GetEffectPoolCap(cue, targetCount);
		if (IsTargetWarmupCue(cue) && IsAttackWarmupCue(cue))
		{
			return Math.Clamp(Math.Max(effectPoolCap, _policy.AttackTransientTargetWarmupMinPoolSize), _policy.TransientEffectPoolSizePerKey, _policy.TransientTargetEffectPoolSizePerKey);
		}
		return effectPoolCap;
	}

	private int GetEffectPoolCap(CombatPresentationEffectCue cue, int targetCount)
	{
		if (!IsTargetWarmupCue(cue))
		{
			return _policy.TransientEffectPoolSizePerKey;
		}
		return Math.Clamp(Math.Max(_policy.TransientEffectPoolSizePerKey, targetCount), _policy.TransientEffectPoolSizePerKey, _policy.TransientTargetEffectPoolSizePerKey);
	}

	private int GetStarterEffectWarmupTargetCount(CombatPresentationEffectCue cue, int targetCount)
	{
		return Math.Min(IsTargetWarmupCue(cue) ? Math.Max(1, Math.Min(_policy.StarterTransientTargetEffectPoolSizePerKey, Math.Max(1, targetCount))) : Math.Max(1, _policy.StarterTransientEffectPoolSizePerKey), GetEffectWarmupTargetCount(cue, targetCount));
	}

	private int GetFollowUpEffectWarmupTargetCount(CombatPresentationEffectCue cue, int targetCount)
	{
		return Math.Min(IsTargetWarmupCue(cue) ? Math.Max(1, Math.Min(_policy.FollowUpTransientTargetEffectPoolSizePerKey, Math.Max(1, targetCount))) : Math.Max(1, _policy.FollowUpTransientEffectPoolSizePerKey), GetEffectWarmupTargetCount(cue, targetCount));
	}

	private static int CompareEffectWarmupPriority(EffectWarmupItem left, EffectWarmupItem right)
	{
		return CompareEffectWarmupPriority(left.Wave, left.Cue, right.Wave, right.Cue);
	}

	private static int CompareEffectWarmupPlanPriority(EffectWarmupPlan left, EffectWarmupPlan right)
	{
		return CompareEffectWarmupPriority(left.Wave, left.Cue, right.Wave, right.Cue);
	}

	private static int CompareEffectWarmupPriority(EffectWarmupWave leftWave, CombatPresentationEffectCue leftCue, EffectWarmupWave rightWave, CombatPresentationEffectCue rightCue)
	{
		int num = GetEffectWarmupPriority(leftWave, leftCue).CompareTo(GetEffectWarmupPriority(rightWave, rightCue));
		if (num != 0)
		{
			return num;
		}
		return string.Compare(leftCue.Source.ScenePath, rightCue.Source.ScenePath, StringComparison.OrdinalIgnoreCase);
	}

	private static int GetEffectWarmupPriority(EffectWarmupWave wave, CombatPresentationEffectCue cue)
	{
		return wave switch
		{
			EffectWarmupWave.Starter => 0, 
			EffectWarmupWave.FollowUp => GetEffectWarmupPriority(cue) + 10, 
			_ => GetEffectWarmupPriority(cue) + 20, 
		};
	}

	private static int GetEffectWarmupPriority(CombatPresentationEffectCue cue)
	{
		bool flag = IsAttackWarmupCue(cue);
		if (flag && IsTargetWarmupCue(cue))
		{
			return 0;
		}
		if (flag)
		{
			return 1;
		}
		if (IsTargetWarmupCue(cue))
		{
			return 2;
		}
		return 3;
	}

	private static bool IsTargetWarmupCue(CombatPresentationEffectCue cue)
	{
		CombatPresentationEffectAnchor anchor = cue.Anchor;
		if (anchor == CombatPresentationEffectAnchor.Target || (uint)(anchor - 4) <= 3u)
		{
			return true;
		}
		return false;
	}

	private static bool IsAttackWarmupCue(CombatPresentationEffectCue cue)
	{
		if (cue.GroupName == null || !cue.GroupName.Contains("attack", StringComparison.OrdinalIgnoreCase))
		{
			if (cue.Source.ScenePath != null)
			{
				return cue.Source.ScenePath.Contains("attack", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		return true;
	}

	private static bool ShouldSkipWarmup(CombatPresentationEffectCue cue)
	{
		return cue.WarmupTier == CombatPresentationEffectWarmupTier.None;
	}

	private static bool TryGetExplicitWarmupWave(CombatPresentationEffectCue cue, out EffectWarmupWave wave)
	{
		switch (cue.WarmupTier)
		{
		case CombatPresentationEffectWarmupTier.Starter:
			wave = EffectWarmupWave.Starter;
			return true;
		case CombatPresentationEffectWarmupTier.FollowUp:
			wave = EffectWarmupWave.FollowUp;
			return true;
		case CombatPresentationEffectWarmupTier.Deferred:
			wave = EffectWarmupWave.Deferred;
			return true;
		default:
			wave = EffectWarmupWave.Deferred;
			return false;
		}
	}

	private EffectWarmupWave GetWarmupWave(CombatPresentationEffectCue cue)
	{
		if (TryGetExplicitWarmupWave(cue, out var wave))
		{
			return wave;
		}
		if (MatchesWarmupToken(cue.GroupName, _policy.StarterCombatWarmupTokens) || MatchesWarmupToken(cue.Source.ScenePath, _policy.StarterCombatWarmupTokens))
		{
			return EffectWarmupWave.Starter;
		}
		if (MatchesWarmupToken(cue.GroupName, _policy.FollowUpCombatWarmupTokens) || MatchesWarmupToken(cue.Source.ScenePath, _policy.FollowUpCombatWarmupTokens))
		{
			return EffectWarmupWave.FollowUp;
		}
		return EffectWarmupWave.Deferred;
	}

	private EffectWarmupWave GetSceneCacheWarmupWave(CombatPresentationEffectCue cue)
	{
		if (TryGetExplicitWarmupWave(cue, out var wave))
		{
			return wave;
		}
		if (MatchesWarmupToken(cue.GroupName, _policy.SceneCacheStarterCombatWarmupTokens) || MatchesWarmupToken(cue.Source.ScenePath, _policy.SceneCacheStarterCombatWarmupTokens))
		{
			return EffectWarmupWave.Starter;
		}
		if (MatchesWarmupToken(cue.GroupName, _policy.SceneCacheFollowUpCombatWarmupTokens) || MatchesWarmupToken(cue.Source.ScenePath, _policy.SceneCacheFollowUpCombatWarmupTokens))
		{
			return EffectWarmupWave.FollowUp;
		}
		return GetWarmupWave(cue);
	}

	private static bool MatchesWarmupToken(string? value, IReadOnlyList<string> tokens)
	{
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		foreach (string token in tokens)
		{
			if (!string.IsNullOrWhiteSpace(token) && value.Contains(token, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private bool ShouldWarmWave(EffectWarmupWave wave)
	{
		return wave switch
		{
			EffectWarmupWave.FollowUp => _policy.WarmFollowUpCombatEffects, 
			EffectWarmupWave.Deferred => _policy.WarmDeferredCombatEffects, 
			_ => true, 
		};
	}

	private void WarmEffectPoolStep(int token, int index)
	{
		if (token != Volatile.Read(in _effectWarmupToken))
		{
			return;
		}
		if (!_host.AreEffectsEnabled())
		{
			ClearWarmupRunStats(ref _effectWarmupStats, token);
			return;
		}
		EffectWarmupItem[] effectWarmupItems = GetEffectWarmupItems();
		if (index >= effectWarmupItems.Length)
		{
			CompleteWarmupRunStats(ref _effectWarmupStats, token);
			return;
		}
		if (_host.ShouldDeferWarmup())
		{
			RecordWarmupBusyRetry(_effectWarmupStats, token);
			ScheduleEffectWarmupStep(token, index, _policy.CombatWarmupBusyRetryDelaySeconds);
			return;
		}
		long startTimestamp = _timingProbe.StartTimingProbe();
		int num = index;
		bool flag = false;
		try
		{
			EffectWarmupWave wave = effectWarmupItems[index].Wave;
			int num2 = (IsAttackWarmupCue(effectWarmupItems[index].Cue) ? _policy.AttackTransientWarmupBatchSize : _policy.NonAttackTransientWarmupBatchSize);
			for (int i = 0; i < num2; i++)
			{
				if (num >= effectWarmupItems.Length)
				{
					break;
				}
				if (!WarmEffectPoolCue(effectWarmupItems[num]))
				{
					flag = true;
					RecordWarmupResourceWait(_effectWarmupStats, token);
					break;
				}
				num++;
				if ((num < effectWarmupItems.Length && effectWarmupItems[num].Wave != wave) || (wave != EffectWarmupWave.Starter && num < effectWarmupItems.Length && !IsAttackWarmupCue(effectWarmupItems[num].Cue)))
				{
					break;
				}
			}
		}
		finally
		{
			_timingProbe.LogTimingProbe("warmup_effect_step", startTimestamp);
		}
		ScheduleEffectWarmupStep(token, num, flag ? _policy.CombatWarmupBusyRetryDelaySeconds : GetEffectWarmupStepDelay(effectWarmupItems, num));
	}

	private float GetEffectWarmupStepDelay(EffectWarmupItem[] items, int nextIndex)
	{
		if (nextIndex >= items.Length)
		{
			return _policy.CombatWarmupStepDelaySeconds;
		}
		bool flag = IsAttackWarmupCue(items[nextIndex].Cue);
		if (items[nextIndex].Wave == EffectWarmupWave.Starter)
		{
			if (!flag)
			{
				return Math.Max(_policy.CombatWarmupStepDelaySeconds, _policy.NonAttackCombatWarmupStepDelaySeconds);
			}
			return _policy.CombatWarmupStepDelaySeconds;
		}
		if (items[nextIndex].Wave == EffectWarmupWave.FollowUp)
		{
			if (nextIndex <= 0 || items[nextIndex - 1].Wave != EffectWarmupWave.Starter)
			{
				if (!flag)
				{
					return Math.Max(_policy.FollowUpCombatWarmupStepDelaySeconds, _policy.NonAttackCombatWarmupStepDelaySeconds);
				}
				return _policy.FollowUpCombatWarmupStepDelaySeconds;
			}
			return _policy.FollowUpCombatWarmupInitialDelaySeconds;
		}
		if (nextIndex <= 0 || items[nextIndex - 1].Wave == EffectWarmupWave.Deferred)
		{
			if (!flag)
			{
				return Math.Max(_policy.DeferredCombatWarmupStepDelaySeconds, _policy.NonAttackCombatWarmupStepDelaySeconds);
			}
			return _policy.DeferredCombatWarmupStepDelaySeconds;
		}
		return _policy.DeferredCombatWarmupInitialDelaySeconds;
	}

	private void ScheduleEffectWarmupStep(int token, int index, float delaySeconds)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, delaySeconds), () =>
			{
				WarmEffectPoolStep(token, index);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat transition effects warmup timer failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private void WarmSceneCacheStep(int token, int index)
	{
		if (token != Volatile.Read(in _sceneCacheWarmupToken))
		{
			return;
		}
		if (!_host.AreEffectsEnabled())
		{
			ClearWarmupRunStats(ref _sceneCacheWarmupStats, token);
			return;
		}
		EffectWarmupItem[] sceneCacheWarmupItems = GetSceneCacheWarmupItems();
		if (index >= sceneCacheWarmupItems.Length)
		{
			CompleteWarmupRunStats(ref _sceneCacheWarmupStats, token);
			return;
		}
		if (!_shouldWarmupCue(sceneCacheWarmupItems[index].Cue))
		{
			ScheduleSceneCacheWarmupStep(token, index + 1, GetSceneCacheWarmupStepDelay(sceneCacheWarmupItems, index, index + 1));
			return;
		}
		if (_host.ShouldDeferWarmup())
		{
			RecordWarmupBusyRetry(_sceneCacheWarmupStats, token);
			ScheduleSceneCacheWarmupStep(token, index, _policy.CombatWarmupBusyRetryDelaySeconds);
			return;
		}
		try
		{
			string scenePath = sceneCacheWarmupItems[index].Cue.Source.ScenePath;
			if (!string.IsNullOrEmpty(scenePath) && !_host.PreloadScene(scenePath, allowSynchronousFallback: false))
			{
				RecordWarmupResourceWait(_sceneCacheWarmupStats, token);
				ScheduleSceneCacheWarmupStep(token, index, _policy.SceneCacheWarmupStepDelaySeconds);
				return;
			}
		}
		catch (Exception ex)
		{
			_warn("Combat transition scene cache preload failed: " + SkinLog.FormatException(ex), 2);
		}
		ScheduleSceneCacheWarmupStep(token, index + 1, GetSceneCacheWarmupStepDelay(sceneCacheWarmupItems, index, index + 1));
	}

	private float GetSceneCacheWarmupStepDelay(EffectWarmupItem[] items, int currentIndex, int nextIndex)
	{
		if (nextIndex >= items.Length)
		{
			return _policy.SceneCacheWarmupStepDelaySeconds;
		}
		EffectWarmupWave wave = items[nextIndex].Wave;
		EffectWarmupWave effectWarmupWave = ((currentIndex >= 0 && currentIndex < items.Length) ? items[currentIndex].Wave : EffectWarmupWave.Starter);
		if (wave == effectWarmupWave)
		{
			return _policy.SceneCacheWarmupStepDelaySeconds;
		}
		if (wave != EffectWarmupWave.FollowUp)
		{
			return _policy.DeferredCombatWarmupInitialDelaySeconds;
		}
		return _policy.FollowUpCombatWarmupInitialDelaySeconds;
	}

	private void ScheduleSceneCacheWarmupStep(int token, int index, float delaySeconds)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, delaySeconds), () =>
			{
				WarmSceneCacheStep(token, index);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat transition scene cache preload timer failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private bool WarmEffectPoolCue(EffectWarmupItem item)
	{
		try
		{
			CombatPresentationEffectCue cue = item.Cue;
			if (!_shouldWarmupCue(cue))
			{
				return true;
			}
			if (cue.Source.Kind != CombatPresentationEffectSourceKind.Scene || string.IsNullOrEmpty(cue.Source.ScenePath))
			{
				return true;
			}
			string poolKey = item.PoolKey ?? _host.GetPoolKey(cue);
			if (_host.GetUsablePooledCount(poolKey) >= GetEffectWarmupTargetCount(cue))
			{
				return true;
			}
			if (!_host.PreloadScene(cue.Source.ScenePath, allowSynchronousFallback: false))
			{
				return false;
			}
			PackedScene packedScene = _host.LoadScene(cue.Source.ScenePath);
			if (packedScene == null)
			{
				return true;
			}
			Node2D node2D = _host.AcquireRoot(cue, packedScene, warmup: false);
			if (node2D != null)
			{
				if (_host.PrimeRoot(node2D, cue))
				{
					ScheduleWarmupRelease(node2D);
				}
				else
				{
					_host.ReleaseRoot(node2D);
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			_warn("Combat transition effects warmup failed: " + SkinLog.FormatException(ex), 2);
			return true;
		}
	}

	private void ScheduleWarmupRelease(Node2D effectRoot)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.02f, _policy.PipelineWarmupHoldSeconds), () =>
			{
				_host.ReleaseRoot(effectRoot);
			});
		}
		catch
		{
			_host.ReleaseRoot(effectRoot);
		}
	}

	private string[] GetSfxPrewarmPaths()
	{
		if (_sfxPrewarmPaths != null)
		{
			return _sfxPrewarmPaths;
		}
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CombatPresentationSfxCue item in _profile.EnumerateSfxCues())
		{
			if (!string.IsNullOrWhiteSpace(item.Path) && (_policy.PrewarmAllCombatSfx || MatchesWarmupToken(item.Path, _policy.CombatSfxWarmupTokens)) && hashSet.Add(item.Path))
			{
				list.Add(item.Path);
			}
		}
		list.Sort(CompareSfxPrewarmPriority);
		_sfxPrewarmPaths = list.ToArray();
		return _sfxPrewarmPaths;
	}

	private int CompareSfxPrewarmPriority(string left, string right)
	{
		int num = GetSfxPrewarmPriority(left).CompareTo(GetSfxPrewarmPriority(right));
		if (num == 0)
		{
			return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
		}
		return num;
	}

	private int GetSfxPrewarmPriority(string path)
	{
		if (MatchesWarmupToken(path, _policy.CombatSfxWarmupTokens))
		{
			return 0;
		}
		if (MatchesWarmupToken(path, _policy.StarterCombatWarmupTokens))
		{
			return 0;
		}
		if (MatchesWarmupToken(path, _policy.FollowUpCombatWarmupTokens))
		{
			return 1;
		}
		if (!path.Contains("attack", StringComparison.OrdinalIgnoreCase))
		{
			return 3;
		}
		return 2;
	}

	private void WarmSfxStep(int token, int index)
	{
		if (token != Volatile.Read(in _sfxPrewarmToken))
		{
			return;
		}
		if (!_host.AreEffectsEnabled() || !_host.IsSfxEnabled())
		{
			ClearWarmupRunStats(ref _sfxPrewarmStats, token);
			return;
		}
		string[] sfxPrewarmPaths = GetSfxPrewarmPaths();
		if (index >= sfxPrewarmPaths.Length)
		{
			CompleteWarmupRunStats(ref _sfxPrewarmStats, token);
			return;
		}
		if (_host.ShouldDeferWarmup())
		{
			RecordWarmupBusyRetry(_sfxPrewarmStats, token);
			ScheduleSfxWarmupStep(token, index, _policy.CombatWarmupBusyRetryDelaySeconds);
			return;
		}
		long startTimestamp = _timingProbe.StartTimingProbe();
		bool flag = true;
		try
		{
			flag = _host.PrewarmSfx(sfxPrewarmPaths[index]);
		}
		finally
		{
			_timingProbe.LogTimingProbe("warmup_sfx_step:" + sfxPrewarmPaths[index], startTimestamp);
		}
		if (!flag)
		{
			RecordWarmupResourceWait(_sfxPrewarmStats, token);
			ScheduleSfxWarmupStep(token, index, _policy.CombatWarmupBusyRetryDelaySeconds);
		}
		else
		{
			ScheduleSfxWarmupStep(token, index + 1, GetSfxWarmupStepDelay(sfxPrewarmPaths, index, index + 1));
		}
	}

	private float GetSfxWarmupStepDelay(string[] paths, int currentIndex, int nextIndex)
	{
		if (nextIndex >= paths.Length)
		{
			return _policy.CombatWarmupStepDelaySeconds;
		}
		int sfxPrewarmPriority = GetSfxPrewarmPriority(paths[nextIndex]);
		if (sfxPrewarmPriority == 0)
		{
			return _policy.CombatWarmupStepDelaySeconds;
		}
		int num = ((currentIndex >= 0 && currentIndex < paths.Length) ? GetSfxPrewarmPriority(paths[currentIndex]) : (-1));
		if (sfxPrewarmPriority == 1)
		{
			if (num != 0)
			{
				return _policy.FollowUpCombatWarmupStepDelaySeconds;
			}
			return _policy.FollowUpCombatWarmupInitialDelaySeconds;
		}
		if (num < 0 || num >= 2)
		{
			return _policy.DeferredCombatWarmupStepDelaySeconds;
		}
		return _policy.DeferredCombatWarmupInitialDelaySeconds;
	}

	private void ScheduleSfxWarmupStep(int token, int index, float delaySeconds)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, delaySeconds), () =>
			{
				WarmSfxStep(token, index);
			});
		}
		catch (Exception ex)
		{
			_warn("Combat SFX warmup timer failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private WarmupRunStats? CreateWarmupRunStats(string kind, int token, int totalItems, Func<string> detailsFactory)
	{
		if (_timingProbe.StartTimingProbe() == 0L)
		{
			return null;
		}
		WarmupRunStats stats = new WarmupRunStats(kind, token, totalItems, detailsFactory());
		SkinLog.Debug(() => stats.FormatStart());
		return stats;
	}

	private static void RecordWarmupBusyRetry(WarmupRunStats? stats, int token)
	{
		if (stats != null && stats.Token == token)
		{
			stats.RecordBusyRetry();
		}
	}

	private static void RecordWarmupResourceWait(WarmupRunStats? stats, int token)
	{
		if (stats != null && stats.Token == token)
		{
			stats.RecordResourceWait();
		}
	}

	private static void ClearWarmupRunStats(ref WarmupRunStats? stats, int token)
	{
		if (stats != null && stats.Token == token)
		{
			stats = null;
		}
	}

	private static void CompleteWarmupRunStats(ref WarmupRunStats? stats, int token)
	{
		if (stats != null && stats.Token == token)
		{
			WarmupRunStats completedStats = stats;
			stats = null;
			SkinLog.Debug(() => completedStats.FormatComplete());
		}
	}

	private static string DescribeWarmupWaves(IReadOnlyList<EffectWarmupItem> items)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (EffectWarmupItem item in items)
		{
			switch (item.Wave)
			{
			case EffectWarmupWave.Starter:
				num++;
				break;
			case EffectWarmupWave.FollowUp:
				num2++;
				break;
			default:
				num3++;
				break;
			}
		}
		return "starter=" + num + " follow_up=" + num2 + " deferred=" + num3;
	}

	private string DescribeSfxPrewarmPriorities(IReadOnlyList<string> paths)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (string path in paths)
		{
			switch (GetSfxPrewarmPriority(path))
			{
			case 0:
				num++;
				break;
			case 1:
				num2++;
				break;
			case 2:
				num3++;
				break;
			default:
				num4++;
				break;
			}
		}
		return "starter=" + num + " follow_up=" + num2 + " attack=" + num3 + " deferred=" + num4;
	}
}
