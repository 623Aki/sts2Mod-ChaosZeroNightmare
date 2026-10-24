using CznCore.Settings;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace CznMonster.Registration;

/// <summary>
/// P1 共享设置面板注入（设计文档 §4.4）：
/// 两处 postfix 幂等，失败只警告不影响游戏（P1 降级路径）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
internal static class SettingsReadyPatch
{
	private static void Postfix(NSettingsScreen __instance)
	{
		SettingsInjectPatch.TryInjectSettingsUi(__instance);
	}
}

[HarmonyLib.HarmonyPatch(typeof(NSettingsScreen), "OnSubmenuOpened")]
internal static class SettingsSubmenuPatch
{
	private static void Postfix(NSettingsScreen __instance)
	{
		SettingsInjectPatch.TryInjectSettingsUi(__instance);
	}
}

internal static class SettingsInjectPatch
{
	/// <summary>
	/// 「全局设置」导航按钮图标（中性选项图标，与参考 mod 同一张图）。
	/// 本 mod 自带一份，缺图时框架会降级成「ALL」文字。
	/// </summary>
	private const string GlobalSettingsIconPath = "res://images/ui/mod_config/btn_option.png";

	// 文案对象无状态，可复用；SkinSettingsUiTextFactory 只负责拼默认文案
	private static MonsterSettingsText? _text;

	internal static MonsterSettingsText Text => _text ??= new MonsterSettingsText(
		SkinSettingsUiTextFactory.Create(
			(string key, string fallback) => SettingsLocalization.Text(key, fallback),
			SettingsLocalization.Text("combat_sfx_enabled", "Effect Sounds"),
			SettingsLocalization.Text("voice_volume", "Character Voice Volume")));

	internal static void TryInjectSettingsUi(NSettingsScreen screen)
	{
		try
		{
			SkinSettingsUi.TryInjectShared(screen, Text, GlobalSettingsIconPath);
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 设置面板注入失败：{ex.GetType().Name}: {ex.Message}");
		}
	}
}
