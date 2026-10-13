using System;
using System.Collections.Generic;
using CznCore.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed record CharacterSelectPresentationRuntimeOptions(ILocalizedText? SwitchBackgroundText = null, SkinSettingsUiTextProfile? SettingsText = null, Func<Node, string, bool>? TryPrepareLoopSpine = null, Func<Player?>? VoiceOwner = null, Func<IReadOnlyList<CharacterSelectMenuSectionDescriptor>>? AdditionalMenuSections = null);
