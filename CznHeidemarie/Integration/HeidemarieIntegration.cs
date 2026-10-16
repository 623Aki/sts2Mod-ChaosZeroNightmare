using System;
using CznCore.Presentation;
using CznCore.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 框架接入入口。
///
	/// <para>对应参考 mod 的 <c>CznNineCharacter.Entry.Init</c>，完整通过
	/// <c>CharacterSkinModBootstrap</c> 注册本角色 Profile 与 CznCore 补丁组。</para>
///
/// <list type="number">
	///   <item><b>注册完整 CznCore Profile 与补丁组</b> —— 包括选人身份、预热、
	///   生命周期、手牌事件和兼容补丁。</item>
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

		CharacterSkinModBootstrap.Init(
			typeof(MainFile),
			HeidemarieSkinProfile.Profile,
			"local.cznheidemarie",
			"Heidemarie CznCore profile initialized.",
			isDebugEnabled: () => HeidemarieSettings.Store.DebugLogEnabled,
			isPresentationTargetCard: card => card?.Owner?.Character is CznHeidemarie.Characters.Heidemarie.Heidemarie,
			configureBeforePatching: () => RunStartPreloadRuntime.Configure(HeidemarieSkinProfile.Preload));
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
		// 战斗音效与角色语音：接「特效音效 / 特效音量 / 角色语音 / 语音音量 / 语音间隔」
		// （音频由 tools/extract_fmod_bank.py 从素材区 .bank 导出，见 HeidemarieCombatAudio）
		HeidemarieCombatAudio.Initialize();
		// 败北 CG：接「败北 CG（关闭 / 类型 1 / 类型 2 / 随机）」（见 HeidemarieDefeatCgRuntime）
		HeidemarieDefeatCgRuntime.Initialize();

		Log.Info("海德玛丽框架接入完成。");
	}

	private static void TryInjectSettingsUi(NSettingsScreen screen)
	{
		SkinSettingsUi.TryInject(screen, SettingsUiRuntime, SettingsGlobalRuntime, SettingsUiTextRuntime);
	}
}
