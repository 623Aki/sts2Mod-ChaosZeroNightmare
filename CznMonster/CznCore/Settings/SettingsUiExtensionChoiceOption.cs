using System;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionChoiceOption(int Id, Func<string> Label);
