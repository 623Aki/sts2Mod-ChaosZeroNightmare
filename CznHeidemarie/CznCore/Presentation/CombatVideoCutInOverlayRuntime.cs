using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class CombatVideoCutInOverlayRuntime<TActor> where TActor : class
{
	private readonly IReadOnlyDictionary<string, CombatVideoCutInProfile> _profiles;

	private readonly Func<bool> _isEnabled;

	private readonly Func<TActor?, bool> _isEnabledFor;

	private readonly Func<TActor?, int, bool> _isTokenCurrent;

	private readonly Func<string, string> _runtimeNodeName;

	private readonly Action<string, int> _warn;

	private readonly Action<string, int>? _info;

	private readonly Action<TActor?, int, float, Action> _scheduleDelay;

	private readonly Func<Viewport?> _getViewport;

	private readonly string _logPrefix;

	private readonly Dictionary<string, VideoStreamTheora> _videoCache = new Dictionary<string, VideoStreamTheora>(StringComparer.OrdinalIgnoreCase);

	private CanvasLayer? _layer;

	private ColorRect? _dim;

	private VideoStreamPlayer? _video;

	private Tween? _introTween;

	private Tween? _outroTween;

	private int _generation;

	public CombatVideoCutInOverlayRuntime(IReadOnlyDictionary<string, CombatVideoCutInProfile> profiles, ICombatVideoCutInOverlayHost<TActor> host, Action<TActor?, int, float, Action>? scheduleDelay = null, Func<Viewport?>? getViewport = null, string logPrefix = "Combat video cut-in")
	{
		_profiles = profiles ?? throw new ArgumentNullException("profiles");
		ArgumentNullException.ThrowIfNull(host, "host");
		_isEnabled = host.IsEnabled;
		_isEnabledFor = host.IsEnabledFor;
		_isTokenCurrent = host.IsTokenCurrent;
		_runtimeNodeName = host.RuntimeNodeName;
		_warn = host.Warn;
		_info = host.Info;
		_scheduleDelay = scheduleDelay ?? new Action<TActor, int, float, Action>(ScheduleDefaultDelay);
		_getViewport = getViewport ?? new Func<Viewport>(ResolveDefaultOverlayViewport);
		_logPrefix = (string.IsNullOrWhiteSpace(logPrefix) ? "Combat video cut-in" : logPrefix);
	}

	public bool HasProfile(string? stateId)
	{
		CombatVideoCutInProfile profile;
		return TryGetProfile(stateId, out profile);
	}

	public void Preload(string? stateId)
	{
		try
		{
			if (_isEnabled() && TryGetProfile(stateId, out CombatVideoCutInProfile profile))
			{
				TryGetVideoStream(profile.VideoPath, out VideoStreamTheora _);
			}
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " preload failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void TryPlay(TActor? actor, string? stateId, int token)
	{
		try
		{
			if (!_isEnabled() || !_isEnabledFor(actor) || !TryGetProfile(stateId, out CombatVideoCutInProfile profile) || !_isTokenCurrent(actor, token))
			{
				return;
			}
			if (profile.StartDelaySeconds > 0f)
			{
				_scheduleDelay(actor, token, profile.StartDelaySeconds, () =>
				{
					TryPlayNow(actor, profile, token);
				});
				_info?.Invoke(_logPrefix + " scheduled state=" + profile.StateId + " delay=" + profile.StartDelaySeconds.ToString("0.###"), 2);
			}
			else
			{
				TryPlayNow(actor, profile, token);
			}
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void Cleanup(bool releaseNodes)
	{
		try
		{
			if (_layer == null || !GodotObject.IsInstanceValid(_layer))
			{
				ResetNodeCache();
			}
			else if (releaseNodes)
			{
				_generation++;
				KillTween(ref _introTween);
				KillTween(ref _outroTween);
				if (GodotObject.IsInstanceValid(_video))
				{
					_video.Stop();
				}
				_layer.QueueFree();
				ResetNodeCache();
			}
			else
			{
				Release(_layer);
			}
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " cleanup failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void ClearVideoCache()
	{
		_videoCache.Clear();
	}

	private static void ScheduleDefaultDelay(TActor? actor, int token, float seconds, Action action)
	{
		GodotScheduler.Delay(Math.Max(0.05f, seconds), action);
	}

	private static Viewport? ResolveDefaultOverlayViewport()
	{
		if (Engine.GetMainLoop() is SceneTree sceneTree && GodotObject.IsInstanceValid(sceneTree.Root))
		{
			return sceneTree.Root;
		}
		Viewport viewport = NCombatRoom.Instance?.GetViewport();
		if (viewport == null || !GodotObject.IsInstanceValid(viewport))
		{
			return null;
		}
		return viewport;
	}

	private bool TryGetProfile(string? stateId, out CombatVideoCutInProfile profile)
	{
		if (!string.IsNullOrWhiteSpace(stateId) && _profiles.TryGetValue(stateId, out profile))
		{
			return true;
		}
		profile = null;
		return false;
	}

	private void TryPlayNow(TActor? actor, CombatVideoCutInProfile profile, int token)
	{
		try
		{
			if (_isEnabled() && _isEnabledFor(actor) && _isTokenCurrent(actor, token))
			{
				Viewport viewport = _getViewport();
				VideoStreamTheora stream;
				if (viewport == null)
				{
					_warn(_logPrefix + " skipped: no viewport.", 2);
				}
				else if (TryGetVideoStream(profile.VideoPath, out stream))
				{
					PlayVideoOverlay(actor, token, viewport, stream, profile);
					_info?.Invoke(_logPrefix + " queued state=" + profile.StateId + " video=" + profile.VideoPath, 2);
				}
			}
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " playback failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private bool TryGetVideoStream(string videoPath, [NotNullWhen(true)] out VideoStreamTheora? stream)
	{
		stream = null;
		try
		{
			if (_videoCache.TryGetValue(videoPath, out VideoStreamTheora value) && GodotObject.IsInstanceValid(value))
			{
				stream = value;
				return true;
			}
			if (!global::Godot.FileAccess.FileExists(videoPath) && !ResourceLoader.Exists(videoPath))
			{
				_warn(_logPrefix + " video missing: " + videoPath, 2);
				return false;
			}
			stream = new VideoStreamTheora
			{
				File = videoPath
			};
			_videoCache[videoPath] = stream;
			return true;
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " video load failed: " + SkinLog.FormatException(ex), 2);
			return false;
		}
	}

	private void PlayVideoOverlay(TActor? actor, int token, Viewport viewport, VideoStreamTheora stream, CombatVideoCutInProfile profile)
	{
		if (_isTokenCurrent(actor, token) && TryAcquire(viewport, stream, profile, out CanvasLayer layer, out ColorRect dim, out VideoStreamPlayer video))
		{
			int overlayGeneration = ++_generation;
			if (profile.FadeInSeconds > 0f)
			{
				_introTween = layer.CreateTween();
				_introTween.SetParallel();
				_introTween.SetEase(Tween.EaseType.Out);
				_introTween.SetTrans(Tween.TransitionType.Cubic);
				_introTween.TweenProperty(dim, "color", new Color(0f, 0f, 0f, profile.DimAlpha), profile.FadeInSeconds);
				_introTween.TweenProperty(video, "modulate", Colors.White, profile.FadeInSeconds);
			}
			else
			{
				dim.Color = new Color(0f, 0f, 0f, profile.DimAlpha);
				video.Modulate = Colors.White;
			}
			video.Play();
			GodotScheduler.OneShot(layer, Math.Max(0.001f, profile.FadeOutDelaySeconds), () =>
			{
				FadeOut(actor, token, overlayGeneration, layer, dim, video, profile);
			});
			GodotScheduler.OneShot(layer, Math.Max(0.05f, profile.LifetimeSeconds + profile.FadeOutSeconds + 0.05f), () =>
			{
				Release(layer, overlayGeneration);
			});
		}
	}

	private bool TryAcquire(Viewport viewport, VideoStreamTheora stream, CombatVideoCutInProfile profile, out CanvasLayer layer, out ColorRect dim, out VideoStreamPlayer video)
	{
		if (!TryGetNodes(out layer, out dim, out video))
		{
			layer = new CanvasLayer
			{
				Name = _runtimeNodeName("CombatVideoCutInLayer"),
				Layer = profile.CanvasLayer
			};
			dim = new ColorRect
			{
				Name = _runtimeNodeName("CombatVideoCutInDim")
			};
			video = new VideoStreamPlayer
			{
				Name = _runtimeNodeName("CombatVideoCutIn")
			};
			layer.AddChild(dim, forceReadableName: false, Node.InternalMode.Disabled);
			layer.AddChild(video, forceReadableName: false, Node.InternalMode.Disabled);
			_layer = layer;
			_dim = dim;
			_video = video;
		}
		KillTween(ref _introTween);
		KillTween(ref _outroTween);
		Configure(viewport, stream, profile, layer, dim, video);
		AttachLayer(viewport, layer);
		return true;
	}

	private bool TryGetNodes(out CanvasLayer layer, out ColorRect dim, out VideoStreamPlayer video)
	{
		layer = _layer;
		dim = _dim;
		video = _video;
		if (_layer == null || _dim == null || _video == null)
		{
			return false;
		}
		layer = _layer;
		dim = _dim;
		video = _video;
		if (GodotObject.IsInstanceValid(layer) && GodotObject.IsInstanceValid(dim))
		{
			return GodotObject.IsInstanceValid(video);
		}
		return false;
	}

	private void Configure(Viewport viewport, VideoStreamTheora stream, CombatVideoCutInProfile profile, CanvasLayer layer, ColorRect dim, VideoStreamPlayer video)
	{
		Vector2 size = viewport.GetVisibleRect().Size;
		Vector2 vector = size / 2f;
		Vector2 sourceSize = profile.SourceSize;
		Vector2 vector2 = ComputeCoverSize(size, sourceSize);
		layer.Name = _runtimeNodeName("CombatVideoCutInLayer");
		layer.Layer = profile.CanvasLayer;
		layer.Visible = true;
		dim.Name = _runtimeNodeName("CombatVideoCutInDim");
		dim.Color = new Color(0f, 0f, 0f, 0f);
		dim.Size = size;
		dim.MouseFilter = Control.MouseFilterEnum.Ignore;
		dim.ZIndex = 0;
		dim.ZAsRelative = false;
		dim.Visible = true;
		video.Stop();
		video.Name = _runtimeNodeName("CombatVideoCutIn");
		video.Stream = stream;
		video.Expand = true;
		video.Loop = false;
		video.Autoplay = false;
		video.MouseFilter = Control.MouseFilterEnum.Ignore;
		video.Position = vector - vector2 / 2f;
		video.Size = vector2;
		video.Modulate = new Color(1f, 1f, 1f, 0f);
		video.ZIndex = 10;
		video.ZAsRelative = false;
		video.VolumeDb = -80f;
		video.Visible = true;
	}

	private static Vector2 ComputeCoverSize(Vector2 containerSize, Vector2 contentSize)
	{
		if (contentSize.X <= 0f || contentSize.Y <= 0f)
		{
			return containerSize;
		}
		float num = MathF.Max(containerSize.X / contentSize.X, containerSize.Y / contentSize.Y);
		return contentSize * num;
	}

	private void FadeOut(TActor? actor, int token, int overlayGeneration, CanvasLayer layer, ColorRect dim, VideoStreamPlayer video, CombatVideoCutInProfile profile)
	{
		if (!GodotObject.IsInstanceValid(layer))
		{
			return;
		}
		if (!_isTokenCurrent(actor, token))
		{
			Release(layer, overlayGeneration);
		}
		else if (overlayGeneration == _generation)
		{
			KillTween(ref _introTween);
			KillTween(ref _outroTween);
			if (profile.FadeOutSeconds > 0f)
			{
				_outroTween = layer.CreateTween();
				_outroTween.SetParallel();
				_outroTween.SetEase(Tween.EaseType.In);
				_outroTween.SetTrans(Tween.TransitionType.Sine);
				_outroTween.TweenProperty(dim, "color", new Color(0f, 0f, 0f, 0f), profile.FadeOutSeconds);
				_outroTween.TweenProperty(video, "modulate", new Color(1f, 1f, 1f, 0f), profile.FadeOutSeconds);
			}
			else
			{
				dim.Color = new Color(0f, 0f, 0f, 0f);
				video.Modulate = new Color(1f, 1f, 1f, 0f);
			}
		}
	}

	private static void AttachLayer(Viewport viewport, CanvasLayer layer)
	{
		layer.GetParent()?.RemoveChild(layer);
		viewport.AddChild(layer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static void KillTween(ref Tween? tween)
	{
		try
		{
			if (tween != null && GodotObject.IsInstanceValid(tween))
			{
				tween.Kill();
			}
		}
		catch
		{
		}
		tween = null;
	}

	private void Release(CanvasLayer layer, int? expectedGeneration = null)
	{
		if (GodotObject.IsInstanceValid(layer) && (!expectedGeneration.HasValue || expectedGeneration.Value == _generation))
		{
			_generation++;
			KillTween(ref _introTween);
			KillTween(ref _outroTween);
			if (GodotObject.IsInstanceValid(_video))
			{
				_video.Stop();
				_video.Stream = null;
				_video.Modulate = new Color(1f, 1f, 1f, 0f);
			}
			if (GodotObject.IsInstanceValid(_dim))
			{
				_dim.Color = new Color(0f, 0f, 0f, 0f);
			}
			layer.Visible = false;
			layer.GetParent()?.RemoveChild(layer);
		}
	}

	private void ResetNodeCache()
	{
		_layer = null;
		_dim = null;
		_video = null;
		_introTween = null;
		_outroTween = null;
	}
}
