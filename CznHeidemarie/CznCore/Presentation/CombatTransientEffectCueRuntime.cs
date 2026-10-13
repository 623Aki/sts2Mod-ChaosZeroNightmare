using System;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatTransientEffectCueRuntime<TActor> where TActor : class
{
	private const float MinDelaySeconds = 0.01f;

	private readonly Func<bool> _isEnabled;

	private readonly Func<TActor?, bool> _isEnabledFor;

	private readonly Func<TActor?, int, bool> _isTokenCurrent;

	private readonly Func<TActor?, bool> _isActorValid;

	private readonly Action<Node2D> _clearExisting;

	private readonly Func<string, bool, PackedScene?> _loadScene;

	private readonly Func<CombatPresentationEffectCue, PackedScene, Node2D?> _acquireRoot;

	private readonly Func<Node2D, Node2D, CombatPresentationEffectCue, int> _prepareRoot;

	private readonly Func<Node2D, int, bool> _isEffectActive;

	private readonly Func<Node2D, int, bool> _markEffectVisible;

	private readonly Action<Node2D, int?> _releaseRoot;

	private readonly Action<Node2D, CombatPresentationEffectCue> _startEffect;

	private readonly ISkinTimingProbe _timingProbe;

	private readonly Action<string, int> _warn;

	public CombatTransientEffectCueRuntime(ICombatTransientEffectCueHost<TActor> host, ISkinTimingProbe timingProbe, Action<string, int> warn)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_isEnabled = host.IsEnabled;
		_isEnabledFor = host.IsEnabledFor;
		_isTokenCurrent = host.IsTokenCurrent;
		_isActorValid = host.IsActorValid;
		_clearExisting = host.ClearExisting;
		_loadScene = host.LoadScene;
		_acquireRoot = host.AcquireRoot;
		_prepareRoot = host.PrepareRoot;
		_isEffectActive = host.IsEffectActive;
		_markEffectVisible = host.MarkEffectVisible;
		_releaseRoot = host.ReleaseRoot;
		_startEffect = host.StartEffect;
		_timingProbe = timingProbe ?? throw new ArgumentNullException("timingProbe");
		_warn = warn ?? throw new ArgumentNullException("warn");
	}

	public void Queue(TActor? actor, Node2D spine, CombatPresentationEffectCue cue, int token)
	{
		Node2D node2D = null;
		int? num = null;
		try
		{
			if (!_isEnabled() || !_isEnabledFor(actor))
			{
				return;
			}
			long timingProbe = _timingProbe.StartTimingProbe();
			if (!GodotObject.IsInstanceValid(spine))
			{
				return;
			}
			if (cue.ClearExisting)
			{
				_clearExisting(spine);
			}
			if (cue.Source.Kind != CombatPresentationEffectSourceKind.Scene || string.IsNullOrEmpty(cue.Source.ScenePath))
			{
				_warn("Unsupported combat transition effects source: " + cue.Source.Kind, 2);
				return;
			}
			PackedScene packedScene = _loadScene(cue.Source.ScenePath, cue.AllowSynchronousSceneLoad);
			if (packedScene == null)
			{
				if (!cue.AllowSynchronousSceneLoad)
				{
					LogScenePreloadPending(cue, timingProbe);
				}
				else
				{
					_warn("Missing combat transition effects scene: " + cue.Source.ScenePath, 2);
				}
				return;
			}
			node2D = _acquireRoot(cue, packedScene);
			if (node2D == null)
			{
				return;
			}
			num = _prepareRoot(node2D, spine, cue);
			spine.AddChild(node2D, forceReadableName: false, Node.InternalMode.Disabled);
			CombatPresentationGodotLayeringRuntime.PlaceEffectRoot(spine, node2D, cue);
			if (cue.StartDelaySeconds <= 0f)
			{
				if (!TryStartIfCurrent(actor, node2D, cue, token, num.Value))
				{
					_releaseRoot(node2D, num);
					node2D = null;
				}
				else
				{
					ScheduleLifecycle(node2D, num.Value, cue);
					node2D = null;
					LogQueued(cue, timingProbe);
				}
			}
			else
			{
				node2D.Visible = false;
				bool flag = ScheduleDelayedStart(actor, node2D, cue, token, num.Value);
				node2D = null;
				if (flag)
				{
					LogQueued(cue, timingProbe);
				}
			}
		}
		catch (Exception ex)
		{
			ReleaseAcquiredRootAfterFailure(node2D, num);
			_warn("Combat transition effects failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private void ReleaseAcquiredRootAfterFailure(Node2D? effectRoot, int? generation)
	{
		if (effectRoot == null || !GodotObject.IsInstanceValid(effectRoot))
		{
			return;
		}
		try
		{
			_releaseRoot(effectRoot, generation);
		}
		catch (Exception ex)
		{
			_warn("Combat transition effects failure cleanup failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private bool ScheduleDelayedStart(TActor? actor, Node2D effectRoot, CombatPresentationEffectCue cue, int token, int generation)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, cue.StartDelaySeconds), () =>
			{
				if (!TryStartIfCurrent(actor, effectRoot, cue, token, generation))
				{
					_releaseRoot(effectRoot, generation);
				}
				else
				{
					ScheduleLifecycle(effectRoot, generation, cue);
				}
			});
			return true;
		}
		catch (Exception ex)
		{
			_releaseRoot(effectRoot, generation);
			_warn("Combat transition effects scheduling failed: " + SkinLog.FormatException(ex), 2);
			return false;
		}
	}

	private void ScheduleLifecycle(Node2D effectRoot, int generation, CombatPresentationEffectCue cue)
	{
		float num = (float.IsFinite(cue.LifetimeSeconds) ? Math.Max(0f, cue.LifetimeSeconds) : 0f);
		float particleTailSeconds = (float.IsFinite(cue.ParticleTailSeconds) ? Math.Max(0f, cue.ParticleTailSeconds) : 0f);
		if (particleTailSeconds <= 0f)
		{
			ScheduleRelease(effectRoot, generation, num);
			return;
		}
		if (num <= 0f)
		{
			if (_isEffectActive(effectRoot, generation))
			{
				CombatEffectRuntime.StopParticles(effectRoot);
			}
			ScheduleRelease(effectRoot, generation, particleTailSeconds);
			return;
		}
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, num), () =>
			{
				if (_isEffectActive(effectRoot, generation))
				{
					CombatEffectRuntime.StopParticles(effectRoot);
					ScheduleRelease(effectRoot, generation, particleTailSeconds);
				}
			});
		}
		catch (Exception ex)
		{
			CombatEffectRuntime.StopParticles(effectRoot);
			_releaseRoot(effectRoot, generation);
			_warn("Combat transition particle tail scheduling failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private void ScheduleRelease(Node2D effectRoot, int generation, float lifetimeSeconds)
	{
		if (lifetimeSeconds <= 0f)
		{
			_releaseRoot(effectRoot, generation);
			return;
		}
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, lifetimeSeconds), () =>
			{
				_releaseRoot(effectRoot, generation);
			});
		}
		catch (Exception ex)
		{
			_releaseRoot(effectRoot, generation);
			_warn("Combat transition effects release scheduling failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private bool TryStartIfCurrent(TActor? actor, Node2D effectRoot, CombatPresentationEffectCue cue, int token, int generation)
	{
		if (!_isEnabled() || !_isEnabledFor(actor) || !_isActorValid(actor) || !_isTokenCurrent(actor, token) || !_isEffectActive(effectRoot, generation))
		{
			return false;
		}
		if (!_markEffectVisible(effectRoot, generation))
		{
			return false;
		}
		effectRoot.Visible = true;
		try
		{
			_startEffect(effectRoot, cue);
			return true;
		}
		catch
		{
			_releaseRoot(effectRoot, generation);
			throw;
		}
	}

	private void LogQueued(CombatPresentationEffectCue cue, long timingProbe)
	{
		_timingProbe.LogTimingProbe("effect_queue:" + (cue.GroupName ?? cue.Source.ScenePath ?? "<null>"), timingProbe);
	}

	private void LogScenePreloadPending(CombatPresentationEffectCue cue, long timingProbe)
	{
		_timingProbe.LogTimingProbe("effect_scene_preload_pending:" + (cue.GroupName ?? cue.Source.ScenePath ?? "<null>"), timingProbe);
	}
}
