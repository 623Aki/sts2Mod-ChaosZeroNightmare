namespace CznCore.Presentation;

internal interface ICharacterSkinPresentationSettings : ICombatPresentationAudienceSettings
{
	string CharacterId { get; }

	bool EffectiveEffectsEnabled { get; }

	bool EffectiveParticlesEnabled { get; }

	bool EffectiveCombatSfxEnabled { get; }

	float EffectBrightness { get; }

	float EffectOpacity { get; }

	float BattleReadyScale { get; }

	float BattleReadyOpacity { get; }

	float BattleReadyOffsetX { get; }

	float BattleReadyOffsetY { get; }

	bool ShowBattleReadyOverlay { get; }

	LeftSideCardPresentationMode BattleReadyOverlayMode { get; }

	bool ShowCardPlayPresentation { get; }

	AttackFullScreenPresentationMode AttackFullScreenPresentationMode { get; }

	CardSelectionPresentationTrigger CardSelectionPresentationTrigger { get; }

	bool ShowDefeatCgOverlay { get; }

	DefeatCgOverlayMode DefeatCgOverlayMode { get; }
}
