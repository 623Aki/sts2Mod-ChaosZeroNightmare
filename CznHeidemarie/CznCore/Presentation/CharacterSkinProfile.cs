using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed record CharacterSkinProfile(string ProfileId, string LogPrefix, string RuntimeNodeNamePrefix, Type? TargetCharacterType, string BodyTexturePath, string BodySkeletonDataPath, string CombatSpriteNodeName, float BodySpineScaleMultiplier, Vector2 CombatPositionOffset, CharacterSkinCombatBodyProfile CombatBody, CharacterSkinFormVfxProfile FormVfx, string PremultipliedAlphaMaterialPath, string CombatIdleEffectsScenePath, CharacterSkinCombatEffectsProfile CombatEffects, CharacterSkinFeatureSupportProfile Features, CharacterSkinSettingsTextProfile SettingsText, CombatCardPresentationProfile CardPresentation, ICombatPresentationProfile CombatPresentation, BattleReadyOverlayProfile BattleReady, CharacterSelectIdentityProfile CharacterSelect, CharacterSelectPresentationProfile CharacterSelectPresentation, RestSitePresentationProfile RestSite, DefeatPresentationProfile Defeat, CharacterVoiceProfile Voice, string GlobalSettingsIconPath = "", Func<Type?>? TargetCharacterTypeResolver = null)
{
	private readonly object _targetCharacterTypeSync = new object();

	private Type? _resolvedTargetCharacterType;

	public string RuntimeMetaKeyPrefix => "character_skin_" + ProfileId;

	private volatile bool _targetCharacterTypeResolved;

	public Type? ResolveTargetCharacterType()
	{
		if (TargetCharacterType != null)
		{
			return TargetCharacterType;
		}
		if (_targetCharacterTypeResolved)
		{
			return _resolvedTargetCharacterType;
		}
		lock (_targetCharacterTypeSync)
		{
			if (!_targetCharacterTypeResolved)
			{
				_resolvedTargetCharacterType = TargetCharacterTypeResolver?.Invoke();
				_targetCharacterTypeResolved = true;
			}
			return _resolvedTargetCharacterType;
		}
	}

	public bool AppliesTo(CharacterModel? characterModel)
	{
		Type type = ResolveTargetCharacterType();
		if (characterModel != null && type != null)
		{
			return type.IsInstanceOfType(characterModel);
		}
		return false;
	}

	public bool AppliesTo(Player? player)
	{
		return AppliesTo(player?.Character);
	}

	public string RuntimeNodeName(string suffix)
	{
		return RuntimeNodeNamePrefix + suffix;
	}

	public string RuntimeMetaKey(string suffix)
	{
		return RuntimeMetaKeyPrefix + "_" + suffix;
	}

}
