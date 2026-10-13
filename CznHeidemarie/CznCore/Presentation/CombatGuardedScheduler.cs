using System;
using CznCore.Godot;

namespace CznCore.Presentation;

internal sealed class CombatGuardedScheduler<TActor> where TActor : class
{
	private const float DefaultMinDelaySeconds = 0.05f;

	private readonly Func<TActor?, int, bool> _isTokenCurrent;

	private readonly Func<TActor?, bool>? _isActorValid;

	private readonly Action<string, int> _warn;

	public CombatGuardedScheduler(Func<TActor?, int, bool> isTokenCurrent, Func<TActor?, bool>? isActorValid, Action<string, int> warn)
	{
		_isTokenCurrent = isTokenCurrent ?? throw new ArgumentNullException("isTokenCurrent");
		_isActorValid = isActorValid;
		_warn = warn ?? throw new ArgumentNullException("warn");
	}

	public bool IsCurrent(TActor? actor, int token)
	{
		if (_isTokenCurrent(actor, token))
		{
			if (_isActorValid != null)
			{
				return _isActorValid(actor);
			}
			return true;
		}
		return false;
	}

	public void Deferred(TActor? actor, int token, Action action, string failureMessage, Func<bool>? extraGuard = null)
	{
		if (!IsCurrent(actor, token))
		{
			return;
		}
		try
		{
			GodotScheduler.Deferred(() =>
			{
				RunIfCurrent(actor, token, action, failureMessage, extraGuard);
			});
		}
		catch (Exception ex)
		{
			_warn(failureMessage + SkinLog.FormatException(ex), 2);
		}
	}

	public void Delay(TActor? actor, int token, float seconds, Action action, string failureMessage, Func<bool>? extraGuard = null, Action? onScheduleFailed = null, float minDelaySeconds = 0.05f)
	{
		if (!IsCurrent(actor, token))
		{
			return;
		}
		try
		{
			GodotScheduler.Delay(Math.Max(minDelaySeconds, seconds), () =>
			{
				RunIfCurrent(actor, token, action, failureMessage, extraGuard);
			});
		}
		catch (Exception ex)
		{
			_warn(failureMessage + SkinLog.FormatException(ex), 2);
			onScheduleFailed?.Invoke();
		}
	}

	private void RunIfCurrent(TActor? actor, int token, Action action, string failureMessage, Func<bool>? extraGuard)
	{
		try
		{
			if (IsCurrent(actor, token) && (extraGuard == null || extraGuard()))
			{
				action();
			}
		}
		catch (Exception ex)
		{
			_warn(failureMessage + SkinLog.FormatException(ex), 2);
		}
	}
}
