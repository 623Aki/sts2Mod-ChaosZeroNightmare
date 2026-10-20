using System;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionSlider(string Id, int Order, Func<string> Label, Func<string> Description, Func<double> Read, Action<double> Write, Func<bool> Enabled, double Min, double Max, double Step, Func<string> Suffix, double DisplayOffset) : SettingsUiExtensionControl(Id, Order, Label, Description, Enabled);
