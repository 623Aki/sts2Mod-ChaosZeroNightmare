using System.Collections.Generic;
using CznCore.Presentation;
using CznCore.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 设置 UI 的运行时绑定。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.NineSettingsUiRuntime</c>。
/// 基类 <see cref="SkinSettingsUiRuntimeBase"/> 已经把绝大多数设置项做成了
/// 转发到 <see cref="SkinCharacterSettings"/> 的只读属性，
/// 角色侧只需要提供身份信息（id、标题、头像、图标）与能力声明，
/// 再用 <see cref="SkinSettingsUiRuntimeHooks"/> 把「设置变了要重算什么」接上。</para>
/// </summary>
internal sealed class HeidemarieSettingsUiRuntime : SkinSettingsUiRuntimeBase
{
	/// <summary>
	/// 三种选角背景：静态立绘 / 动态立绘 / 角色大厅。
	/// 对应场景见 <see cref="HeidemarieCharSelectBackgroundRuntime"/>，
	/// 下拉里的显示名取自 settings.json 的 character_select_background_* 三条文案。
	/// </summary>
	private static readonly CharacterSelectBackgroundVariant[] BackgroundVariants =
	{
		CharacterSelectBackgroundVariant.StaticPortrait,
		CharacterSelectBackgroundVariant.TeamJoinPortrait,
		CharacterSelectBackgroundVariant.LiveLobby
	};

	/// <inheritdoc/>
	public override string CharacterSettingsKey => HeidemarieSettings.CurrentCharacterId;

	/// <inheritdoc/>
	public override string CharacterSettingsHeader => HeidemarieText.SettingsHeader.Current;

	/// <inheritdoc/>
	public override string CharacterSkinTarget => HeidemarieText.CharacterType.Current;

	/// <inheritdoc/>
	public override string CharacterSettingsAvatarPath => "res://images/ui/top_panel/character_icon_heidemarie.png";

	/// <inheritdoc/>
	/// <remarks>
	/// 全局设置页的导航按钮用中性「选项」图标，与参考 mod 保持一致；
	/// 角色页才用 <see cref="CharacterSettingsAvatarPath"/> 的角色头像。
	/// 缺图时框架会降级成「ALL」文字，不会报错。
	/// </remarks>
	public override string GlobalSettingsIconPath => "res://images/ui/mod_config/btn_option.png";

	/// <inheritdoc/>
	/// <remarks>本项目没有专属战斗特效资源，标签沿用模板中的通用文案。</remarks>
	public override string CharacterEffectsLabel => "战斗特效";

	/// <inheritdoc/>
	/// <remarks>本项目没有语音资源，面板不会展示这一项。</remarks>
	public override string CharacterVoiceLabel => "角色语音";

	/// <inheritdoc/>
	public override bool SupportsMeleeApproachMovement => false;

	/// <inheritdoc/>
	public override bool SupportsOfficialAttackTiming => true;

	/// <inheritdoc/>
	public override IReadOnlyList<CharacterSelectBackgroundVariant> SupportedCharacterSelectBackgroundVariants =>
		BackgroundVariants;

	/// <inheritdoc/>
	/// <remarks>
	/// 「连接动画」的后门开关从这条扩展点注入（暂不开放，见
	/// <see cref="HeidemarieLinkOverlaySettings"/>）；走框架的
	/// <c>SettingsUiExtensionGroup</c>，不必改动 CznCore 本身的设置项。
	/// </remarks>
	public override IReadOnlyList<SettingsUiExtensionGroup> CharacterSettingsExtensions =>
		HeidemarieLinkOverlaySettings.Extensions;

	public HeidemarieSettingsUiRuntime(SkinCharacterSettings settings)
		: base(settings, new SkinSettingsUiRuntimeHooks(
			// 「特效亮度 / 特效不透明度」：对当前在画面上的特效重算一次 modulate
			ApplyEffectBrightnessSettings: HeidemarieEffectMaterials.Refresh,
			ApplyBattleReadyTransform: HeidemarieBattleReadyRuntime.ApplyTransform,
			CleanupBattleReadyOverlay: () => HeidemarieBattleReadyRuntime.Cleanup(),
			// 「战斗特效」关掉：正在播的单体特效与全屏演出立刻收尾
			CleanupSkinEffectsRuntime: () =>
			{
				HeidemarieMoveEffectsRuntime.CancelAll();
				HeidemarieCinematicRuntime.CancelAll();
			},
			// 「额外粒子」关掉：卡牌拖尾的两个粒子节点停止发射。
			// 顺带刷新手牌「连接」光带：它的开关 / 粒子口径都挂在这条回调上，
			// 面板里一改就立刻生效（不必等下一场战斗）。
			CleanupSkinVisualEffectsRuntime: () =>
			{
				HeidemarieTrailParticles.Refresh();
				HeidemarieLinkOverlay.Refresh();
			},
			// 「特效音效」关掉：清掉音效播放器缓存并停掉正在播的
			CleanupSkinSfxRuntime: HeidemarieCombatAudio.TrimSfx,
			// 「角色语音」关掉：停掉正在播的语音
			StopVoice: HeidemarieCombatAudio.StopVoice,
			// 关掉「出牌演出」时，出牌特写与全屏演出都要立刻收尾
			ApplyCardPlayPresentationEnabled: enabled =>
			{
				HeidemarieBattleReadyRuntime.ApplyCardPlayPresentation(enabled);
				HeidemarieCinematicRuntime.ApplyCardPlayPresentation(enabled);
			},
			DebugLog: (string message, int _) => HeidemarieIntegration.Log.Info(message)))
	{
	}

	/// <inheritdoc/>
	protected override CharacterSelectBackgroundVariant NormalizeBackgroundVariant(CharacterSelectBackgroundVariant variant)
	{
		// 只认声明过的三种变体，其余一律回落到静态立绘
		return variant is CharacterSelectBackgroundVariant.StaticPortrait
			or CharacterSelectBackgroundVariant.TeamJoinPortrait
			or CharacterSelectBackgroundVariant.LiveLobby
			? variant
			: CharacterSelectBackgroundVariant.StaticPortrait;
	}
}
