using System;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznHeidemarie.Integration;

/// <summary>
/// 战斗中右侧的「全屏演出」开关。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.Entry.ConfigureCombatCutInToggleHud</c>：
/// 战斗界面右下角（弃牌堆上方）会出现一个圆形图标按钮，
/// 点一下就在「全屏演出 开 / 关」之间切换，并把选择持久化到角色设置里。
/// 图标本身来自 CznCore 的 <see cref="CombatCutInToggleHudRuntime"/>，
/// 位置、悬停提示、显隐时机都由它负责，本类只提供值。</para>
///
/// <para>开关读写的就是设置里的「你的全屏角色演出」
/// （<c>ShowCardPlayPresentation</c>）—— 和设置面板里那一项是同一个值，
/// 在战斗里点按钮等价于改设置；点按钮时同时把「战斗演出模式」置为自定义，
/// 免得被预设整体覆盖（与参考 mod 行为一致）。</para>
///
/// <para><b>时序铁律（参考 Nine / Tiphera 的 <c>Entry.ConfigureCombatCutInToggleHud</c>）：</b>
/// 必须<b>先注册、后打补丁</b>。CznCore 的 4 个 <c>NCombatUi</c> 补丁类都带
/// <c>Prepare() => CombatCutInToggleHudService.IsRegistered</c> 守卫，
/// 若补丁应用时尚未注册，Harmony 会静默跳过整个补丁类，按钮将不再出现。</para>
/// </summary>
internal static class HeidemarieCutInToggleRuntime
{
	/// <summary>开关图标（开 / 关）所在目录。</summary>
	private const string IconRoot = "res://images/ui/";

	/// <summary>角色本地化表名；命名键（<c>HEIDEMARIE.cut_in_on.*</c>）也放在这张表里。</summary>
	private const string HoverTipLocTable = "characters";

	/// <summary>运行时节点名前缀，避免与游戏及其它 mod 的节点撞名。</summary>
	private const string RuntimeNodeNamePrefix = "CznHeidemarie";

	private static bool _registered;

	/// <summary>
	/// 注册开关值提供器，并应用它的四个 <c>NCombatUi</c> 补丁。
	/// 由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	/// <param name="harmony">mod 入口创建的 Harmony 实例。</param>
	internal static void Initialize(Harmony harmony)
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		CombatCutInToggleHudProfile profile = new(
			EnabledTexturePath: IconRoot + "cut_in_toggle_on.png",
			DisabledTexturePath: IconRoot + "cut_in_toggle_off.png",
			Size: 64f,
			OffsetRight: -22f,
			OffsetBottom: -116f,
			ButtonNodeNameSuffix: "CombatCutInToggleHud",
			IconNodeNameSuffix: "CombatCutInToggleHudIcon",
			HoverTipLocTable: HoverTipLocTable,
			EnabledHoverTipTitleKey: "HEIDEMARIE.cut_in_on.title",
			EnabledHoverTipDescriptionKey: "HEIDEMARIE.cut_in_on.description",
			DisabledHoverTipTitleKey: "HEIDEMARIE.cut_in_off.title",
			DisabledHoverTipDescriptionKey: "HEIDEMARIE.cut_in_off.description");

		CombatCutInToggleHudService.Register(
			profile,
			isTargetPlayer: static (Player? player) => player?.Character is Heidemarie,
			isAvailable: static () => HeidemarieSettings.CurrentCharacter.EffectiveEffectsEnabled,
			isCutInEnabled: static () => HeidemarieSettings.CurrentCharacter.ShowCardPlayPresentation,
			setCutInEnabled: SetCutInEnabled,
			runtimeNodeName: static (string suffix) => RuntimeNodeNamePrefix + suffix,
			warn: static (string message, int _) => HeidemarieIntegration.Log.Warn("全屏演出开关: " + message),
			debug: static (string message, int _) => HeidemarieIntegration.Log.Info("全屏演出开关: " + message));

		// 注册之后才应用补丁：此时 IsRegistered 已为 true，四个补丁类的 Prepare() 才会放行
		// （与参考 mod Nine / Tiphera 的 ConfigureCombatCutInToggleHud 完全一致）
		int patched = 0;
		foreach (Type patchType in CombatCutInToggleHudPatches.PatchTypes)
		{
			try
			{
				harmony.CreateClassProcessor(patchType).Patch();
				patched++;
			}
			catch (Exception ex)
			{
				// 游戏版本间 NCombatUi 的方法签名可能变化，单个补丁失败不该拖垮整个 mod
				HeidemarieIntegration.Log.Warn("跳过全屏演出开关补丁 " + patchType.Name + "：" + ex.GetType().Name + ": " + ex.Message);
			}
		}

		HeidemarieIntegration.Log.Info("全屏演出开关已注册（补丁 " + patched + "/" + CombatCutInToggleHudPatches.PatchTypes.Count + "）。");
	}

	/// <summary>
	/// 写入开关值：把「战斗演出模式」拉成自定义，再写入「你的全屏角色演出」。
	/// </summary>
	/// <param name="enabled">是否开启全屏演出。</param>
	/// <param name="persist">是否落盘。</param>
	private static void SetCutInEnabled(bool enabled, bool persist)
	{
		HeidemarieSettingsUiRuntime settings = HeidemarieIntegration.SettingsUiRuntime;
		settings.SetCombatPresentationMode(CombatPresentationMode.Custom, persist);
		settings.SetCardPlayPresentationEnabled(enabled, persist);
	}
}
