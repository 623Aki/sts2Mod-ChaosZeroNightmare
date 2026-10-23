using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal sealed class CharacterSkinVoicePlayerRuntime
{
	private const int MaxConcurrentVoicePlayers = 12;

	private const int PrewarmVoicePlayers = 2;

	private readonly AudioStreamPlayerRuntime _runtime;

	private readonly string _basePlayerName;

	private readonly float _volumeMultiplier;

	private ICharacterSkinAudioSettings _settings;

	private CombatPresentationAudiencePolicy _audiencePolicy;

	private long _lastLocalVoicePlaybackTick;

	public bool IsEnabled => _settings.EffectiveVoiceEnabled;

	public CharacterSkinVoicePlayerRuntime(ICharacterSkinAudioSettings settings, string basePlayerName = "CharacterSkinVoicePlayer", float volumeMultiplier = 8f)
	{
		_settings = settings ?? throw new ArgumentNullException("settings");
		_audiencePolicy = CreateAudiencePolicy(_settings);
		_basePlayerName = basePlayerName;
		_volumeMultiplier = volumeMultiplier;
		Action<string, int> warn = SkinLog.Warn;
		_runtime = new AudioStreamPlayerRuntime(basePlayerName, "Missing voice asset: ", "Voice asset load failed: ", null, warn, 12);
		Configure(settings);
	}

	public void Configure(ICharacterSkinAudioSettings settings)
	{
		string characterId = _settings.CharacterId;
		_settings = settings ?? throw new ArgumentNullException("settings");
		_audiencePolicy = CreateAudiencePolicy(_settings);
		_runtime.SetPlayerName(_basePlayerName + "_" + _settings.CharacterId);
		if (!string.Equals(characterId, _settings.CharacterId, StringComparison.OrdinalIgnoreCase))
		{
			_lastLocalVoicePlaybackTick = 0L;
		}
	}

	public float ResolveVolumeLinear(float volume = 1f)
	{
		if (!_settings.EffectiveVoiceEnabled)
		{
			return 0f;
		}
		return volume * _volumeMultiplier * _settings.VoiceVolume;
	}

	public void Prewarm(IReadOnlyList<string> resPaths)
	{
		if (!_settings.EffectiveVoiceEnabled)
		{
			return;
		}
		_runtime.PrewarmPlayers(2);
		foreach (string resPath in resPaths)
		{
			_runtime.Prewarm(resPath);
		}
	}

	public void Prewarm(string resPath)
	{
		if (_settings.EffectiveVoiceEnabled)
		{
			_runtime.PrewarmPlayers(2);
			_runtime.Prewarm(resPath);
		}
	}

	public void PlayRandom(IReadOnlyList<string> resPaths, float volume = 1f)
	{
		if (resPaths.Count > 0)
		{
			Play(resPaths[Random.Shared.Next(resPaths.Count)], volume);
		}
	}

	public void PlayRandomFor(Player? player, IReadOnlyList<string> resPaths, float volume = 1f)
	{
		if (resPaths.Count > 0)
		{
			PlayFor(player, resPaths[Random.Shared.Next(resPaths.Count)], volume);
		}
	}

	public void PlayRandomRequiredPresentationFor(Player? player, IReadOnlyList<string> resPaths, float volume = 1f)
	{
		if (resPaths.Count > 0)
		{
			PlayRequiredPresentationFor(player, resPaths[Random.Shared.Next(resPaths.Count)], volume);
		}
	}

	public bool PlayCueFor(Player? player, CombatPresentationVoiceCue? cue)
	{
		if (cue == null || cue.Paths.Count == 0)
		{
			return false;
		}
		string text = cue.Mode switch
		{
			CombatPresentationVoiceCueMode.Single => cue.Paths[0], 
			CombatPresentationVoiceCueMode.Random => cue.Paths[Random.Shared.Next(cue.Paths.Count)], 
			_ => string.Empty, 
		};
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (!cue.IsRequiredPresentation)
		{
			return PlayFor(player, text, cue.Volume);
		}
		return PlayRequiredPresentationFor(player, text, cue.Volume);
	}

	public bool PlayExclusiveCueFor(Player? player, CombatPresentationVoiceCue? cue)
	{
		if (cue == null || cue.Paths.Count == 0)
		{
			return false;
		}
		Stop();
		return PlayCueFor(player, cue);
	}

	public bool Play(string resPath, float volume = 1f)
	{
		return PlayWithResult(resPath, volume).Played;
	}

	public AudioPlaybackResult PlayWithResult(string resPath, float volume = 1f)
	{
		return PlayLocalPathWithResult(resPath, volume);
	}

	public bool PlayFor(Player? player, string? resPath, float volume = 1f)
	{
		return PlayForWithResult(player, resPath, volume).Played;
	}

	public AudioPlaybackResult PlayForWithResult(Player? player, string? resPath, float volume = 1f)
	{
		return PlayForWithResultCore(player, resPath, volume, bypassCooldown: false);
	}

	public bool PlayRequiredPresentationFor(Player? player, string? resPath, float volume = 1f)
	{
		return PlayRequiredPresentationForWithResult(player, resPath, volume).Played;
	}

	public AudioPlaybackResult PlayRequiredPresentationForWithResult(Player? player, string? resPath, float volume = 1f)
	{
		return PlayForWithResultCore(player, resPath, volume, bypassCooldown: true);
	}

	private AudioPlaybackResult PlayForWithResultCore(Player? player, string? resPath, float volume, bool bypassCooldown)
	{
		if (!AllowsPlayer(player) || string.IsNullOrWhiteSpace(resPath))
		{
			return AudioPlaybackResult.NotPlayed;
		}
		bool flag = _audiencePolicy.IsTeammate(player);
		ulong voiceOwnerNetId = player?.NetId ?? 0uL;
		if (flag && (NCombatRoom.Instance == null || (!bypassCooldown && !SharedTeammateVoicePlaybackGate.Allows(voiceOwnerNetId, _settings.TeammateVoiceCooldownSeconds))))
		{
			return AudioPlaybackResult.NotPlayed;
		}
		if (!flag && !bypassCooldown && !AllowsLocalVoicePlayback())
		{
			return AudioPlaybackResult.NotPlayed;
		}
		AudioPlaybackResult result = PlayRawWithResult(resPath, volume);
		if (flag && result.Played)
		{
			SharedTeammateVoicePlaybackGate.RecordSuccessfulPlayback(voiceOwnerNetId);
		}
		else if (result.Played)
		{
			RecordLocalVoicePlayback();
		}
		return result;
	}

	public bool PlayLocalFor(Player? player, string? resPath, float volume = 1f)
	{
		return PlayLocalForWithResult(player, resPath, volume).Played;
	}

	public AudioPlaybackResult PlayLocalForWithResult(Player? player, string? resPath, float volume = 1f)
	{
		if (!Allows(player) || string.IsNullOrWhiteSpace(resPath))
		{
			return AudioPlaybackResult.NotPlayed;
		}
		return PlayLocalPathWithResult(resPath, volume);
	}

	private AudioPlaybackResult PlayLocalPathWithResult(string resPath, float volume)
	{
		if (!AllowsLocalVoicePlayback())
		{
			return AudioPlaybackResult.NotPlayed;
		}
		AudioPlaybackResult result = PlayRawWithResult(resPath, volume);
		if (result.Played)
		{
			RecordLocalVoicePlayback();
		}
		return result;
	}

	private AudioPlaybackResult PlayRawWithResult(string resPath, float volume)
	{
		if (!_settings.EffectiveVoiceEnabled)
		{
			return AudioPlaybackResult.NotPlayed;
		}
		return _runtime.PlayWithResult(resPath, volume * _volumeMultiplier * _settings.VoiceVolume);
	}

	private bool AllowsLocalVoicePlayback()
	{
		float voiceCooldownSeconds = _settings.VoiceCooldownSeconds;
		if (voiceCooldownSeconds <= 0f || _lastLocalVoicePlaybackTick == 0L)
		{
			return true;
		}
		long num = Environment.TickCount64 - _lastLocalVoicePlaybackTick;
		if (num >= 0)
		{
			return (float)num >= voiceCooldownSeconds * 1000f;
		}
		return true;
	}

	private void RecordLocalVoicePlayback()
	{
		_lastLocalVoicePlaybackTick = Environment.TickCount64;
	}

	public static bool Allows(Player? player)
	{
		if (player != null)
		{
			return LocalContext.IsMe(player);
		}
		return true;
	}

	public bool AllowsPlayer(Player? player)
	{
		if (player != null)
		{
			return _audiencePolicy.Allows(player, CombatPresentationChannel.Voice);
		}
		return true;
	}

	private static CombatPresentationAudiencePolicy CreateAudiencePolicy(ICombatPresentationAudienceSettings settings)
	{
		return new CombatPresentationAudiencePolicy(settings, (Player? player) => player != null);
	}

	public static Player? GetLocalRunPlayer()
	{
		try
		{
			RunState runState = RunManager.Instance?.DebugOnlyGetState();
			return (runState != null) ? LocalContext.GetMe(runState) : null;
		}
		catch
		{
			return null;
		}
	}

	public void Stop()
	{
		_runtime.Stop();
	}

	public void TrimCache(bool stopPlayback = false)
	{
		_runtime.TrimCache(stopPlayback);
	}
}
