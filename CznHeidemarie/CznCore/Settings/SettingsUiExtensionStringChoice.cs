using System;
using System.Collections.Generic;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionStringChoice(string Id, int Order, Func<string> Label, Func<string> Description, Func<string> Read, Action<string> Write, Func<bool> Enabled, IReadOnlyList<SettingsUiExtensionStringChoiceOption> Options) : SettingsUiExtensionControl(Id, Order, Label, Description, Enabled);
