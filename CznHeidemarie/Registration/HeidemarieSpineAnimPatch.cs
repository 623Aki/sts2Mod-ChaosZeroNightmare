using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace CznHeidemarie.Registration;

/// <summary>
/// Spine 动画名别名解析工具。
///
/// <para>背景：游戏原始角色的 Spine 骨架里，休息点动画固定叫
/// <c>overgrowth_loop / hive_loop / glory_loop</c>，商店柜台动画固定叫 <c>relaxed_loop</c>。
/// 本 mod 用的是《卡厄思梦境》原生 30093 骨架，它只有 <c>camping / stop / b_idle</c>
/// 这套命名，没有上述游戏约定名。骨架又不允许改动（见 AGENTS.md 2.3），
/// 因此只能在这里把「骨架里不存在的名字」改写成骨架真实拥有的近义动画。</para>
///
/// <para>所有改写都先做存在性判断：骨架自带该动画时（游戏原始角色）一律原样返回，
/// 对其它角色零侵入。</para>
/// </summary>
internal static class HeidemarieSpineAnimAlias
{
	/// <summary>SpineSprite 的 Godot 类名，用于在子节点里定位骨架节点。</summary>
	private const string SpineClassName = "SpineSprite";

	/// <summary>
	/// 在 <paramref name="root"/> 的直接子节点里找出第一个 SpineSprite。
	/// </summary>
	/// <param name="root">宿主节点（商店 / 休息点角色场景的根）。</param>
	/// <returns>包装好的骨架控制器；没找到骨架时返回 <c>null</c>。</returns>
	internal static MegaSprite? FindSpineSprite(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child.GetClass().ToString() == SpineClassName)
			{
				return new MegaSprite(child);
			}
		}

		return null;
	}

	/// <summary>
	/// 若骨架里没有 <paramref name="requested"/>，就依次回退到
	/// <paramref name="fallbacks"/> 中第一个真实存在的动画名。
	/// </summary>
	/// <param name="sprite">目标骨架；为 <c>null</c> 或骨架尚未就绪时不做任何改写。</param>
	/// <param name="requested">游戏原本要播的动画名。</param>
	/// <param name="fallbacks">候选替代动画名，按优先级排列。</param>
	/// <returns>最终应当播放的动画名。</returns>
	internal static string Resolve(MegaSprite? sprite, string requested, params string[] fallbacks)
	{
		// 骨架未就绪时查不到动画表，强行改写反而会改错，直接交回原值
		if (sprite is null || !sprite.IsAnimationStateReady())
		{
			return requested;
		}

		if (sprite.HasAnimation(requested))
		{
			return requested;
		}

		foreach (string candidate in fallbacks)
		{
			if (sprite.HasAnimation(candidate))
			{
				return candidate;
			}
		}

		return requested;
	}

	/// <summary>
	/// 循环播放指定动画，并把起始时间随机化，
	/// 避免同屏多个同类角色动作完全同步（与游戏原生实现保持一致）。
	/// </summary>
	/// <param name="state">骨架的动画状态机。</param>
	/// <param name="animationName">要循环播放的动画名。</param>
	internal static void PlayLooping(MegaAnimationState state, string animationName)
	{
		state.SetAnimation(animationName, loop: true);
		using MegaTrackEntry? entry = state.GetCurrent(0);
		entry?.SetTrackTime(entry.GetAnimationEnd() * GD.Randf());
	}
}

/// <summary>
/// 商店柜台动画重定向。
///
/// <para><c>NMerchantCharacter._Ready</c> 硬编码播放 <c>relaxed_loop</c>，
/// 30093 骨架没有该动画，会把角色留在 setup pose 上。</para>
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class HeidemarieMerchantAnimPatch
{
	/// <summary>
	/// 只改写实际播放的动画名，不改动原方法的其它行为。
	/// </summary>
	private static void Prefix(NMerchantCharacter __instance, ref string anim)
	{
		// stop 是 30093.skel 里的停驻姿势；逐级回退保证任何骨架都有能播的动画
		anim = HeidemarieSpineAnimAlias.Resolve(
			HeidemarieSpineAnimAlias.FindSpineSprite(__instance),
			anim,
			"stop",
			"b_idle",
			"idle");
	}
}

/// <summary>
/// 休息点动画重定向。
///
/// <para><c>NRestSiteCharacter._Ready</c> 按当前幕硬编码播
/// <c>overgrowth_loop / hive_loop / glory_loop</c>，30093 骨架都没有。</para>
///
/// <para>动画名是在方法内部算出来的局部变量，补丁拿不到，所以改成在
/// 原方法执行完之后、骨架就绪时再覆盖一次：原方法注册的等待回调先执行，
/// 本回调后执行，因此最终播放结果由这里决定。</para>
/// </summary>
[HarmonyPatch(typeof(NRestSiteCharacter), "_Ready")]
internal static class HeidemarieRestSiteAnimPatch
{
	private static void Postfix(NRestSiteCharacter __instance)
	{
		MegaSprite? sprite = HeidemarieSpineAnimAlias.FindSpineSprite(__instance);
		if (sprite is null)
		{
			return;
		}

		__instance.RunWhenSpineReady(sprite, state =>
		{
			// 骨架自带三幕休息动画（游戏原始角色）时保持游戏原本的播放结果
			if (sprite.HasAnimation("overgrowth_loop"))
			{
				return;
			}

			string? target = null;
			if (sprite.HasAnimation("camping"))
			{
				target = "camping";
			}
			else if (sprite.HasAnimation("collapse_idle"))
			{
				target = "collapse_idle";
			}
			else if (sprite.HasAnimation("b_idle"))
			{
				target = "b_idle";
			}

			if (target is not null)
			{
				HeidemarieSpineAnimAlias.PlayLooping(state, target);
			}
		});
	}
}
