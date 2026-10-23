using CznCore.Multiplayer;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace CznHeidemarie.Integration;

/// <summary>
/// 在 run 的服务层就绪后把 <c>NetService</c> 交给 CZN 设置同步运行时。
///
/// <para>为什么挂 <c>RunManager.InitializeShared</c>：它是单机 / 联机 / 读档三条路径
/// 唯一共用的初始化点，且方法体内已经完成 <c>NetService</c> 赋值，
/// Postfix 里拿到的服务一定可用。</para>
///
/// <para>只做「转交」，不做任何判定：单机 / 联机的分支、消息的收发、
/// 断线后的回退全部在 <see cref="CznSettingsSyncRuntime"/> 里处理，
/// 这样多个 CZN mod 共用同一份逻辑，且本 mod 的补丁保持零侵入。</para>
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeShared")]
internal static class HeidemarieSettingsSyncPatch
{
	private static void Postfix(RunManager __instance)
	{
		CznSettingsSyncRuntime.OnRunServiceReady(__instance.NetService);
	}
}
