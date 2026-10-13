using System;
using System.Collections.Generic;
using System.Threading;
using CznCore.Godot;

namespace CznCore.Presentation;

internal sealed class AudioPrewarmRuntime
{
	private const float MinDelaySeconds = 0.01f;

	private readonly Func<IReadOnlyList<string>> _getPaths;

	private readonly float _initialDelaySeconds;

	private readonly float _stepDelaySeconds;

	private readonly Action<string> _prewarm;

	private readonly Action<string, int> _warn;

	private readonly string _scheduleFailureMessage;

	private int _token;

	private int _scheduled;

	public AudioPrewarmRuntime(Func<IReadOnlyList<string>> getPaths, float initialDelaySeconds, float stepDelaySeconds, Action<string> prewarm, Action<string, int> warn, string scheduleFailureMessage)
	{
		_getPaths = getPaths ?? throw new ArgumentNullException("getPaths");
		_initialDelaySeconds = initialDelaySeconds;
		_stepDelaySeconds = stepDelaySeconds;
		_prewarm = prewarm ?? throw new ArgumentNullException("prewarm");
		_warn = warn ?? throw new ArgumentNullException("warn");
		_scheduleFailureMessage = scheduleFailureMessage ?? throw new ArgumentNullException("scheduleFailureMessage");
	}

	public void ScheduleOnce()
	{
		if (_getPaths().Count != 0 && Interlocked.Exchange(ref _scheduled, 1) != 1)
		{
			int token = Interlocked.Increment(ref _token);
			ScheduleStep(token, 0, _initialDelaySeconds);
		}
	}

	public void Stop(bool resetSchedule = false)
	{
		Interlocked.Increment(ref _token);
		if (resetSchedule)
		{
			Volatile.Write(ref _scheduled, 0);
		}
	}

	private void ScheduleStep(int token, int index, float delaySeconds)
	{
		try
		{
			GodotScheduler.Delay(Math.Max(0.01f, delaySeconds), () =>
			{
				PrewarmStep(token, index);
			});
		}
		catch (Exception ex)
		{
			_warn(_scheduleFailureMessage + SkinLog.FormatException(ex), 2);
		}
	}

	private void PrewarmStep(int token, int index)
	{
		if (token == Volatile.Read(in _token))
		{
			IReadOnlyList<string> readOnlyList = _getPaths();
			if (index < readOnlyList.Count)
			{
				_prewarm(readOnlyList[index]);
				ScheduleStep(token, index + 1, _stepDelaySeconds);
			}
		}
	}
}
