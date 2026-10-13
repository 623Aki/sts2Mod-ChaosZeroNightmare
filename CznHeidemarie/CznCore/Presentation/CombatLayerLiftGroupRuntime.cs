using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatLayerLiftGroupRuntime
{
	private readonly string _ownerKey;

	private readonly string _label;

	private readonly int _zIndex;

	private readonly float _durationSeconds;

	private readonly string _logPrefix;

	private readonly Action<string, int> _warn;

	private readonly Action<Func<string>, int>? _debug;

	private int _token;

	private int _restoreGeneration;

	public int ActiveCount => CombatLayerLiftRuntime.ActiveCount(_ownerKey);

	public CombatLayerLiftGroupRuntime(string? ownerKey, string? label, int zIndex, float durationSeconds, string? logPrefix, Action<string, int> warn, Action<Func<string>, int>? debug = null)
	{
		_ownerKey = ownerKey ?? throw new ArgumentNullException("ownerKey");
		_label = label ?? throw new ArgumentNullException("label");
		_zIndex = zIndex;
		_durationSeconds = durationSeconds;
		_logPrefix = logPrefix ?? throw new ArgumentNullException("logPrefix");
		_warn = warn ?? throw new ArgumentNullException("warn");
		_debug = debug;
	}

	public void Begin(IEnumerable<Node2D?>? spines, int token)
	{
		try
		{
			Cleanup();
			BeginOrExtend(spines, token, "started");
		}
		catch (Exception ex)
		{
			Cleanup();
			_warn(_logPrefix + " " + _label + " failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void Ensure(IEnumerable<Node2D?>? spines, int token)
	{
		try
		{
			if (_token != 0 && _token != token)
			{
				Cleanup();
			}
			BeginOrExtend(spines, token, "extended");
		}
		catch (Exception ex)
		{
			Cleanup();
			_warn(_logPrefix + " " + _label + " ensure failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void Cleanup()
	{
		_restoreGeneration++;
		int restoredCount = Release();
		if (restoredCount > 0)
		{
			_debug?.Invoke(() => _logPrefix + " " + _label + " cleanup restored nodes=" + restoredCount, 2);
		}
	}

	private void BeginOrExtend(IEnumerable<Node2D?>? spines, int token, string action)
	{
		if (spines == null)
		{
			return;
		}
		_token = token;
		foreach (Node2D spine in spines)
		{
			CombatLayerLiftRuntime.Acquire(spine, _ownerKey, _zIndex);
		}
		int liftedCount = ActiveCount;
		if (liftedCount == 0)
		{
			_token = 0;
			return;
		}
		if (_durationSeconds > 0f)
		{
			ScheduleRestore(token);
		}
		_debug?.Invoke(() => _logPrefix + " " + _label + " " + action + " nodes=" + liftedCount + " token=" + token, 2);
	}

	private void ScheduleRestore(int token)
	{
		int restoreGeneration = ++_restoreGeneration;
		GodotScheduler.Delay(_durationSeconds, () =>
		{
			Restore(token, restoreGeneration);
		});
	}

	private void Restore(int token, int restoreGeneration)
	{
		if (_token != token || _restoreGeneration != restoreGeneration)
		{
			return;
		}
		int restoredCount = Release();
		if (restoredCount > 0)
		{
			_debug?.Invoke(() => _logPrefix + " " + _label + " restored nodes=" + restoredCount + " token=" + token, 2);
		}
	}

	private int Release()
	{
		_token = 0;
		try
		{
			return CombatLayerLiftRuntime.ReleaseAll(_ownerKey);
		}
		catch (Exception ex)
		{
			_warn(_logPrefix + " " + _label + " restore failed: " + SkinLog.FormatException(ex), 2);
			return 0;
		}
	}
}
