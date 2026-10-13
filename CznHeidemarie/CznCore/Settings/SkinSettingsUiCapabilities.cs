using System;

namespace CznCore.Settings;

[Flags]
internal enum SkinSettingsUiCapabilities
{
	None = 0,
	CharacterSelectBackground = 1,
	DefeatCg = 2,
	CombatPresentationMode = 4,
	CustomCombatPresentation = 8,
	CardSelectionPresentationTrigger = 0x10,
	CardPlayPresentation = 0x20,
	BattleReadyPresentation = 0x40,
	Effects = 0x80,
	Particles = 0x100,
	CombatSfx = 0x200,
	EffectVolume = 0x400,
	EffectBrightness = 0x800,
	EffectOpacity = 0x1000,
	Voice = 0x2000,
	VoiceVolume = 0x4000,
	VoiceCooldown = 0x8000,
	All = CharacterSelectBackground | DefeatCg | CombatPresentationMode | CustomCombatPresentation | CardSelectionPresentationTrigger | CardPlayPresentation | BattleReadyPresentation | Effects | Particles | CombatSfx | EffectVolume | EffectBrightness | EffectOpacity | Voice | VoiceVolume | VoiceCooldown
}
