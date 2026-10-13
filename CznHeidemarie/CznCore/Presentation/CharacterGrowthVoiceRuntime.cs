using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed class CharacterGrowthVoiceRuntime
{
	private readonly CharacterSkinProfile _profile;

	private readonly CharacterSkinVoicePlayerRuntime _voicePlayer;

	public CharacterGrowthVoiceRuntime(CharacterSkinProfile profile, CharacterSkinVoicePlayerRuntime voicePlayer)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		_voicePlayer = voicePlayer ?? throw new ArgumentNullException("voicePlayer");
	}

	public bool TryPlay(Player? player, float volume = 1f)
	{
		if (player == null || !_profile.AppliesTo(player))
		{
			return false;
		}
		IReadOnlyList<string> growthPaths = _profile.Voice.GrowthPaths;
		if (growthPaths.Count == 0)
		{
			return false;
		}
		return _voicePlayer.PlayLocalFor(player, growthPaths[Random.Shared.Next(growthPaths.Count)], volume);
	}
}
