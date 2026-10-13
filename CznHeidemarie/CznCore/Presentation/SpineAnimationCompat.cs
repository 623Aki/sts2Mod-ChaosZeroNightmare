using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal static class SpineAnimationCompat
{
	public static bool UsesFireAndForgetAnimationApi => CznRuntimeCapabilities.UsesFireAndForgetAnimationApi;

	public static void SetAnimation(SpineAnimationAccess access, string animationName, bool loop = true, int trackId = 0)
	{
		SetAnimation(access.GetAnimationState(), animationName, loop, trackId);
	}

	public static void SetAnimation(MegaAnimationState? state, string animationName, bool loop = true, int trackId = 0)
	{
		if (state != null)
		{
			Invoke(CznRuntimeCapabilities.SpineSetAnimationMethod, state, animationName, loop, trackId);
		}
	}

	public static MegaTrackEntry? SetAnimationTracked(SpineAnimationAccess access, string animationName, bool loop = true, int trackId = 0)
	{
		return SetAnimationTracked(access.GetAnimationState(), animationName, loop, trackId);
	}

	public static MegaTrackEntry? SetAnimationTracked(MegaAnimationState? state, string animationName, bool loop = true, int trackId = 0)
	{
		if (state == null)
		{
			return null;
		}
		return (Invoke(CznRuntimeCapabilities.SpineSetAnimationMethod, state, animationName, loop, trackId) as MegaTrackEntry) ?? state.GetCurrent(trackId);
	}

	public static void AddAnimation(SpineAnimationAccess access, string animationName, float delay = 0f, bool loop = true, int trackId = 0)
	{
		AddAnimation(access.GetAnimationState(), animationName, delay, loop, trackId);
	}

	public static void AddAnimation(MegaAnimationState? state, string animationName, float delay = 0f, bool loop = true, int trackId = 0)
	{
		if (state != null)
		{
			Invoke(CznRuntimeCapabilities.SpineAddAnimationMethod, state, animationName, delay, loop, trackId);
		}
	}

	public static MegaTrackEntry? AddAnimationTracked(SpineAnimationAccess access, string animationName, float delay = 0f, bool loop = true, int trackId = 0)
	{
		return AddAnimationTracked(access.GetAnimationState(), animationName, delay, loop, trackId);
	}

	public static MegaTrackEntry? AddAnimationTracked(MegaAnimationState? state, string animationName, float delay = 0f, bool loop = true, int trackId = 0)
	{
		if (state == null)
		{
			return null;
		}
		return Invoke(CznRuntimeCapabilities.SpineAddAnimationTrackedMethod ?? CznRuntimeCapabilities.SpineAddAnimationMethod, state, animationName, delay, loop, trackId) as MegaTrackEntry;
	}

	public static void ReleaseTracked(MegaTrackEntry? entry)
	{
		((IDisposable)entry)?.Dispose();
	}

	private static object? Invoke(MethodInfo method, object target, params object?[] args)
	{
		try
		{
			return method.Invoke(target, args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}
}
