using System;

namespace CznCore.Presentation;

internal sealed record CharacterSelectMenuActionDescriptor(string Id, string IconPath, Func<string> Title, Func<string> State, Func<string> Tooltip, Action Activate, bool Accent = false) : CharacterSelectMenuItemDescriptor(Id, Title, Tooltip);
