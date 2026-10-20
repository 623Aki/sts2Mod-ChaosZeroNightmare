using System;
using System.Collections.Generic;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionChoice(string Id, int Order, Func<string> Label, Func<string> Description, Func<int> Read, Action<int> Write, Func<bool> Enabled, IReadOnlyList<SettingsUiExtensionChoiceOption> Options) : SettingsUiExtensionControl(Id, Order, Label, Description, Enabled);
