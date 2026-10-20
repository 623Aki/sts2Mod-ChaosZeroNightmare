using CznHeidemarie.Characters.Heidemarie.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// 「临时力量」能力的图标重定向 —— 让 <see cref="HeidemarieTemporaryStrengthPowerBase"/>
/// 的两个子类（市长铭牌 / 严阵以待两条发放链）共用同一张
/// <c>res://images/powers/heidemarie_temporary_strength_power.png</c>。
///
/// <para><b>为什么必须用补丁而不能在子类里 override</b>：引擎的
/// <see cref="PowerModel.PackedIconPath"/> 与 <c>BigIconPath</c> / <c>ResolvedBigIconPath</c>
/// 都不是虚成员（<c>BigIconPath</c> 甚至是 private），路径按
/// <c>Id.Entry</c> 拼死，取不到就回落 <c>images/powers/missing_power.png</c>。
/// 两个子类的 <c>Id.Entry</c> 分别是 <c>heidemarie_hope_strength_power</c> 与
/// <c>heidemarie_temporary_strength_power</c>，天然指向两个不同文件；
/// 而「两处发放共用一张图」是设计口径，所以在这里把两个 getter 的返回值统一改写。</para>
///
/// <para><b>做法与参考 mod 一致</b>：Nine / Tiphera 同样用补丁改写路径
/// （RitsuLib 的 <c>IModPowerAssetOverrides</c> 也是这个思路，只是它由框架代劳）。
/// 本工程不引第三方库（铁律 5），所以自己写这两条最小补丁。</para>
///
/// <para><b>只改自己的</b>：两条 Prefix 都先判 <c>__instance is HeidemarieTemporaryStrengthPowerBase</c>，
/// 不是就 <c>return true</c> 走原实现 —— 游戏自带能力与其它 mod 的能力完全不受影响。</para>
///
/// <para><b>覆盖面</b>：<c>Icon</c>（= <c>ResourceLoader.Load(PackedIconPath)</c>）与
/// <c>BigIcon</c>（= <c>AssetCache.GetTexture2D(ResolvedBigIconPath)</c>）都走这两个 getter，
/// 所以战斗内图标、能力施加 / 消失特效、悬停提示一次全部覆盖，不需要再补丁纹理属性本身。
/// 目标文件已有有效 <c>.import</c>（<c>images/powers/heidemarie_temporary_strength_power.png</c>），
/// 回落后不会触发 <c>AssetLoadException</c>。</para>
///
/// <para>本类由 <c>MainFile.ApplyModPatches</c> 按命名空间 <c>CznHeidemarie.*</c> 自动扫描挂载，
/// 无需在入口处手动注册。</para>
/// </summary>
[HarmonyPatch(typeof(PowerModel), "get_PackedIconPath")]
internal static class HeidemariePowerPackedIconPathPatch
{
	/// <summary>
	/// 本族能力把「图集 sprite 路径」换成共享 PNG；其它能力原样返回。
	/// </summary>
	/// <param name="__instance">正在取图标的能力实例。</param>
	/// <param name="__result">原 getter 的返回值，命中时被覆盖。</param>
	/// <returns><c>false</c> 表示跳过原实现（返回值已由本补丁给出）。</returns>
	private static bool Prefix(PowerModel __instance, ref string __result)
	{
		if (__instance is not HeidemarieTemporaryStrengthPowerBase)
		{
			return true;
		}

		__result = HeidemarieTemporaryStrengthPowerBase.SharedIconPath;
		return false;
	}
}

/// <summary>
/// 大图标（256）重定向：与 <see cref="HeidemariePowerPackedIconPathPatch"/> 同一口径，
/// 保证能力施加 / 消失特效与悬停提示用的也是同一张图。
/// </summary>
[HarmonyPatch(typeof(PowerModel), "get_ResolvedBigIconPath")]
internal static class HeidemariePowerBigIconPathPatch
{
	/// <summary>
	/// 本族能力把「大图标路径」换成共享 PNG；其它能力原样返回。
	/// </summary>
	/// <param name="__instance">正在取大图标的能力实例。</param>
	/// <param name="__result">原 getter 的返回值，命中时被覆盖。</param>
	/// <returns><c>false</c> 表示跳过原实现（返回值已由本补丁给出）。</returns>
	private static bool Prefix(PowerModel __instance, ref string __result)
	{
		if (__instance is not HeidemarieTemporaryStrengthPowerBase)
		{
			return true;
		}

		__result = HeidemarieTemporaryStrengthPowerBase.SharedIconPath;
		return false;
	}
}
