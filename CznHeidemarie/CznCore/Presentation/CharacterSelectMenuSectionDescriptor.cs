using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CharacterSelectMenuSectionDescriptor(string Id, Func<string> Title, IReadOnlyList<CharacterSelectMenuItemDescriptor> Items);
