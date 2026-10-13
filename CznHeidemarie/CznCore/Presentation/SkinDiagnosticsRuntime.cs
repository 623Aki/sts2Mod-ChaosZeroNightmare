using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace CznCore.Presentation;

internal sealed class SkinDiagnosticsRuntime : ISkinTimingProbe
{
	private sealed class TimingSummaryEntry
	{
		public int Count { get; private set; }

		public double TotalMs { get; private set; }

		public double MaxMs { get; private set; }

		public string MaxLabel { get; private set; } = string.Empty;

		public double AverageMs
		{
			get
			{
				if (Count != 0)
				{
					return TotalMs / (double)Count;
				}
				return 0.0;
			}
		}

		public void Record(string label, double elapsedMs)
		{
			Count++;
			TotalMs += elapsedMs;
			if (elapsedMs > MaxMs)
			{
				MaxMs = elapsedMs;
				MaxLabel = label;
			}
		}
	}

	private readonly Func<bool> _isDebugEnabled;

	private readonly double _timingProbeThresholdMs;

	private readonly int _maxSummaryEntries;

	private readonly object _timingSummaryLock = new object();

	private readonly Dictionary<string, TimingSummaryEntry> _timingSummary = new Dictionary<string, TimingSummaryEntry>(StringComparer.Ordinal);

	private long _warmupDeferredUntilTick;

	private long _timingSummaryStartTick;

	public SkinDiagnosticsRuntime(Func<bool> isDebugEnabled, double timingProbeThresholdMs, int maxSummaryEntries = 8)
	{
		_isDebugEnabled = isDebugEnabled ?? throw new ArgumentNullException("isDebugEnabled");
		_timingProbeThresholdMs = Math.Max(0.0, timingProbeThresholdMs);
		_maxSummaryEntries = Math.Max(1, maxSummaryEntries);
	}

	public void Debug(string message, int priority = 2)
	{
		SkinLog.Debug(message, _isDebugEnabled(), priority);
	}

	public void Debug(Func<string> messageFactory, int priority = 2)
	{
		if (_isDebugEnabled())
		{
			SkinLog.Debug(messageFactory(), enabled: true, priority);
		}
	}

	public void Warn(string message, int priority = 2)
	{
		SkinLog.Warn(message, priority);
	}

	public void Error(string message, int priority = 2)
	{
		SkinLog.Error(message, priority);
	}

	public long StartTimingProbe()
	{
		if (!_isDebugEnabled())
		{
			return 0L;
		}
		return Stopwatch.GetTimestamp();
	}

	public void LogTimingProbe(string label, long startTimestamp)
	{
		if (startTimestamp == 0L || !_isDebugEnabled())
		{
			return;
		}
		double elapsedMs = (double)(Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / (double)Stopwatch.Frequency;
		RecordTimingProbe(label, elapsedMs);
		if (elapsedMs >= _timingProbeThresholdMs)
		{
			Debug(() => "perf " + label + "=" + elapsedMs.ToString("0.###", CultureInfo.InvariantCulture) + "ms");
		}
	}

	public void ResetTimingSummary()
	{
		lock (_timingSummaryLock)
		{
			_timingSummary.Clear();
			_timingSummaryStartTick = Environment.TickCount64;
		}
	}

	public void FlushTimingSummary(string reason)
	{
		if (!_isDebugEnabled())
		{
			return;
		}
		List<KeyValuePair<string, TimingSummaryEntry>> list;
		long timingSummaryStartTick;
		lock (_timingSummaryLock)
		{
			if (_timingSummary.Count == 0)
			{
				return;
			}
			list = new List<KeyValuePair<string, TimingSummaryEntry>>(_timingSummary);
			_timingSummary.Clear();
			timingSummaryStartTick = _timingSummaryStartTick;
			_timingSummaryStartTick = 0L;
		}
		list.Sort((KeyValuePair<string, TimingSummaryEntry> left, KeyValuePair<string, TimingSummaryEntry> right) => right.Value.MaxMs.CompareTo(left.Value.MaxMs));
		double durationSeconds = ((timingSummaryStartTick > 0) ? Math.Max(0.0, (double)(Environment.TickCount64 - timingSummaryStartTick) / 1000.0) : 0.0);
		int num = 0;
		foreach (KeyValuePair<string, TimingSummaryEntry> pair in list)
		{
			if (num >= _maxSummaryEntries)
			{
				break;
			}
			TimingSummaryEntry entry = pair.Value;
			Debug(() => "perf_summary reason=" + reason + " duration=" + durationSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s group=" + pair.Key + " count=" + entry.Count + " avg=" + entry.AverageMs.ToString("0.###", CultureInfo.InvariantCulture) + "ms max=" + entry.MaxMs.ToString("0.###", CultureInfo.InvariantCulture) + "ms max_label=" + entry.MaxLabel);
			num++;
		}
	}

	public void MarkWarmupBusy(float seconds)
	{
		if (!(seconds <= 0f))
		{
			long num = Environment.TickCount64 + (long)(seconds * 1000f);
			long num2;
			do
			{
				num2 = Volatile.Read(in _warmupDeferredUntilTick);
			}
			while (num2 < num && Interlocked.CompareExchange(ref _warmupDeferredUntilTick, num, num2) != num2);
		}
	}

	public bool ShouldDeferWarmup()
	{
		return Environment.TickCount64 < Volatile.Read(in _warmupDeferredUntilTick);
	}

	private void RecordTimingProbe(string label, double elapsedMs)
	{
		string timingGroup = GetTimingGroup(label);
		lock (_timingSummaryLock)
		{
			if (!_timingSummary.TryGetValue(timingGroup, out TimingSummaryEntry value))
			{
				value = new TimingSummaryEntry();
				_timingSummary[timingGroup] = value;
			}
			value.Record(label, elapsedMs);
		}
	}

	private static string GetTimingGroup(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return "<empty>";
		}
		int num = label.IndexOf(':');
		if (num <= 0)
		{
			return label;
		}
		return label.Substring(0, num);
	}
}
