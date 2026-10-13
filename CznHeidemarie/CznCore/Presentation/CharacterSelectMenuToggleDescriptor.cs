using System;

namespace CznCore.Presentation;

internal sealed record CharacterSelectMenuToggleDescriptor(string Id, Func<string> Title, Func<string> Tooltip, Func<bool> Value, Action<bool> SetValue) : CharacterSelectMenuItemDescriptor(Id, Title, Tooltip);
