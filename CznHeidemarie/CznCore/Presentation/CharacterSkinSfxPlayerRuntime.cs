using System;
using Godot;

namespace CznCore.Presentation;

internal sealed class CharacterSkinSfxPlayerRuntime
{
	private const int MaxConcurrentSfxPlayers = 12;

	private readonly AudioStreamPlayerRuntime _runtime;

	private readonly string _basePlayerName;

	private readonly float _volumeMultiplier;

	private readonly int _prewarmPlayers;

	private ICharacterSkinAudioSettings _settings;

	public bool IsEnabled => _settings.EffectiveCombatSfxEnabled;

	public CharacterSkinSfxPlayerRuntime(ICharacterSkinAudioSettings settings, string basePlayerName = "CharacterSkinSfxPlayer", float volumeMultiplier = 1f, int prewarmPlayers = 2)
	{
		_settings = settings ?? throw new ArgumentNullException("settings");
		_basePlayerName = basePlayerName;
		_volumeMultiplier = volumeMultiplier;
		_prewarmPlayers = Math.Max(1, prewarmPlayers);
		_runtime = new AudioStreamPlayerRuntime(basePlayerName, "Missing SFX asset: ", "SFX asset load failed: ", ResourceLoader.CacheMode.Reuse, SkinLog.Warn, 12);
		Configure(settings);
	}

	public void Configure(ICharacterSkinAudioSettings settings)
	{
		_settings = settings ?? throw new ArgumentNullException("settings");
		_runtime.SetPlayerName(_basePlayerName + "_" + _settings.CharacterId);
	}

	public float ResolveVolumeLinear(float volume = 1f)
	{
		if (!_settings.EffectiveCombatSfxEnabled)
		{
			return 0f;
		}
		return volume * _volumeMultiplier * _settings.EffectVolume;
	}

	public bool Prewarm(string resPath)
	{
		if (_settings.EffectiveCombatSfxEnabled)
		{
			_runtime.PrewarmPlayers(_prewarmPlayers);
			return _runtime.Prewarm(resPath);
		}
		return true;
	}

	public void Play(string resPath, float volume = 1f)
	{
		TryPlay(resPath, volume);
	}

	public bool TryPlay(string resPath, float volume = 1f)
	{
		if (!_settings.EffectiveCombatSfxEnabled || _settings.EffectVolume <= 0f)
		{
			return false;
		}
		return _runtime.Play(resPath, volume * _volumeMultiplier * _settings.EffectVolume);
	}

	public bool PlayVolumeDb(string resPath, float volumeDb)
	{
		if (!_settings.EffectiveCombatSfxEnabled || _settings.EffectVolume <= 0f)
		{
			return true;
		}
		return _runtime.Play(resPath, Mathf.DbToLinear(volumeDb) * _settings.EffectVolume);
	}

	public void TrimCache(bool stopPlayback = false)
	{
		_runtime.TrimCache(stopPlayback);
	}
}
