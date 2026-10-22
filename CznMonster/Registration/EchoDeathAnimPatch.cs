using CznMonster.Content.Monsters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznMonster.Registration;

/// <summary>
/// 余响碎片死亡演出压缩补丁（阿涅丝设计文档 §1.5 / §8 已知限制）：
/// <para>3000541 骨架只有 <c>normal_idle / normal_hit</c> 两条动作，且两条 Spine 时间轴
/// 都拖满 <b>8 秒</b>（CZN 原始数据如此，见提取 <c>3000541.json</c>）。死亡触发器落在
/// <c>normal_hit</c> 上后，引擎死亡流程按轨道总时长行动：
/// <see cref="NCreature.StartDeathAnim"/> 返回 <c>GetCurrentAnimationLength()</c>（8s）喂给
/// 战斗流程等待；<c>AnimDie</c> 用 <c>GetCurrentAnimationTimeRemaining() + 0.5</c> 拖住本体，
/// 之后才开始引擎统一的消散 burn——碎片死亡一卡就是 10 秒出头。</para>
/// <para>本补丁只对 <see cref="AgnesEcho"/> 实例把这两个读数压到「重伤定格 + 立即消散」
/// 的量级；其他怪物先判类型 <c>return false</c> 不入分支（零侵入铁律 6）。</para>
/// </summary>
[HarmonyPatch]
internal static class EchoDeathAnimPatch
{
	/// <summary>碎片死亡后到开始消散的最长等待（AnimDie 在该值上再 +0.5s）。</summary>
	private const float MaxTimeRemaining = 0.25f;

	/// <summary>死亡等待回传给战斗流程（HP 条淡出 / 手牌解锁等）的时长上限。</summary>
	private const float MaxAnimLength = 0.5f;

	[HarmonyPatch(typeof(NCreature), nameof(NCreature.GetCurrentAnimationTimeRemaining))]
	internal static class RemainingPatch
	{
		private static void Postfix(NCreature __instance, ref float __result)
		{
			if (__result > MaxTimeRemaining && __instance?.Entity?.Monster is AgnesEcho)
			{
				__result = MaxTimeRemaining;
			}
		}
	}

	[HarmonyPatch(typeof(NCreature), nameof(NCreature.GetCurrentAnimationLength))]
	internal static class LengthPatch
	{
		private static void Postfix(NCreature __instance, ref float __result)
		{
			if (__result > MaxAnimLength && __instance?.Entity?.Monster is AgnesEcho)
			{
				__result = MaxAnimLength;
			}
		}
	}
}
