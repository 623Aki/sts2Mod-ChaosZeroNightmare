using System;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed class CharacterRestSiteHealVoiceRuntime
{
	private readonly string? _voicePath;

	private readonly Func<Player?, bool> _appliesTo;

	private readonly CharacterSkinVoicePlayerRuntime _voicePlayer;

	public CharacterRestSiteHealVoiceRuntime(CharacterSkinProfile profile, CharacterSkinVoicePlayerRuntime voicePlayer)
		: this(GetVoiceProfile(profile), voicePlayer, profile.AppliesTo)
	{
	}

	public CharacterRestSiteHealVoiceRuntime(CharacterVoiceProfile voice, CharacterSkinVoicePlayerRuntime voicePlayer, Func<Player?, bool> appliesTo)
	{
		ArgumentNullException.ThrowIfNull(voice, "voice");
		_voicePath = voice.RestSiteHealPath;
		_appliesTo = appliesTo ?? throw new ArgumentNullException("appliesTo");
		_voicePlayer = voicePlayer ?? throw new ArgumentNullException("voicePlayer");
	}

	public bool TryPlay(Player? player, bool isMimicked, float volume = 1f)
	{
		if (isMimicked || player == null || !_appliesTo(player))
		{
			return false;
		}
		return _voicePlayer.PlayLocalFor(player, _voicePath, volume);
	}

	private static CharacterVoiceProfile GetVoiceProfile(CharacterSkinProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile, "profile");
		return profile.Voice;
	}
}
