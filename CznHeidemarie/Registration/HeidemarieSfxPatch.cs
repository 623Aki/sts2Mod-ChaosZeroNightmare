using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// Heidemarie 的战斗音效重定向工具。
///
/// <para><c>CharacterModel.AttackSfx</c> / <c>CastSfx</c> / <c>DeathSfx</c> 都是
/// 非虚属性，无法在 <see cref="Heidemarie"/> 里 override，
/// 因此用 Harmony 补丁把这三个属性在 Heidemarie 实例上指向战士的同名音效，
/// 避免 FMOD 找不到 <c>event:/sfx/characters/heidemarie/*</c> 事件而刷警告。</para>
/// </summary>
internal static class HeidemarieSfxRedirect
{
	/// <summary>战士同结构音效的事件路径前缀。</summary>
	private const string IroncladSfxPrefix = "event:/sfx/characters/ironclad/ironclad_";

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

		result = IroncladSfxPrefix + suffix;
		return false;
	}
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
