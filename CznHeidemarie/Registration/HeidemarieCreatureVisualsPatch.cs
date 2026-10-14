using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznHeidemarie.Registration;

/// <summary>
/// 让「正在待机 / 正在受击」这一对判定认识本 mod 骨架的动画命名。
///
/// <para><c>NCreatureVisuals</c> 里这两个方法都是按动画名字符串硬判的：
/// 待机认 <c>idle_loop</c> / <c>low_health_loop</c>，受击认 <c>hurt</c>。
/// 本 mod 的 30093 骨架用的却是 <c>b_idle</c> / <c>hit</c>，名字对不上时判定恒为 false。</para>
///
/// <list type="bullet">
///   <item><see cref="NCreatureVisuals.IsPlayingIdleAnimation"/> 在治疗流程里被用来决定
/// 是否重播待机动作（<c>CreatureCmd</c>）；</item>
///   <item><see cref="NCreatureVisuals.IsPlayingHurtAnimation"/> 被 <c>NCreature</c>
/// 用来避免受击抖动与受击动画互相打架。</item>
/// </list>
///
/// <para>两处补丁都只在「原判定为 false 且骨架确实没有游戏约定动画」时才补一个近义名判断，
/// 因此游戏原始角色完全不受影响。</para>
/// </summary>
internal static class HeidemarieCreatureVisualsShared
{
	/// <summary>游戏约定的战斗待机动画名。</summary>
	internal const string IdleLoopAnimation = "idle_loop";

	/// <summary>游戏约定的受击动画名。</summary>
	internal const string HurtAnimation = "hurt";

	/// <summary>
	/// 判断 visuals 是否属于 Heidemarie 玩家。
	/// 游戏把 NCreatureVisuals 直接挂在 NCreature 下，实体角色可从其 Player.Character 取得。
	/// </summary>
	internal static bool IsHeidemarie(NCreatureVisuals visuals)
	{
		return visuals.GetParent() is NCreature creature
			&& creature.Entity.Player?.Character is Heidemarie;
	}

	/// <summary>
	/// 骨架是否遵循游戏的动画命名约定。
	/// 遵循的话（游戏原始角色）补丁一律放手，交回原实现。
	/// </summary>
	internal static bool UsesGameNaming(MegaSprite? body, string conventionAnimation)
	{
		return body is null || body.HasAnimation(conventionAnimation);
	}
}

/// <summary>
/// 待机判定补丁：骨架把待机命名为 <c>b_idle</c> / <c>idle</c> 时同样算作待机。
/// </summary>
[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.IsPlayingIdleAnimation))]
internal static class HeidemarieIdleAnimationPatch
{
	private static void Postfix(NCreatureVisuals __instance, ref bool __result)
	{
		if (__result || !HeidemarieCreatureVisualsShared.IsHeidemarie(__instance))
		{
			return;
		}

		MegaSprite? body = __instance.SpineBody;
		if (HeidemarieCreatureVisualsShared.UsesGameNaming(body, HeidemarieCreatureVisualsShared.IdleLoopAnimation))
		{
			return;
		}

		string? current = __instance.SpineAnimation.GetCurrentAnimationName();
		__result = current is "b_idle" or "idle";
	}
}

/// <summary>
/// 受击判定补丁：骨架把受击命名为 <c>hit</c> 时同样算作受击。
/// </summary>
[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.IsPlayingHurtAnimation))]
internal static class HeidemarieHurtAnimationPatch
{
	private static void Postfix(NCreatureVisuals __instance, ref bool __result)
	{
		if (__result || !HeidemarieCreatureVisualsShared.IsHeidemarie(__instance))
		{
			return;
		}

		MegaSprite? body = __instance.SpineBody;
		if (HeidemarieCreatureVisualsShared.UsesGameNaming(body, HeidemarieCreatureVisualsShared.HurtAnimation))
		{
			return;
		}

		__result = __instance.SpineAnimation.GetCurrentAnimationName() == "hit";
	}
}
