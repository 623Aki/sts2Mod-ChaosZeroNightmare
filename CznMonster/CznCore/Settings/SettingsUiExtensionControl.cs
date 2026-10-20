using System;

namespace CznCore.Settings;

internal abstract record SettingsUiExtensionControl(string Id, int Order, Func<string> Label, Func<string> Description, Func<bool> Enabled)
{
	public string? ParentId { get; init; }
}
