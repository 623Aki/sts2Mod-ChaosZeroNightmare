using System;
using System.Collections.Generic;

namespace CznCore.Settings;

internal sealed record SettingsUiExtensionGroup(string ProviderId, string CategoryId, int Order, Func<string> Label, Action? Reset, IReadOnlyList<SettingsUiExtensionControl> Controls);
