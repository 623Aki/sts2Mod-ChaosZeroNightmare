using CznCore.Presentation;

namespace CznCore.Settings;

internal sealed class CharacterSkinCharacterSettings
{
	public bool EffectsEnabled { get; set; } = true;

	public bool ParticlesEnabled { get; set; } = true;

	public bool CombatSfxEnabled { get; set; } = true;

	public bool VoiceEnabled { get; set; } = true;

	public float EffectVolume { get; set; } = 1f;

	public float EffectBrightness { get; set; } = 1f;

	public float EffectOpacity { get; set; } = 1f;

	public float VoiceVolume { get; set; } = 0.3f;

	public float VoiceCooldownSeconds { get; set; }

	public float BattleReadyScale { get; set; } = 1f;

	public float BattleReadyOpacity { get; set; } = 1f;

	public float BattleReadyOffsetX { get; set; }

	public float BattleReadyOffsetY { get; set; }

	public bool ShowBattleReadyOverlay { get; set; } = true;

	public LeftSideCardPresentationMode BattleReadyOverlayMode { get; set; } = LeftSideCardPresentationMode.AllSupportedCards;

	public bool ShowCardPlayPresentation { get; set; } = true;

	public AttackFullScreenPresentationMode AttackFullScreenPresentationMode { get; set; }

	public CardSelectionPresentationTrigger CardSelectionPresentationTrigger { get; set; }

	public bool MeleeApproachMovementEnabled { get; set; } = true;

	public bool OfficialAttackTimingEnabled { get; set; } = true;

	public CombatPresentationMode CombatPresentationMode { get; set; } = CombatPresentationMode.Full;

	public DefeatCgOverlayMode DefeatCgOverlayMode { get; set; } = DefeatCgOverlayMode.Random;

	public bool ShowDefeatCgOverlay
	{
		get
		{
			return DefeatCgOverlayMode != DefeatCgOverlayMode.Off;
		}
		set
		{
			DefeatCgOverlayMode = (value ? DefeatCgOverlayMode.Random : DefeatCgOverlayMode.Off);
		}
	}

	public CharacterSelectBackgroundVariant CharacterSelectBackgroundVariant { get; set; }

	public void Reset()
	{
		EffectsEnabled = true;
		ParticlesEnabled = true;
		CombatSfxEnabled = true;
		VoiceEnabled = true;
		EffectVolume = 1f;
		EffectBrightness = 1f;
		EffectOpacity = 1f;
		VoiceVolume = 0.3f;
		VoiceCooldownSeconds = 0f;
		BattleReadyScale = 1f;
		BattleReadyOpacity = 1f;
		BattleReadyOffsetX = 0f;
		BattleReadyOffsetY = 0f;
		ShowBattleReadyOverlay = true;
		BattleReadyOverlayMode = LeftSideCardPresentationMode.AllSupportedCards;
		ShowCardPlayPresentation = true;
		AttackFullScreenPresentationMode = AttackFullScreenPresentationMode.Always;
		CardSelectionPresentationTrigger = CardSelectionPresentationTrigger.CardHover;
		MeleeApproachMovementEnabled = true;
		OfficialAttackTimingEnabled = true;
		CombatPresentationMode = CombatPresentationMode.Full;
		DefeatCgOverlayMode = DefeatCgOverlayMode.Random;
		CharacterSelectBackgroundVariant = CharacterSelectBackgroundVariant.StaticPortrait;
	}
}
