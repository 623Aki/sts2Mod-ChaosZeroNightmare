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
			SkinSettingsUi.TryInjectShared(screen, Text);
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 设置面板注入失败：{ex.GetType().Name}: {ex.Message}");
		}
	}
}
