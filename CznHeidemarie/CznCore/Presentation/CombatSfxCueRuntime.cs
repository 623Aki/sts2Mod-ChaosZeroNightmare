using System;
using System.Collections.Generic;
using CznCore.Godot;

namespace CznCore.Presentation;

internal sealed class CombatSfxCueRuntime<TActor> where TActor : class
{
	private const float MinDelaySeconds = 0.01f;

	private readonly Func<bool> _isEnabled;

	private readonly Func<TActor?, bool> _isEnabledFor;

	private readonly Func<TActor?, int, bool> _isTokenCurrent;

	private readonly Func<TActor?, bool>? _isActorValid;

	private readonly Action<string, float> _play;

	private readonly Action<string, int> _warn;

	private readonly string _scheduleFailureMessage;

	public CombatSfxCueRuntime(ICombatSfxCueHost<TActor> host, string scheduleFailureMessage)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_isEnabled = host.IsEnabled;
		_isEnabledFor = host.IsEnabledFor;
		_isTokenCurrent = host.IsTokenCurrent;
		_isActorValid = host.IsActorValid;
		_play = host.PlaySfx;
		_warn = host.Warn;
		_scheduleFailureMessage = scheduleFailureMessage ?? throw new ArgumentNullException("scheduleFailureMessage");
	}

	public void Play(TActor? actor, IReadOnlyList<CombatPresentationSfxCue>? cues, int token)
	{
		if (!_isEnabled() || cues == null || cues.Count == 0)
		{
			return;
		}
		foreach (CombatPresentationSfxCue cue in cues)
		{
			Queue(actor, cue, token);
		}
	}

	public void Queue(TActor? actor, CombatPresentationSfxCue cue, int token)
	{
		if (!CanPlay(actor, token))
		{
			return;
		}
		if (cue.DelaySeconds <= 0f)
		{
			_play(cue.Path, cue.AdapterGainLinear);
			return;
		}
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, cue.DelaySeconds), () =>
			{
				if (CanPlay(actor, token))
				{
					_play(cue.Path, cue.AdapterGainLinear);
				}
			});
		}
		catch (Exception ex)
		{
			_warn(_scheduleFailureMessage + SkinLog.FormatException(ex), 2);
		}
	}

	private bool CanPlay(TActor? actor, int token)
	{
		if (_isEnabled() && _isEnabledFor(actor) && _isTokenCurrent(actor, token))
		{
			if (_isActorValid != null)
			{
				return _isActorValid(actor);
			}
			return true;
		}
		return false;
	}
}
