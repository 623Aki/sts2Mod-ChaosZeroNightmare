using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatAttackTimingSession
{
	private readonly IReadOnlyList<float>? _damageReleaseScheduleSeconds;

	private int _nativeAttackTriggerIndex;

	public int Token { get; }

	public ulong StartMsec { get; }

	public bool HasDamageReleaseTiming
	{
		get
		{
			IReadOnlyList<float> damageReleaseScheduleSeconds = _damageReleaseScheduleSeconds;
			if (damageReleaseScheduleSeconds != null)
			{
				return damageReleaseScheduleSeconds.Count > 0;
			}
			return false;
		}
	}

	public CombatAttackTimingSession(int token, ulong startMsec, IReadOnlyList<float>? damageReleaseScheduleSeconds = null)
	{
		Token = token;
		StartMsec = startMsec;
		_damageReleaseScheduleSeconds = damageReleaseScheduleSeconds;
	}

	public int NextNativeAttackTriggerIndex()
	{
		int nativeAttackTriggerIndex = _nativeAttackTriggerIndex;
		_nativeAttackTriggerIndex++;
		return nativeAttackTriggerIndex;
	}

	public bool TryResolveDamageReleaseElapsedSeconds(int triggerIndex, int hitCount, out float targetElapsedSeconds)
	{
		targetElapsedSeconds = 0f;
		int num = Math.Max(1, hitCount);
		if (_damageReleaseScheduleSeconds == null || _damageReleaseScheduleSeconds.Count == 0 || triggerIndex < 0 || triggerIndex >= num)
		{
			return false;
		}
		float num2;
		if (num > 1)
		{
			num2 = _damageReleaseScheduleSeconds[Math.Min(triggerIndex, _damageReleaseScheduleSeconds.Count - 1)];
		}
		else
		{
			IReadOnlyList<float>? damageReleaseScheduleSeconds = _damageReleaseScheduleSeconds;
			num2 = damageReleaseScheduleSeconds[damageReleaseScheduleSeconds.Count - 1];
		}
		targetElapsedSeconds = num2;
		return true;
	}

	public float ResolveRemainingDelaySeconds(float targetElapsedSeconds, ulong nowMsec)
	{
		return ResolveRemainingDelaySeconds(StartMsec, targetElapsedSeconds, nowMsec);
	}

	public static float ResolveRemainingDelaySeconds(ulong startMsec, float targetElapsedSeconds, ulong nowMsec)
	{
		ulong num = startMsec + (ulong)Math.Round(Math.Max(0f, targetElapsedSeconds) * 1000f);
		if (nowMsec < num)
		{
			return (float)(num - nowMsec) / 1000f;
		}
		return 0f;
	}
}
