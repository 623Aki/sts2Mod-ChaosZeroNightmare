using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed record CharacterSelectPresentationProfile(string LiveNodeName, string OriginalBgNodeName, string CustomBgNodeName, string ToggleButtonNodeName, string StaticScenePath, string LiveScenePath, string LoopAnimationName, Vector2 ToggleButtonMinimumSize, Vector2 ToggleButtonPosition, IReadOnlyList<CharacterSelectBackgroundPresentationProfile>? BackgroundOptions = null, string CombatPresentationIconPath = "", string CharacterBackgroundIconPath = "", string SettingsAvatarPath = "", string MapMarkerIconPath = "", string MainMenuThumbnailPath = "", string LobbyMusicEventPath = "")
{
	public string EffectiveMapMarkerIconPath
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(MapMarkerIconPath))
			{
				return MapMarkerIconPath;
			}
			return SettingsAvatarPath;
		}
	}

	public string LiveLobbyScenePath
	{
		get
		{
			IReadOnlyList<CharacterSelectBackgroundPresentationProfile> supportedBackgroundOptions = SupportedBackgroundOptions;
			for (int i = 0; i < supportedBackgroundOptions.Count; i++)
			{
				if (supportedBackgroundOptions[i].Variant == CharacterSelectBackgroundVariant.LiveLobby && !string.IsNullOrWhiteSpace(supportedBackgroundOptions[i].ScenePath))
				{
					return supportedBackgroundOptions[i].ScenePath;
				}
			}
			return LiveScenePath;
		}
	}

	public IReadOnlyList<CharacterSelectBackgroundPresentationProfile> SupportedBackgroundOptions
	{
		get
		{
			IReadOnlyList<CharacterSelectBackgroundPresentationProfile> backgroundOptions = BackgroundOptions;
			if (backgroundOptions != null && backgroundOptions.Count > 0)
			{
				return BackgroundOptions;
			}
			return new _003C_003Ez__ReadOnlyArray<CharacterSelectBackgroundPresentationProfile>(new CharacterSelectBackgroundPresentationProfile[2]
			{
				new CharacterSelectBackgroundPresentationProfile(CharacterSelectBackgroundVariant.StaticPortrait, StaticScenePath),
				new CharacterSelectBackgroundPresentationProfile(CharacterSelectBackgroundVariant.LiveLobby, LiveScenePath, ForceLoopAnimation: true)
			});
		}
	}

	public IReadOnlyList<CharacterSelectBackgroundVariant> SupportedBackgroundVariants
	{
		get
		{
			IReadOnlyList<CharacterSelectBackgroundPresentationProfile> supportedBackgroundOptions = SupportedBackgroundOptions;
			CharacterSelectBackgroundVariant[] array = new CharacterSelectBackgroundVariant[supportedBackgroundOptions.Count];
			for (int i = 0; i < supportedBackgroundOptions.Count; i++)
			{
				array[i] = supportedBackgroundOptions[i].Variant;
			}
			return array;
		}
	}

	public CharacterSelectBackgroundPresentationProfile GetBackgroundOptionOrDefault(CharacterSelectBackgroundVariant variant)
	{
		IReadOnlyList<CharacterSelectBackgroundPresentationProfile> supportedBackgroundOptions = SupportedBackgroundOptions;
		for (int i = 0; i < supportedBackgroundOptions.Count; i++)
		{
			if (supportedBackgroundOptions[i].Variant == variant)
			{
				return supportedBackgroundOptions[i];
			}
		}
		if (supportedBackgroundOptions.Count > 0)
		{
			return supportedBackgroundOptions[0];
		}
		throw new InvalidOperationException("Character select background profile has no supported options.");
	}

	public CharacterSelectBackgroundVariant NormalizeVariant(CharacterSelectBackgroundVariant variant)
	{
		return GetBackgroundOptionOrDefault(variant).Variant;
	}

	public CharacterSelectBackgroundVariant NextVariant(CharacterSelectBackgroundVariant current)
	{
		IReadOnlyList<CharacterSelectBackgroundPresentationProfile> supportedBackgroundOptions = SupportedBackgroundOptions;
		if (supportedBackgroundOptions.Count == 0)
		{
			return CharacterSelectBackgroundVariant.StaticPortrait;
		}
		for (int i = 0; i < supportedBackgroundOptions.Count; i++)
		{
			if (supportedBackgroundOptions[i].Variant == current)
			{
				return supportedBackgroundOptions[(i + 1) % supportedBackgroundOptions.Count].Variant;
			}
		}
		return supportedBackgroundOptions[0].Variant;
	}
}
