using System;
using System.Diagnostics;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace CznHeidemarie.Registration;

/// <summary>
/// 死亡动画名守护（GameOver「Can not find animation: die」）。
///
/// <para>背景：RitsuLib 的 <c>CharacterGameOverScreenCompatibilityPatch</c> 在 GameOver 时
/// 会给每个角色 <c>NCreatureVisuals</c> 播 <c>die</c>（栈：
/// <c>ModCreatureVisualPlayback.TryPlaySpine → MegaAnimationState.SetAnimation</c>）。
/// 30093 骨架没有 <c>die</c>，Spine 每次都刷一条
/// <c>ERROR: Can not find animation: die</c>。</para>
///
/// <para>本角色的败北演出不依赖本体 Spine 死亡动画：走的是败北 CG 覆盖层
/// （<c>HeidemarieDefeatCgRuntime</c> 的 collapse 帧序列，日志
/// <c>heidemarie_defeat_runtime_built fx=6 slots=12</c>）与
/// <c>GameOverVisualScaleRuntime</c> 的独立死亡序列。因此这里不把 <c>die</c>
/// 改写成别的动画（会和 CG 叠加成双份演出），直接**跳过原方法**消掉报错。</para>
///
/// <para>判据：调用栈来自 RitsuLib 的 <c>ModCreatureVisualPlayback</c>（本次报错的源头）。
/// 取不到栈时一律放行，保持原行为 —— 宁可留一条报错日志，也不改变任何角色的死亡演出。
/// 之所以不查「骨架有没有 die」：<see cref="MegaAnimationState"/> 与 <c>GodotObject</c>
/// 没有继承关系（编译期 CS8121 证实），拿不到骨架句柄；而败北 CG 覆盖层与本体 Spine
/// 死亡动画本就是两条独立路径，跳过不影响观感。</para>
/// </summary>
[HarmonyPatch(typeof(MegaAnimationState), "SetAnimation", new Type[] { typeof(string), typeof(bool), typeof(int) })]
internal static class HeidemarieDeathAnimGuardPatch
{
	/// <summary>RitsuLib 死亡 cue 的动画名。</summary>
	private const string DeathAnimationName = "die";

	/// <summary>RitsuLib 播放封装类的名字（调用栈判据用）。</summary>
	private const string RitsuLibPlaybackClass = "ModCreatureVisualPlayback";

	/// <summary>只提示一次，避免 GameOver 时刷屏。</summary>
	private static bool _logged;

	/// <summary>
	/// 返回 <c>false</c> = 跳过原方法（不播 <c>die</c>）；<c>true</c> = 放行。
	/// </summary>
	private static bool Prefix(MegaAnimationState __instance, string animation, bool loop, int trackId)
	{
		if (!string.Equals(animation, DeathAnimationName, StringComparison.Ordinal))
		{
			return true;
		}

		// 判据：调用栈来自 RitsuLib 的播放封装（本次报错的源头）。
		if (!IsCalledByRitsuLibPlayback())
		{
			return true;
		}

		if (!_logged)
		{
			_logged = true;
			Integration.HeidemarieIntegration.Log.Info(
				$"已跳过死亡动画 \"{DeathAnimationName}\"（调用来自 RitsuLib ModCreatureVisualPlayback，30093 骨架无该动画）"
				+ " —— 败北演出由败北 CG 覆盖层负责；如需恢复请删本补丁。");
		}

		return false;
	}

	/// <summary>
	/// 当前调用栈里是否出现 RitsuLib 的 Spine 播放封装类。
	/// GameOver 一次只走一两回，构造 StackTrace 的开销可以忽略。
	/// </summary>
	private static bool IsCalledByRitsuLibPlayback()
	{
		try
		{
			StackTrace stack = new();
			for (int i = 0; i < stack.FrameCount; i++)
			{
				string? declaringType = stack.GetFrame(i)?.GetMethod()?.DeclaringType?.FullName;
				if (declaringType != null && declaringType.IndexOf(RitsuLibPlaybackClass, StringComparison.Ordinal) >= 0)
				{
					return true;
				}
			}
		}
		catch
		{
			// 取栈失败 → 放行，不阻断任何演出
		}

		return false;
	}
}
