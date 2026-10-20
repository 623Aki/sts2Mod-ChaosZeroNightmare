using System;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionToggle(string Id, int Order, Func<string> Label, Func<string> Description, Func<bool> Read, Action<bool> Write, Func<bool> Enabled) : SettingsUiExtensionControl(Id, Order, Label, Description, Enabled);
