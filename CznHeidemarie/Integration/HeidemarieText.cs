using CznCore.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 选人界面设置面板用到的全部文案。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.UiText</c>。
/// 这里只声明本项目真正提供的设置项 —— 角色语音与多套角色背景没有对应素材，
/// 就不放对应文案，避免面板上出现按了没反应的开关。</para>
/// </summary>
internal static class HeidemarieText
{
	/// <summary>面板标题。</summary>
	public static readonly SettingsLocalizedText SettingsHeader =
		SettingsLocalization.Text("heidemarie_settings_header", "海德玛丽");

	/// <summary>角色类型说明（显示在面板副标题）。</summary>
	public static readonly SettingsLocalizedText CharacterType =
		SettingsLocalization.Text("heidemarie_character_type", "独立角色");

	/// <summary>战斗演出模式：完整。</summary>
	public static readonly SettingsLocalizedText CombatPresentationFull =
		SettingsLocalization.Text("heidemarie_combat_full", "完整");

	/// <summary>战斗演出模式：精简。</summary>
	public static readonly SettingsLocalizedText CombatPresentationFast =
		SettingsLocalization.Text("heidemarie_combat_fast", "精简");

	/// <summary>战斗演出模式：自定义。</summary>
	public static readonly SettingsLocalizedText CombatPresentationCustom =
		SettingsLocalization.Text("heidemarie_combat_custom", "自定义");

	/// <summary>战斗演出模式说明。</summary>
	public static readonly SettingsLocalizedText CombatPresentationModeDescription =
		SettingsLocalization.Text(
			"heidemarie_combat_mode_description",
			"完整：所有演出；精简：只保留必要动画；自定义：单独控制各项。");

	/// <summary>选人面板里「战斗演出」这一行的标题。</summary>
	public static readonly SettingsLocalizedText CharacterSelectCombatToolbarTitle =
		SettingsLocalization.Text("heidemarie_combat_toolbar_title", "战斗演出");

	/// <summary>出牌特写开关标题。</summary>
	public static readonly SettingsLocalizedText CardPlayPresentationEnabled =
		SettingsLocalization.Text("heidemarie_card_play_enabled", "出牌特写立绘");

	/// <summary>出牌特写开关说明。</summary>
	public static readonly SettingsLocalizedText CardPlayPresentationTooltip =
		SettingsLocalization.Text(
			"heidemarie_card_play_tooltip",
			"打出手牌时，在屏幕左侧弹出角色特写。");

	/// <summary>「战斗音效」开关的标题（面板「战斗特效」分区用）。</summary>
	public static readonly SettingsLocalizedText CombatSfxEnabled =
		SettingsLocalization.Text("combat_sfx_enabled", "Effect Sounds");

	/// <summary>「语音音量」滑条的标题。</summary>
	public static readonly SettingsLocalizedText VoiceVolume =
		SettingsLocalization.Text("voice_volume", "Voice Volume");

	/// <summary>设置面板用的文本集合。</summary>
	/// <remarks>
	/// 工厂签名是 <c>Create(text, combatSfxEnabled, voiceVolume)</c>：
	/// 后两个参数分别是「战斗音效」与「语音音量」两项的标题，不是角色名与类型 ——
	/// 传错会让面板上这两项显示成「海德玛丽 / 独立角色」。
	/// </remarks>
	public static readonly SkinSettingsUiTextProfile SettingsUi =
		SkinSettingsUiTextFactory.Create(SettingsLocalization.Text, CombatSfxEnabled, VoiceVolume);
}
