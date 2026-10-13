using System;
using System.Collections.Generic;
using CznCore.Presentation;
using CznCore.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 框架接入入口。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.Entry.Init</c>。分两件事：</para>
///
/// <list type="number">
///   <item><b>注册 CznCore 的补丁组</b> —— 这些补丁把游戏原生回调
///   （手牌聚焦、出牌提交、房间切换……）转发到 <see cref="SkinEventHub"/>，
///   出牌特写与设置面板都靠它们拿事件。
///   注意参考 mod 没有走 <c>CharacterSkinModBootstrap.Init</c>（那条路要求提供完整的
///   <c>CharacterSkinProfile</c>，是为「一套框架驱动多个角色的皮肤系统」准备的），
///   而是像这里一样按组手动注册。</item>
///   <item><b>启动本 mod 的运行时</b> —— 目前只有出牌特写。</item>
/// </list>
/// </summary>
internal static class HeidemarieIntegration
{
	private static bool _initialized;

	/// <summary>本 mod 的日志出口，供各接入组件共用。</summary>
	internal static Logger Log { get; } = new Logger("CznHeidemarie", LogType.Generic);

	private static readonly Lazy<SkinSettingsGlobalUiRuntime> SettingsGlobalRuntimeLazy =
		new Lazy<SkinSettingsGlobalUiRuntime>(() => new SkinSettingsGlobalUiRuntime(HeidemarieSettings.Store));

	private static readonly Lazy<SkinSettingsUiTextRuntime> SettingsUiTextRuntimeLazy =
		new Lazy<SkinSettingsUiTextRuntime>(() => new SkinSettingsUiTextRuntime(HeidemarieText.SettingsUi));

	private static readonly Lazy<HeidemarieSettingsUiRuntime> SettingsUiRuntimeLazy =
		new Lazy<HeidemarieSettingsUiRuntime>(() => new HeidemarieSettingsUiRuntime(HeidemarieSettings.CurrentCharacter));

	private static SkinSettingsGlobalUiRuntime SettingsGlobalRuntime => SettingsGlobalRuntimeLazy.Value;

	private static SkinSettingsUiTextRuntime SettingsUiTextRuntime => SettingsUiTextRuntimeLazy.Value;

	/// <summary>本角色的设置视图；选人界面的设置抽屉也复用同一个实例。</summary>
	internal static HeidemarieSettingsUiRuntime SettingsUiRuntime => SettingsUiRuntimeLazy.Value;

	/// <summary>
	/// 设置界面打开时把本角色的设置分区注入进去。
	///
	/// <para>补丁挂在 <c>NSettingsScreen</c> 上，与参考 mod 的
	/// <c>Entry.SettingsReadyPatch</c> / <c>SettingsSubmenuPatch</c> 一一对应。</para>
	/// </summary>
	[HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
	private static class SettingsReadyPatch
	{
		private static void Postfix(NSettingsScreen __instance)
		{
			TryInjectSettingsUi(__instance);
		}
	}

	/// <summary>子菜单切换后重新注入。</summary>
	[HarmonyPatch(typeof(NSettingsScreen), "OnSubmenuOpened")]
	private static class SettingsSubmenuPatch
	{
		private static void Postfix(NSettingsScreen __instance)
		{
			TryInjectSettingsUi(__instance);
		}
	}

	/// <summary>
	/// 初始化。由 <c>MainFile.Initialize</c> 在 <c>harmony.PatchAll()</c> 之后调用。
	/// </summary>
	/// <param name="harmony">mod 入口创建的 Harmony 实例。</param>
	public static void Init(Harmony harmony)
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;

		// 强制求值一次，让设置存储在启动时就完成加载
		_ = SettingsUiRuntime;

		ApplyCorePatchGroups(harmony);
		HeidemarieBattleReadyRuntime.Initialize();
		// 战斗入场演出：进入战斗时播一次 Entry 动作链（见 HeidemarieCombatEntryRuntime）
		HeidemarieCombatEntryRuntime.Initialize();
		// 选人界面背景变体切换：立绘 / 动态立绘 / 角色大厅（见 HeidemarieCharSelectBackgroundRuntime）
		HeidemarieCharSelectBackgroundRuntime.Initialize();
		// 选人界面左侧中间的角色设置抽屉（见 HeidemarieCharSelectMenuRuntime）
		HeidemarieCharSelectMenuRuntime.Initialize();
		// 全屏演出：终焉剑舞 / 破军的整屏接管演出（见 HeidemarieCinematicRuntime）
		HeidemarieCinematicRuntime.Initialize();
		// 战斗界面右侧的「全屏演出」开关（见 HeidemarieCutInToggleRuntime）
		HeidemarieCutInToggleRuntime.Initialize(harmony);
		// 单体技能特效：把已整合的特效骨架接到卡牌演出上（见 HeidemarieMoveEffectsRuntime）
		HeidemarieMoveEffectsRuntime.Initialize();

		Log.Info("海德玛丽框架接入完成。");
	}

	/// <summary>
	/// 注册 CznCore 的事件转发补丁组。
	/// </summary>
	/// <remarks>
	/// 只挑本 mod 真正依赖的四组：
	/// <list type="bullet">
	///   <item><c>SkinCardTransportPatches</c> —— 手牌聚焦/悬停/按下/出牌提交，出牌特写的全部事件源；</item>
	///   <item><c>SkinTransportPatches</c> —— 房间进入/战斗开始等生命周期事件；</item>
	///   <item><c>SkinCharacterSelectPatches</c> —— 选人界面事件，设置面板挂载用；</item>
	///   <item><c>CharacterLifecycleTransport</c> —— 玩家阵亡/胜利，用于清理特写。</item>
	/// </list>
	/// 其余组（战斗特效音效替换、形态 VFX、连携卡、过场切换 HUD）对应本项目没有的功能，不注册。
	/// </remarks>
	private static void ApplyCorePatchGroups(Harmony harmony)
	{
		int patched = 0;
		List<string>? skipped = null;

		PatchGroup(harmony, SkinCardTransportPatches.PatchTypes, ref patched, ref skipped);
		PatchGroup(harmony, SkinTransportPatches.PatchTypes, ref patched, ref skipped);
		PatchGroup(harmony, SkinCharacterSelectPatches.PatchTypes, ref patched, ref skipped);
		PatchGroup(harmony, CharacterLifecycleTransport.PatchTypes, ref patched, ref skipped);

		string summary = $"框架补丁：成功 {patched} 个" + (skipped is null ? string.Empty : $"，跳过 {skipped.Count} 个 [{string.Join(", ", skipped)}]");
		Log.Info(summary);
	}

	private static void PatchGroup(Harmony harmony, IReadOnlyList<Type> patchTypes, ref int patched, ref List<string>? skipped)
	{
		foreach (Type patchType in patchTypes)
		{
			try
			{
				harmony.CreateClassProcessor(patchType).Patch();
				patched++;
			}
			catch (Exception ex)
			{
				// 单个补丁的目标方法签名在游戏版本间可能变化，失败了不该拖垮整个 mod
				(skipped ??= new List<string>()).Add(patchType.Name);
				Log.Warn($"跳过补丁 {patchType.Name}：{ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	private static void TryInjectSettingsUi(NSettingsScreen screen)
	{
		SkinSettingsUi.TryInject(screen, SettingsUiRuntime, SettingsGlobalRuntime, SettingsUiTextRuntime);
	}
}
