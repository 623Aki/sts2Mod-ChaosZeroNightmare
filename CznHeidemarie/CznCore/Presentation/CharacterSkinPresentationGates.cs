using System;

namespace CznCore.Presentation;

internal sealed class CharacterSkinPresentationGates
{
	private readonly CharacterSkinFeatureSupportProfile _features;

	private ICharacterSkinPresentationSettings _settings;

	public bool EffectsEnabled => _settings.EffectiveEffectsEnabled;

	public bool BattleReadyOverlayDisplayEnabled
	{
		get
		{
			if (_features.BattleReadyOverlay)
			{
				return _settings.BattleReadyOverlayMode != LeftSideCardPresentationMode.Off;
			}
			return false;
		}
	}

	public bool CardPlayPresentationEnabled => _settings.ShowCardPlayPresentation;

	public bool CutInPresentationRuntimeEnabled
	{
		get
		{
			if (!_settings.ShowCardPlayPresentation)
			{
				if (_settings.TeammatePresentationEnabled)
				{
					return _settings.TeammateCutInEnabled;
				}
				return false;
			}
			return true;
		}
	}

	public AttackFullScreenPresentationMode AttackFullScreenPresentationMode => _settings.AttackFullScreenPresentationMode;

	public AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode => _settings.TeammateAttackFullScreenPresentationMode;

	public bool DefeatCgOverlayDisplayEnabled
	{
		get
		{
			if (_features.DeathCgOverlay)
			{
				return _settings.DefeatCgOverlayMode != DefeatCgOverlayMode.Off;
			}
			return false;
		}
	}

	public bool CombatBodyPresentationEnabled => _features.CombatEntryAnimation;

	public bool CombatPresentationEffectsEnabled
	{
		get
		{
			if (CombatBodyPresentationEnabled)
			{
				return EffectsEnabled;
			}
			return false;
		}
	}

	public bool CombatParticlesEnabled
	{
		get
		{
			if (CombatPresentationEffectsEnabled)
			{
				return _settings.EffectiveParticlesEnabled;
			}
			return false;
		}
	}

	public bool CombatSfxEnabled => _settings.EffectiveCombatSfxEnabled;

	public bool CombatPresentationSfxEnabled
	{
		get
		{
			if (CombatBodyPresentationEnabled)
			{
				return CombatSfxEnabled;
			}
			return false;
		}
	}

	public bool CombatIdleEffectsEnabled
	{
		get
		{
			if (_features.CombatIdleEffects)
			{
				return EffectsEnabled;
			}
			return false;
		}
	}

	public CharacterSkinPresentationGates(CharacterSkinFeatureSupportProfile features, ICharacterSkinPresentationSettings settings)
	{
		_features = features;
		_settings = settings ?? throw new ArgumentNullException("settings");
	}

	public void Configure(ICharacterSkinPresentationSettings settings)
	{
		_settings = settings ?? throw new ArgumentNullException("settings");
	}
}
