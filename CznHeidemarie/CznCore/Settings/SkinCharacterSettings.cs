using System;
using CznCore.Presentation;

namespace CznCore.Settings;

internal sealed class SkinCharacterSettings : ICharacterSkinAudioSettings, ICharacterSkinPresentationSettings, ICombatPresentationAudienceSettings
{
	private readonly CharacterSkinSettingsStore _store;

	public string CharacterId { get; }

	public bool EffectsEnabled => _store.GetCharacterEffectsEnabled(CharacterId);

	public bool ParticlesEnabled => _store.GetCharacterParticlesEnabled(CharacterId);

	public bool CombatSfxEnabled => _store.GetCharacterCombatSfxEnabled(CharacterId);

	public bool VoiceEnabled => _store.GetCharacterVoiceEnabled(CharacterId);

	public bool EffectiveEffectsEnabled => _store.GetEffectiveCharacterEffectsEnabled(CharacterId);

	public bool EffectiveParticlesEnabled => _store.GetEffectiveCharacterParticlesEnabled(CharacterId);

	public bool EffectiveCombatSfxEnabled => _store.GetEffectiveCharacterCombatSfxEnabled(CharacterId);

	public bool EffectiveVoiceEnabled => _store.GetEffectiveCharacterVoiceEnabled(CharacterId);

	public bool TeammatePresentationEnabled => _store.TeammatePresentationEnabled;

	public bool TeammateEffectsEnabled => _store.TeammateEffectsEnabled;

	public bool TeammateCombatSfxEnabled => _store.TeammateCombatSfxEnabled;

	public bool TeammateVoiceEnabled => _store.TeammateVoiceEnabled;

	public float TeammateVoiceCooldownSeconds => _store.TeammateVoiceCooldownSeconds;

	public bool TeammateCutInEnabled => _store.TeammateCutInEnabled;

	public AttackFullScreenPresentationMode TeammateAttackFullScreenPresentationMode => _store.TeammateAttackFullScreenPresentationMode;

	public bool LocalCutInEnabled => ShowCardPlayPresentation;

	public float EffectVolume => _store.GetCharacterEffectVolume(CharacterId);

	public float EffectBrightness => _store.GetCharacterEffectBrightness(CharacterId);

	public float EffectOpacity => _store.GetCharacterEffectOpacity(CharacterId);

	public float VoiceVolume => _store.GetCharacterVoiceVolume(CharacterId);

	public float VoiceCooldownSeconds => _store.GetCharacterVoiceCooldownSeconds(CharacterId);

	public float BattleReadyScale => _store.GetCharacterBattleReadyScale(CharacterId);

	public float BattleReadyOpacity => _store.GetCharacterBattleReadyOpacity(CharacterId);

	public float BattleReadyOffsetX => _store.GetCharacterBattleReadyOffsetX(CharacterId);

	public float BattleReadyOffsetY => _store.GetCharacterBattleReadyOffsetY(CharacterId);

	public bool ShowBattleReadyOverlay => _store.GetCharacterShowBattleReadyOverlay(CharacterId);

	public LeftSideCardPresentationMode BattleReadyOverlayMode => _store.GetCharacterBattleReadyOverlayMode(CharacterId);

	public bool ShowCardPlayPresentation => _store.GetCharacterShowCardPlayPresentation(CharacterId);

	public AttackFullScreenPresentationMode AttackFullScreenPresentationMode => _store.GetCharacterAttackFullScreenPresentationMode(CharacterId);

	public CardSelectionPresentationTrigger CardSelectionPresentationTrigger => _store.GetCharacterCardSelectionPresentationTrigger(CharacterId);

	public bool MeleeApproachMovementEnabled => _store.GetCharacterMeleeApproachMovementEnabled(CharacterId);

	public bool OfficialAttackTimingEnabled => _store.GetCharacterOfficialAttackTimingEnabled(CharacterId);

	public CombatPresentationMode CombatPresentationMode => _store.GetCharacterCombatPresentationMode(CharacterId);

	public bool ShowDefeatCgOverlay => _store.GetCharacterShowDefeatCgOverlay(CharacterId);

	public DefeatCgOverlayMode DefeatCgOverlayMode => _store.GetCharacterDefeatCgOverlayMode(CharacterId);

	public CharacterSelectBackgroundVariant CharacterSelectBackgroundVariant => _store.GetCharacterSelectBackgroundVariant(CharacterId);

