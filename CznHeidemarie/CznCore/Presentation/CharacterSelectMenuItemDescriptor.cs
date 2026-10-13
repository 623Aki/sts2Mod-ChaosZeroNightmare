using System;

namespace CznCore.Presentation;

internal abstract record CharacterSelectMenuItemDescriptor(string Id, Func<string> Title, Func<string> Tooltip);
