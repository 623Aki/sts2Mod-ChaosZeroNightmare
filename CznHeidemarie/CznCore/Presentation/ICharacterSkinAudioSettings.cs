namespace CznCore.Presentation;

internal interface ICharacterSkinAudioSettings : ICharacterSkinPresentationSettings, ICombatPresentationAudienceSettings
{
	bool EffectiveVoiceEnabled { get; }

	float EffectVolume { get; }

	float VoiceVolume { get; }

	float VoiceCooldownSeconds { get; }
}
