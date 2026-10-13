using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznCore.Presentation;

internal static class CznRuntimeCapabilities
{
	private static readonly Type[] SetAnimationParameterTypes = new Type[3]
	{
		typeof(string),
		typeof(bool),
		typeof(int)
	};

	private static readonly Type[] AddAnimationParameterTypes = new Type[4]
	{
		typeof(string),
		typeof(float),
		typeof(bool),
		typeof(int)
	};

	internal static MethodInfo SpineSetAnimationMethod { get; } = typeof(MegaAnimationState).GetMethod("SetAnimation", SetAnimationParameterTypes) ?? throw new MissingMethodException(typeof(MegaAnimationState).FullName, "SetAnimation");

	internal static MethodInfo SpineAddAnimationMethod { get; } = typeof(MegaAnimationState).GetMethod("AddAnimation", AddAnimationParameterTypes) ?? throw new MissingMethodException(typeof(MegaAnimationState).FullName, "AddAnimation");

	internal static MethodInfo? SpineAddAnimationTrackedMethod { get; } = typeof(MegaAnimationState).GetMethod("AddAnimationTracked", AddAnimationParameterTypes);

	public static bool UsesFireAndForgetAnimationApi => SpineSetAnimationMethod.ReturnType == typeof(void);

	public static bool HasTrackedAnimationQueueApi
	{
		get
		{
			if (SpineAddAnimationTrackedMethod != null)
			{
				return typeof(MegaTrackEntry).IsAssignableFrom(SpineAddAnimationTrackedMethod.ReturnType);
			}
			return false;
		}
	}

	public static bool TrackEntryRequiresCallerDispose => typeof(IDisposable).IsAssignableFrom(typeof(MegaTrackEntry));

	public static bool RequiresLowPressureSpineWarmupPolicy
	{
		get
		{
			if (!UsesFireAndForgetAnimationApi)
			{
				return TrackEntryRequiresCallerDispose;
			}
			return true;
		}
	}

	public static bool UseSubThreadsForWarmupResourceLoads => !RequiresLowPressureSpineWarmupPolicy;

	public static string Describe()
	{
		return "spine_set_returns=" + DescribeReturnType(SpineSetAnimationMethod) + " spine_add_returns=" + DescribeReturnType(SpineAddAnimationMethod) + " tracked_queue=" + HasTrackedAnimationQueueApi + " track_entry_disposable=" + TrackEntryRequiresCallerDispose + " low_pressure_spine_warmup=" + RequiresLowPressureSpineWarmupPolicy + " warmup_subthreads=" + UseSubThreadsForWarmupResourceLoads;
	}

	private static string DescribeReturnType(MethodInfo method)
	{
		if (!(method.ReturnType == typeof(void)))
		{
			return method.ReturnType.Name;
		}
		return "void";
	}
}
