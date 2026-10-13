using System;
using System.Runtime.CompilerServices;

namespace CznCore.Presentation;

internal sealed class CombatHitTimingRuntime<TSession> where TSession : class
{
	private sealed class HitTimingState
	{
		public string AnimationName { get; set; } = string.Empty;

		public ulong StartMsec { get; set; }
	}

	private readonly Func<ulong> _getTicksMsec;

	private readonly ConditionalWeakTable<TSession, HitTimingState> _states = new ConditionalWeakTable<TSession, HitTimingState>();

	public CombatHitTimingRuntime(Func<ulong> getTicksMsec)
	{
		_getTicksMsec = getTicksMsec ?? throw new ArgumentNullException("getTicksMsec");
	}

	public void MarkStart(TSession? session, string animationName, float startDelaySeconds)
	{
		if (session != null)
		{
			HitTimingState orCreateValue = _states.GetOrCreateValue(session);
			orCreateValue.AnimationName = (string.IsNullOrEmpty(animationName) ? string.Empty : animationName);
			orCreateValue.StartMsec = _getTicksMsec() + (ulong)Math.Round(Math.Max(0f, startDelaySeconds) * 1000f);
		}
	}

	public float ResolveRemainingDelaySeconds(TSession? session, CombatHitHoldDurationResolver resolveHoldSeconds)
	{
		if (session == null || resolveHoldSeconds == null || !_states.TryGetValue(session, out HitTimingState value) || !resolveHoldSeconds(value.AnimationName, out var holdSeconds))
		{
			return 0f;
		}
		ulong num = _getTicksMsec();
		float num2 = ((num >= value.StartMsec) ? ((float)(num - value.StartMsec) / 1000f) : (0f - (float)(value.StartMsec - num) / 1000f));
		return Math.Max(0f, holdSeconds - num2);
	}
}
