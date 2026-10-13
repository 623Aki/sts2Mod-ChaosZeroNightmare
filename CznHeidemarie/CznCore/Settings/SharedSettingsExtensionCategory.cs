using System;
using System.Collections.Generic;

namespace CznCore.Settings;

internal sealed record SharedSettingsExtensionCategory(string Id, int Order, Func<string> Label, IReadOnlyList<SettingsUiExtensionGroup> Groups);
