using CznCore.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 设置面板文案的本地化读取入口。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.SettingsLocalization</c>：
/// 只把 <see cref="SettingsLocalizationStore"/> 的构造参数换成本 mod 的路径。
/// 文案文件按 <c>res://CznHeidemarie/localization/{语言}/settings.json</c> 查找。</para>
/// </summary>
internal static class SettingsLocalization
{
	private static readonly SettingsLocalizationStore Store = new SettingsLocalizationStore(
		"res://CznHeidemarie/localization/{0}/settings.json",
		(string message, int _) => HeidemarieIntegration.Log.Warn(message));

	/// <summary>取一条设置文案；找不到对应语言条目时回退到 <paramref name="fallback"/>。</summary>
	public static SettingsLocalizedText Text(string key, string fallback)
	{
		return new SettingsLocalizedText(Store, key, fallback);
	}
}
