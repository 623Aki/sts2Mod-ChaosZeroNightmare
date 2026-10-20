using CznCore.Presentation;
using CznCore.Settings;

namespace CznMonster.Settings;

/// <summary>
/// 设置面板文案的本地化读取入口（设计文档 §4.6）：
/// 文案文件按 <c>res://CznMonster/localization/{语言}/settings.json</c> 查找。
/// </summary>
internal static class SettingsLocalization
{
	private static readonly SettingsLocalizationStore Store = new SettingsLocalizationStore(
		"res://CznMonster/localization/{0}/settings.json",
		(string message, int _) => SkinLog.Warn(message));

	/// <summary>取一条设置文案；找不到对应语言条目时回退到 <paramref name="fallback"/>。</summary>
	public static SettingsLocalizedText Text(string key, string fallback)
	{
		return new SettingsLocalizedText(Store, key, fallback);
	}
}
