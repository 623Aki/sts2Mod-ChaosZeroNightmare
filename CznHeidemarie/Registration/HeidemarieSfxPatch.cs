using System;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// Heidemarie 的战斗音效重定向工具。
///
/// <para><c>CharacterModel.AttackSfx</c> / <c>CastSfx</c> / <c>DeathSfx</c> 都是
/// 非虚属性，无法在 <see cref="Heidemarie"/> 里 override，因此靠 Harmony 改写。</para>
///
/// <para><b>改成占位事件路径而不是其它角色的音效</b>：
/// 本角色真正的战斗音效是素材区导出的 ogg（<c>se_30093_*.ogg</c>），由
/// <see cref="Integration.HeidemarieCombatAudio"/> 用 Godot 音频播放器播放 ——
/// 这条属性返回的是 <b>FMOD 事件路径</b>，两者不是一套东西，指过去也放不出来。
/// 所以这里统一返回 <c>/characters/heidemarie/</c> 前缀的占位路径，
/// 再由 <c>Registration/HeidemarieCombatPresentationPatch.cs</c> 里的
/// <c>SfxCmd.Play</c> 补丁把它挡掉：既不发声（我们的音频接管），
/// 也不会让 FMOD 去解析一个不存在的 <c>event:/</c> 事件而刷警告。</para>
/// </summary>
internal static class HeidemarieSfxRedirect
{
	/// <summary>本角色占位事件路径的前缀；<c>SfxCmd.Play</c> 按它识别并拦截。</summary>
	internal const string PlaceholderSfxPrefix = "event:/sfx/characters/heidemarie/heidemarie_";

	/// <summary>
	/// 仅当实例是 Heidemarie 时改写结果；其它角色交回原实现，保持零侵入。
	/// </summary>
	/// <param name="instance">正在取属性的角色模型。</param>
	/// <param name="suffix">音效后缀（attack / cast / die）。</param>
	/// <param name="result">原属性的返回值，命中时被覆盖。</param>
	/// <returns>返回 <c>true</c> 表示继续执行原实现。</returns>
	internal static bool Redirect(CharacterModel instance, string suffix, ref string result)
	{
		if (instance is not Heidemarie)
		{
			return true;
		}

		result = PlaceholderSfxPrefix + suffix;
		return false;
	}

	/// <summary>这条 FMOD 事件路径是不是本角色的占位路径（应当被拦掉）。</summary>
	internal static bool IsPlaceholderSfx(string? sfx)
		=> !string.IsNullOrEmpty(sfx) && sfx.StartsWith(PlaceholderSfxPrefix, StringComparison.Ordinal);
}

/// <summary>把 Heidemarie 的攻击音效重定向到战士的攻击音效。</summary>
[HarmonyPatch(typeof(CharacterModel), "get_AttackSfx")]
internal static class HeidemarieAttackSfxPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		return HeidemarieSfxRedirect.Redirect(__instance, "attack", ref __result);
	}
}

/// <summary>把 Heidemarie 的施法音效重定向到战士的施法音效。</summary>
[HarmonyPatch(typeof(CharacterModel), "get_CastSfx")]
internal static class HeidemarieCastSfxPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		return HeidemarieSfxRedirect.Redirect(__instance, "cast", ref __result);
	}
}

/// <summary>把 Heidemarie 的死亡音效重定向到战士的死亡音效。</summary>
[HarmonyPatch(typeof(CharacterModel), "get_DeathSfx")]
internal static class HeidemarieDeathSfxPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		return HeidemarieSfxRedirect.Redirect(__instance, "die", ref __result);
	}
}
