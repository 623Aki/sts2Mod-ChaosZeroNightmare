using System;
using System.Collections.Generic;
using CznCore.Presentation;
using CznCore.Settings;
using CznHeidemarie.Characters.Heidemarie;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CznHeidemarie.Integration;

/// <summary>
/// 选人界面左侧中间的角色设置抽屉。
///
/// <para>对应参考 mod（Nine）的 <c>Entry.CharacterSelectMenuPatch</c>：
/// 选中本角色时挂上 <see cref="CharacterSelectSettingsMenu"/>，分成「角色界面」一组，
/// 里面放 战斗演出 / 角色背景 / 战斗特效 三个控件；选中别的角色时把自己那套摘掉，
/// 与其它 mod 的同名抽屉互不覆盖（用 <see cref="HeidemarieIntegration"/> 侧的 ownerId 区分）。</para>
///
/// <para>抽屉本体的位置由 CznCore 固定为屏幕左侧居中
/// （<c>LayoutPreset.CenterLeft</c>，尺寸 456x438）。</para>
///
/// <para>本项目没有语音素材，因此不提供参考 mod 里的「角色语音」开关。</para>
/// </summary>
internal static class HeidemarieCharSelectMenuRuntime
{
	/// <summary>抽屉归属标记，避免与其它 mod 的抽屉互相顶掉。</summary>
	private const string OwnerId = "CznHeidemarie";

	/// <summary>「战斗演出」一行的图标。</summary>
	private const string CombatIconPath = "res://CznHeidemarie/images/character_select/icon_large_battle.png";

	/// <summary>「角色背景」一行的图标。</summary>
	private const string BackgroundIconPath = "res://CznHeidemarie/images/character_select/icon_large_lobby.png";

	/// <summary>事件订阅句柄，保持引用以防被回收。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>
	/// 订阅选人事件。由 <see cref="HeidemarieIntegration.Init"/> 在补丁应用后调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 选人菜单", (CharacterSelectedEvent e) =>
		{
			if (!GodotObject.IsInstanceValid(e.Screen))
			{
				return;
			}

			if (e.Character is not Heidemarie)
			{
				// 选中了别的角色：把自己那套抽屉摘掉
				CharacterSelectSettingsMenu.RemoveOwned(e.Screen, OwnerId);
				return;
			}

			Attach(e.Screen, e.Character);
		}));
	}

	/// <summary>
	/// 挂上（或刷新）角色设置抽屉。
	/// </summary>
	/// <param name="screen">选人界面。</param>
	/// <param name="character">当前选中的角色，用于切换背景后立刻刷新。</param>
	private static void Attach(NCharacterSelectScreen screen, CharacterModel character)
	{
		HeidemarieSettingsUiRuntime settings = HeidemarieIntegration.SettingsUiRuntime;
		SkinSettingsUiTextProfile text = HeidemarieText.SettingsUi;

		CharacterSelectSettingsMenu.Attach(
			screen,
			OwnerId,
			HeidemarieText.SettingsHeader.Current,
			new List<CharacterSelectMenuSectionDescriptor>(1)
			{
				new(
					"character_screen",
					() => text.VisualSettingsHeader.Current,
					new List<CharacterSelectMenuItemDescriptor>(3)
					{
						// 战斗演出：点击在 完整 / 快速 / 自定义 之间循环
						new CharacterSelectMenuActionDescriptor(
							"combat_presentation",
							CombatIconPath,
							() => text.CharacterSelectCombatToolbarTitle.Current,
							() => settings.CombatPresentationMode switch
							{
								CombatPresentationMode.Fast => text.CombatPresentationFast.Current,
								CombatPresentationMode.Custom => text.CombatPresentationCustom.Current,
								_ => text.CombatPresentationFull.Current,
							},
							() => text.CombatPresentationModeDescription.Current,
							() => settings.SetCombatPresentationMode(
								NextCombatPresentationMode(settings.CombatPresentationMode),
								persist: true)),

						// 角色背景：点击在 立绘 / 动态立绘 / 角色大厅 之间循环，并立刻刷新背景
						new CharacterSelectMenuActionDescriptor(
							"character_background",
							BackgroundIconPath,
							() => text.CharacterSelectBackgroundToolbarTitle.Current,
							() => BackgroundLabel(text, settings.CharacterSelectBackgroundVariant),
							() => text.CharacterSelectBackgroundDescription.Current,
							() =>
							{
								settings.SetCharacterSelectBackgroundVariant(
									NextBackgroundVariant(settings.CharacterSelectBackgroundVariant),
									persist: true);
								HeidemarieCharSelectBackgroundRuntime.Refresh(screen, character);
							},
							Accent: true),

						// 战斗特效总开关
						new CharacterSelectMenuToggleDescriptor(
							"combat_effects",
							() => text.CombatEffectsHeader.Current,
							() => text.EffectsEnabledDescription.Current,
							() => settings.EffectsEnabled,
							value => settings.SetEffectsEnabled(value, persist: true)),
					})
			});
	}

	/// <summary>战斗演出模式的三态循环。</summary>
	private static CombatPresentationMode NextCombatPresentationMode(CombatPresentationMode mode)
	{
		return mode switch
		{
			CombatPresentationMode.Full => CombatPresentationMode.Fast,
			CombatPresentationMode.Fast => CombatPresentationMode.Custom,
			_ => CombatPresentationMode.Full,
		};
	}

	/// <summary>角色背景的三态循环。</summary>
	private static CharacterSelectBackgroundVariant NextBackgroundVariant(CharacterSelectBackgroundVariant variant)
	{
		return variant switch
		{
			CharacterSelectBackgroundVariant.StaticPortrait => CharacterSelectBackgroundVariant.TeamJoinPortrait,
			CharacterSelectBackgroundVariant.TeamJoinPortrait => CharacterSelectBackgroundVariant.LiveLobby,
			_ => CharacterSelectBackgroundVariant.StaticPortrait,
		};
	}

	/// <summary>把背景分类翻译成抽屉上显示的短标签。</summary>
	private static string BackgroundLabel(SkinSettingsUiTextProfile text, CharacterSelectBackgroundVariant variant)
	{
		return variant switch
		{
			CharacterSelectBackgroundVariant.TeamJoinPortrait => text.CharacterSelectBackgroundTeamJoinPortrait.Current,
			CharacterSelectBackgroundVariant.LiveLobby => text.CharacterSelectBackgroundLiveLobby.Current,
			_ => text.CharacterSelectBackgroundStaticPortrait.Current,
		};
	}
}
