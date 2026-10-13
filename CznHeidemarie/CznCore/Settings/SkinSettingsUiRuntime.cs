using System;
using System.Collections.Generic;
using CznCore.Presentation;

namespace CznCore.Settings;

internal sealed class SkinSettingsUiRuntime : SkinSettingsUiRuntimeBase
{
	private readonly CharacterSkinProfile _profile;

	private readonly IReadOnlyList<SettingsUiExtensionGroup> _extensions;

	public override string CharacterSettingsKey => _profile.ProfileId;

	public override string CharacterSettingsHeader => _profile.SettingsText.SettingsHeader.Current;

	public override string CharacterSkinTarget => _profile.SettingsText.SkinTarget.Current;

	public override string CharacterSettingsAvatarPath => ResolveCharacterSettingsAvatarPath();

	public override string GlobalSettingsIconPath => _profile.GlobalSettingsIconPath;

	public override string CharacterEffectsLabel => _profile.SettingsText.EffectsLabel.Current;

	public override string CharacterVoiceLabel => _profile.SettingsText.VoiceLabel.Current;

	public override bool SupportsMeleeApproachMovement => _profile.Features.MeleeApproachMovement;

	public override bool SupportsOfficialAttackTiming => _profile.Features.OfficialAttackTiming;

	public override IReadOnlyList<CharacterSelectBackgroundVariant> SupportedCharacterSelectBackgroundVariants => _profile.CharacterSelectPresentation.SupportedBackgroundVariants;

	public override IReadOnlyList<SettingsUiExtensionGroup> CharacterSettingsExtensions => _extensions;

	public SkinSettingsUiRuntime(CharacterSkinProfile profile, SkinCharacterSettings settings, SkinSettingsUiRuntimeHooks? hooks = null, IReadOnlyList<SettingsUiExtensionGroup>? characterSettingsExtensions = null)
		: base(settings, hooks)
	{
		_profile = profile;
		_extensions = characterSettingsExtensions ?? Array.Empty<SettingsUiExtensionGroup>();
	}

	protected override CharacterSelectBackgroundVariant NormalizeBackgroundVariant(CharacterSelectBackgroundVariant variant)
	{
		return _profile.CharacterSelectPresentation.NormalizeVariant(variant);
	}

	private string ResolveCharacterSettingsAvatarPath()
	{
		if (!string.IsNullOrWhiteSpace(_profile.CharacterSelectPresentation.SettingsAvatarPath))
		{
			return _profile.CharacterSelectPresentation.SettingsAvatarPath;
		}
		if (!string.IsNullOrWhiteSpace(_profile.CharacterSelectPresentation.CharacterBackgroundIconPath))
		{
			return _profile.CharacterSelectPresentation.CharacterBackgroundIconPath;
		}
		return _profile.CharacterSelectPresentation.CombatPresentationIconPath;
	}
}
