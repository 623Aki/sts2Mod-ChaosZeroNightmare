using System;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;

namespace CznHeidemarie.Registration;

/// <summary>
/// Heidemarie 的能量图标路径重定向工具。
///
/// <para><see cref="EnergyIconHelper.GetPath(string)"/> 对任何配色键都会拼出
/// <c>res://images/atlases/ui_atlas.sprites/card/energy_&lt;键&gt;.tres</c>，
/// 但这条路径被游戏的 <c>AtlasResourceLoader</c> 拦截：它把 <c>res://images/atlases/</c> 下的
/// <c>.tres</c> 统一当作「内存图集里的一个 sprite」来解析，从 <c>AtlasManager</c> 取
/// <c>AtlasTexture</c>，命中不到也不会回落到磁盘上的同名文件。</para>
///
/// <para>而 <c>ui_atlas</c> 里没有本角色的 sprite、也没有 fallback 分支，
/// 最终只会返回默认的 <c>missing_power.png</c>——所以即使 mod 目录下放了那份同名
/// <c>.tres</c>，也永远不会被读到（游戏日志会刷
/// <c>AtlasResourceLoader: Missing sprite 'card/energy_heidemarie' in ui_atlas</c>）。</para>
///
/// <para>做法与参考 mod（Nine / Tiphera）一致：Postfix 命中本角色配色键时改写结果，
/// 直接指向 mod 自带的独立 PNG，绕开图集系统。</para>
///
/// <para>本类由 <c>MainFile.ApplyModPatches</c> 按命名空间 <c>CznHeidemarie.*</c> 自动扫描挂载，
/// 无需在入口处手动注册。</para>
/// </summary>
internal static class HeidemarieEnergyIconRedirect
{
	/// <summary>
	/// 本角色卡牌费用图标所用的独立 PNG 路径。
	///
	/// <para><b>注意前缀</b>：本工程的 <c>images/</c> 树位于 Godot 工程根，
	/// 不在嵌套的 <c>CznHeidemarie/</c> 目录下（后者只放 <c>animations/</c>、<c>cinematic/</c>、
	/// <c>audio/</c>、<c>localization/</c> 等）。因此必须写
	/// <c>res://images/...</c>；误写成 <c>res://CznHeidemarie/images/...</c> 会让
	/// 资源加载失败并被 <c>AssetCache</c> 永久标记，之后每次取能量提示都会抛
	/// <c>AssetLoadException</c>（症状见 Neow 事件生成选项时崩溃）。</para>
	/// </summary>
	internal const string CardEnergyIconPath = "res://images/ui/energy/heidemarie_energy_card_74.png";

	/// <summary>本角色的能量图标配色键，与 <see cref="HeidemarieCardPool.EnergyColorName"/> 保持一致。</summary>
	internal const string EnergyColorName = "heidemarie";
}

/// <summary>
/// 把 Heidemarie 的卡牌 / 遗物 / 药水提示能量图标，从「图集路径」重定向到 mod 自带的单图 PNG。
/// </summary>
[HarmonyPatch(typeof(EnergyIconHelper), "GetPath", new Type[] { typeof(string) })]
internal static class HeidemarieEnergyIconPathPatch
{
	/// <summary>
	/// 仅当配色键为 heidemarie 时改写路径；其它角色交回原实现，保持零侵入。
	/// </summary>
	/// <param name="prefix">配色键（本角色的 <c>EnergyColorName</c>）。</param>
	/// <param name="__result">原方法返回值，命中时被覆盖。</param>
	private static void Postfix(string prefix, ref string __result)
	{
		if (string.Equals(prefix, HeidemarieEnergyIconRedirect.EnergyColorName, StringComparison.OrdinalIgnoreCase))
		{
			__result = HeidemarieEnergyIconRedirect.CardEnergyIconPath;
		}
	}
}
