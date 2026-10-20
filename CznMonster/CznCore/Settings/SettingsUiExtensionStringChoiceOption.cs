using System;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionStringChoiceOption(string Id, Func<string> Label);
