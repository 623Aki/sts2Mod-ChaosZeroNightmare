using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CharacterSelectIdentityProfile(string TitleLocKey, string DescriptionLocKey, IReadOnlyDictionary<CharacterSelectBackgroundVariant, string> VoicePathsByBackground)
{
	public bool TryGetVoicePath(CharacterSelectBackgroundVariant backgroundVariant, out string voicePath)
	{
		return VoicePathsByBackground.TryGetValue(backgroundVariant, out voicePath);
	}
}
