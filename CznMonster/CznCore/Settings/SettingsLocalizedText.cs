using CznCore.Presentation;

namespace CznCore.Settings;

internal sealed record SettingsLocalizedText(SettingsLocalizationStore Store, string Key, string Fallback) : ILocalizedText
{
	public string Current => Store.Get(Key, Fallback);
}
