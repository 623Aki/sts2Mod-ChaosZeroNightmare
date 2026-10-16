using System;
using System.Collections.Generic;
using CznCore.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「连接动画」的设置入口。
///
/// <para>这套手牌连接光带（见 <see cref="HeidemarieLinkOverlay"/>）在 2026-10-16 定稿并<b>解封</b>：
/// 开关改成持久化的角色设置项 <c>LinkOverlayEnabled</c>，注入到角色设置面板的
/// 「战斗特效」大类下，玩家可以随时开 / 关。</para>
///
/// <para>面板上的开关走 CznCore 的 <c>CharacterSettingsExtensions</c> 扩展点，
/// <b>没有改动框架本身的设置项</b>；改动会立刻生效 ——
/// 写值后由 <c>SkinSettingsUiRuntimeHooks.CleanupSkinVisualEffectsRuntime</c>
/// 回调 <see cref="HeidemarieLinkOverlay.Refresh"/> 补挂 / 收掉视觉层。</para>
///
/// <para>另外还会与角色的「战斗特效」总开关相与：总开关关掉时本功能一并关闭。</para>
/// </summary>
internal static class HeidemarieLinkOverlaySettings
{
	/// <summary>设置扩展的提供者 id（同时也是开关的节点名前缀）。</summary>
	private const string ProviderId = "CznHeidemarie.LinkOverlay";

	/// <summary>
	/// 连接动画开关（持久化的角色设置项）。
	///
	/// <para>读取失败时返回 <c>false</c>：宁可少画一层，也不要因为读设置崩掉战斗 UI。</para>
	/// </summary>
	internal static bool Enabled
	{
		get
		{
			try
			{
				return HeidemarieSettings.CurrentCharacter.LinkOverlayEnabled;
			}
			catch (Exception ex)
			{
				HeidemarieIntegration.Log.Warn("读取连接动画开关失败：" + ex.Message);
				return false;
			}
		}
	}

	/// <summary>
	/// 注入到角色设置面板的扩展分组（一项「显示连接动画」开关）。
	///
	/// <para>走 CznCore 的 <c>CharacterSettingsExtensions</c> 扩展点，不必改框架代码；
	/// 分组会出现在角色的「战斗特效」大类下。</para>
	/// </summary>
	internal static readonly IReadOnlyList<SettingsUiExtensionGroup> Extensions = new SettingsUiExtensionGroup[]
	{
		new(
			ProviderId: ProviderId,
			CategoryId: CharacterSettingsCategoryIds.Effects,
			Order: 100,
			Label: () => HeidemarieText.LinkOverlaySection.Current,
			Reset: null,
			Controls: new SettingsUiExtensionControl[] { CreateToggle() }),
	};

	private static SettingsUiExtensionControl CreateToggle()
	{
		return new SettingsUiExtensionToggle(
			Id: ProviderId + ".Enabled",
			Order: 0,
			Label: () => HeidemarieText.LinkOverlayEnabled.Current,
			Description: () => HeidemarieText.LinkOverlayDescription.Current,
			Read: () => Enabled,
			Write: OnToggleWrite,
			Enabled: () => true);
	}

	/// <summary>写入开关（持久化到角色设置）。</summary>
	/// <param name="value">面板传来的目标值。</param>
	private static void OnToggleWrite(bool value)
	{
		try
		{
			HeidemarieSettings.CurrentCharacter.SetLinkOverlayEnabled(value, persist: true);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("写入连接动画开关失败：" + ex.Message);
		}
	}
}
