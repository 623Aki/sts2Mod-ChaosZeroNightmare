using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationVoiceCue(CombatPresentationVoiceCueMode Mode, IReadOnlyList<string> Paths, float Volume = 1f, CombatPresentationVoicePlaybackPolicy PlaybackPolicy = CombatPresentationVoicePlaybackPolicy.Standard)
{
	public bool IsRequiredPresentation => PlaybackPolicy == CombatPresentationVoicePlaybackPolicy.RequiredPresentation;

	public static CombatPresentationVoiceCue Single(string path, float volume = 1f)
	{
		return new CombatPresentationVoiceCue(CombatPresentationVoiceCueMode.Single, new _003C_003Ez__ReadOnlySingleElementList<string>(path), volume);
	}

	public static CombatPresentationVoiceCue Random(IReadOnlyList<string> paths, float volume = 1f)
	{
		return new CombatPresentationVoiceCue(CombatPresentationVoiceCueMode.Random, paths, volume);
	}

	public static CombatPresentationVoiceCue RequiredSingle(string path, float volume = 1f)
	{
		return new CombatPresentationVoiceCue(CombatPresentationVoiceCueMode.Single, new _003C_003Ez__ReadOnlySingleElementList<string>(path), volume, CombatPresentationVoicePlaybackPolicy.RequiredPresentation);
	}

	public static CombatPresentationVoiceCue RequiredRandom(IReadOnlyList<string> paths, float volume = 1f)
	{
		return new CombatPresentationVoiceCue(CombatPresentationVoiceCueMode.Random, paths, volume, CombatPresentationVoicePlaybackPolicy.RequiredPresentation);
	}
}