	public SkinCharacterSettings(CharacterSkinSettingsStore store, string characterId)
	{
		_store = store ?? throw new ArgumentNullException("store");
		CharacterId = _store.NormalizeCharacterId(characterId);
	}

	internal void Subscribe(Action<string?> changed)
	{
		_store.Changed += changed;
	}

	public void SetEffectsEnabled(bool value, bool persist)
	{
		_store.SetCharacterEffectsEnabled(CharacterId, value, persist);
	}

	public void SetVoiceEnabled(bool value, bool persist)
	{
		_store.SetCharacterVoiceEnabled(CharacterId, value, persist);
	}

	public void SetParticlesEnabled(bool value, bool persist)
	{
		_store.SetCharacterParticlesEnabled(CharacterId, value, persist);
	}

	public void SetCombatSfxEnabled(bool value, bool persist)
	{
		_store.SetCharacterCombatSfxEnabled(CharacterId, value, persist);
	}

	public void SetEffectVolume(float value, bool persist)
	{
		_store.SetCharacterEffectVolume(CharacterId, value, persist);
	}

	public void SetEffectBrightness(float value, bool persist)
	{
		_store.SetCharacterEffectBrightness(CharacterId, value, persist);
	}

	public void SetEffectOpacity(float value, bool persist)
	{
		_store.SetCharacterEffectOpacity(CharacterId, value, persist);
	}

	public void SetVoiceVolume(float value, bool persist)
	{
		_store.SetCharacterVoiceVolume(CharacterId, value, persist);
	}

	public void SetVoiceCooldownSeconds(float value, bool persist)
	{
		_store.SetCharacterVoiceCooldownSeconds(CharacterId, value, persist);
	}

	public void SetBattleReadyScale(float value, bool persist)
	{
		_store.SetCharacterBattleReadyScale(CharacterId, value, persist);
	}

	public void SetBattleReadyOpacity(float value, bool persist)
	{
		_store.SetCharacterBattleReadyOpacity(CharacterId, value, persist);
	}

	public void SetBattleReadyOffsetX(float value, bool persist)
	{
		_store.SetCharacterBattleReadyOffsetX(CharacterId, value, persist);
	}

	public void SetBattleReadyOffsetY(float value, bool persist)
	{
		_store.SetCharacterBattleReadyOffsetY(CharacterId, value, persist);
	}

	public void SetShowBattleReadyOverlay(bool value, bool persist)
	{
		_store.SetCharacterShowBattleReadyOverlay(CharacterId, value, persist);
	}

	public void SetBattleReadyOverlayMode(LeftSideCardPresentationMode value, bool persist)
	{
		_store.SetCharacterBattleReadyOverlayMode(CharacterId, value, persist);
	}

	public void SetShowCardPlayPresentation(bool value, bool persist)
	{
		_store.SetCharacterShowCardPlayPresentation(CharacterId, value, persist);
	}

	public void SetAttackFullScreenPresentationMode(AttackFullScreenPresentationMode value, bool persist)
	{
		_store.SetCharacterAttackFullScreenPresentationMode(CharacterId, value, persist);
	}

	public void SetCardSelectionPresentationTrigger(CardSelectionPresentationTrigger value, bool persist)
	{
		_store.SetCharacterCardSelectionPresentationTrigger(CharacterId, value, persist);
	}

	public void SetMeleeApproachMovementEnabled(bool value, bool persist)
	{
		_store.SetCharacterMeleeApproachMovementEnabled(CharacterId, value, persist);
	}

	public void SetOfficialAttackTimingEnabled(bool value, bool persist)
	{
		_store.SetCharacterOfficialAttackTimingEnabled(CharacterId, value, persist);
	}

	public void SetCombatPresentationMode(CombatPresentationMode value, bool persist)
	{
		_store.SetCharacterCombatPresentationMode(CharacterId, value, persist);
	}

	public void SetShowDefeatCgOverlay(bool value, bool persist)
	{
		_store.SetCharacterShowDefeatCgOverlay(CharacterId, value, persist);
	}

	public void SetDefeatCgOverlayMode(DefeatCgOverlayMode value, bool persist)
	{
		_store.SetCharacterDefeatCgOverlayMode(CharacterId, value, persist);
	}

	public void SetCharacterSelectBackgroundVariant(CharacterSelectBackgroundVariant value, bool persist)
	{
		_store.SetCharacterSelectBackgroundVariant(CharacterId, value, persist);
	}

	public void ResetToDefaults(bool persist)
	{
		_store.ResetCharacterToDefaults(CharacterId, persist);
	}
}
