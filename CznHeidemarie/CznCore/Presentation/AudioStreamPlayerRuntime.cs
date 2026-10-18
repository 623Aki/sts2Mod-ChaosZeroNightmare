using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Godot.Collections;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class AudioStreamPlayerRuntime
{
	private static readonly StringName SfxBus = new StringName("SFX");

	private readonly System.Collections.Generic.Dictionary<string, AudioStream> _cache = new System.Collections.Generic.Dictionary<string, AudioStream>(StringComparer.OrdinalIgnoreCase);

	private readonly System.Collections.Generic.Dictionary<string, long> _missingAssetNextLogAt = new System.Collections.Generic.Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _threadedLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly ResourceLoader.CacheMode? _cacheMode;

	private readonly string _missingAssetPrefix;

	private readonly string _loadFailedPrefix;

	private readonly Action<string, int> _warn;

	private readonly int _maxPlayers;

	private readonly List<AudioStreamPlayer> _players = new List<AudioStreamPlayer>();

	private string _playerName;

	private Node? _attachedTo;

	private int _nextPlayerIndex;

	private ResourceLoader.CacheMode EffectiveCacheMode => _cacheMode ?? ResourceLoader.CacheMode.Reuse;

	public AudioStreamPlayerRuntime(string playerName, string missingAssetPrefix, string loadFailedPrefix, ResourceLoader.CacheMode? cacheMode = null, Action<string, int>? warn = null, int maxPlayers = 1)
	{
		_playerName = playerName;
		_missingAssetPrefix = missingAssetPrefix;
		_loadFailedPrefix = loadFailedPrefix;
		_cacheMode = cacheMode;
		_warn = warn ?? new Action<string, int>(NoopWarn);
		_maxPlayers = Math.Max(1, maxPlayers);
	}

	public void SetPlayerName(string playerName)
	{
		_playerName = playerName;
		for (int i = 0; i < _players.Count; i++)
		{
			AudioStreamPlayer audioStreamPlayer = _players[i];
			if (GodotObject.IsInstanceValid(audioStreamPlayer))
			{
				audioStreamPlayer.Name = FormatPlayerName(i);
			}
		}
	}

	public bool Prewarm(string resPath)
	{
		if (TryGetCachedStream(resPath, out AudioStream stream))
		{
			return true;
		}
		if (_threadedLoads.Contains(resPath))
		{
			return TryCompleteThreadedLoad(resPath, allowBlocking: false);
		}
		try
		{
			if (!ResourceLoader.Exists(resPath))
			{
				if (TryCacheRawAudioStream(resPath, out stream))
				{
					return true;
				}
				LogMissingAsset(resPath, null);
				return true;
			}
			Error error = ResourceLoader.LoadThreadedRequest(resPath, "", useSubThreads: false, EffectiveCacheMode);
			if (error != Error.Ok)
			{
				if (TryCacheRawAudioStream(resPath, out stream))
				{
					return true;
				}
				LogMissingAsset(resPath, "threaded request failed: " + error);
				return true;
			}
			_threadedLoads.Add(resPath);
			return TryCompleteThreadedLoad(resPath, allowBlocking: false);
		}
		catch (Exception ex)
		{
			LogMissingAsset(resPath, ex.Message);
			return true;
		}
	}

	public bool Play(string resPath, float volumeLinear)
	{
		return PlayWithResult(resPath, volumeLinear).Played;
	}

	public AudioPlaybackResult PlayWithResult(string resPath, float volumeLinear)
	{
		AudioStream stream = GetStream(resPath);
		if (stream == null)
		{
			return AudioPlaybackResult.NotPlayed;
		}
		AudioStreamPlayer audioStreamPlayer = EnsurePlayer();
		if (audioStreamPlayer == null)
		{
			return AudioPlaybackResult.NotPlayed;
		}
		audioStreamPlayer.Stop();
		audioStreamPlayer.Stream = stream;
		audioStreamPlayer.VolumeLinear = volumeLinear;
		audioStreamPlayer.Bus = SfxBus;
		audioStreamPlayer.Play();
		return new AudioPlaybackResult(Played: true, GetStreamLengthSeconds(stream));
	}

	public int PrewarmPlayers(int count = 0)
	{
		Node node = ResolveTargetParent();
		if (node == null)
		{
			return _players.Count;
		}
		RemoveInvalidPlayers();
		EnsurePlayersAttached(node);
		int num = ((count <= 0) ? _maxPlayers : Math.Clamp(count, 1, _maxPlayers));
		while (_players.Count < num)
		{
			AudioStreamPlayer audioStreamPlayer = CreatePlayer(_players.Count);
			node.AddChild(audioStreamPlayer, forceReadableName: false, Node.InternalMode.Disabled);
			_players.Add(audioStreamPlayer);
		}
		_attachedTo = node;
		return _players.Count;
	}

	public void Stop()
	{
		foreach (AudioStreamPlayer player in _players)
		{
			if (GodotObject.IsInstanceValid(player))
			{
				player.Stop();
			}
		}
	}

	public void TrimCache(bool stopPlayback = false)
	{
		_cache.Clear();
		_missingAssetNextLogAt.Clear();
		foreach (AudioStreamPlayer player in _players)
		{
			if (GodotObject.IsInstanceValid(player))
			{
				if (stopPlayback && player.Playing)
				{
					player.Stop();
				}
				if (stopPlayback || !player.Playing)
				{
					player.Stream = null;
				}
			}
		}
	}

	private AudioStreamPlayer? EnsurePlayer()
	{
		Node node = ResolveTargetParent();
		if (node == null)
		{
			return null;
		}
		RemoveInvalidPlayers();
		EnsurePlayersAttached(node);
		for (int i = 0; i < _players.Count; i++)
		{
			AudioStreamPlayer audioStreamPlayer = _players[i];
			if (!audioStreamPlayer.Playing)
			{
				return audioStreamPlayer;
			}
		}
		if (_players.Count < _maxPlayers)
		{
			AudioStreamPlayer audioStreamPlayer2 = CreatePlayer(_players.Count);
			node.AddChild(audioStreamPlayer2, forceReadableName: false, Node.InternalMode.Disabled);
			_players.Add(audioStreamPlayer2);
			_attachedTo = node;
			return audioStreamPlayer2;
		}
		_nextPlayerIndex = (_nextPlayerIndex + 1) % _players.Count;
		AudioStreamPlayer audioStreamPlayer3 = _players[_nextPlayerIndex];
		audioStreamPlayer3.Stop();
		return audioStreamPlayer3;
	}

	private static Node? ResolveTargetParent()
	{
		object obj = NCombatRoom.Instance?.Ui;
		if (obj == null)
		{
			obj = NCombatRoom.Instance;
			if (obj == null)
			{
				obj = NGame.Instance;
				if (obj == null)
				{
					SceneTree obj2 = Engine.GetMainLoop() as SceneTree;
					if (obj2 == null)
					{
						return null;
					}
					obj = obj2.Root;
				}
			}
		}
		return (Node?)obj;
	}

	private void RemoveInvalidPlayers()
	{
		for (int num = _players.Count - 1; num >= 0; num--)
		{
			if (!GodotObject.IsInstanceValid(_players[num]))
			{
				_players.RemoveAt(num);
			}
		}
		if (_players.Count == 0)
		{
			_nextPlayerIndex = 0;
		}
		else if (_nextPlayerIndex >= _players.Count)
		{
			_nextPlayerIndex = 0;
		}
	}

	private void EnsurePlayersAttached(Node targetParent)
	{
		if (_attachedTo != targetParent || !GodotObject.IsInstanceValid(_attachedTo))
		{
			for (int i = 0; i < _players.Count; i++)
			{
				AudioStreamPlayer audioStreamPlayer = _players[i];
				audioStreamPlayer.GetParent()?.RemoveChild(audioStreamPlayer);
				audioStreamPlayer.Name = FormatPlayerName(i);
				targetParent.AddChild(audioStreamPlayer, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_attachedTo = targetParent;
		}
	}

	private AudioStreamPlayer CreatePlayer(int index)
	{
		return new AudioStreamPlayer
		{
			Name = FormatPlayerName(index)
		};
	}

	private string FormatPlayerName(int index)
	{
		if (_maxPlayers > 1)
		{
			return _playerName + "_" + index.ToString(CultureInfo.InvariantCulture);
		}
		return _playerName;
	}

	private AudioStream? GetStream(string resPath)
	{
		if (TryGetCachedStream(resPath, out AudioStream stream))
		{
			return stream;
		}
		if (_threadedLoads.Contains(resPath) && TryCompleteThreadedLoad(resPath, allowBlocking: true))
		{
			if (!TryGetCachedStream(resPath, out stream))
			{
				return null;
			}
			return stream;
		}
		try
		{
			if (!ResourceLoader.Exists(resPath))
			{
				if (TryCacheRawAudioStream(resPath, out stream))
				{
					return stream;
				}
				LogMissingAsset(resPath, null);
				return null;
			}
			stream = ResourceLoader.Load<AudioStream>(resPath, null, EffectiveCacheMode);
			if (stream == null)
			{
				if (TryCacheRawAudioStream(resPath, out stream))
				{
					return stream;
				}
				LogMissingAsset(resPath, null);
				return null;
			}
			_cache[resPath] = stream;
			return stream;
		}
		catch (Exception ex)
		{
			LogMissingAsset(resPath, ex.Message);
			return null;
		}
	}

	private bool TryGetCachedStream(string resPath, out AudioStream? stream)
	{
		if (_cache.TryGetValue(resPath, out stream) && stream != null && GodotObject.IsInstanceValid(stream))
		{
			return true;
		}
		stream = null;
		_cache.Remove(resPath);
		return false;
	}

	private bool TryCompleteThreadedLoad(string resPath, bool allowBlocking)
	{
		switch (ResourceLoader.LoadThreadedGetStatus(resPath))
		{
		case ResourceLoader.ThreadLoadStatus.InProgress:
			if (!allowBlocking)
			{
				return false;
			}
			return CompleteThreadedLoad(resPath);
		case ResourceLoader.ThreadLoadStatus.Loaded:
			return CompleteThreadedLoad(resPath);
		default:
			_threadedLoads.Remove(resPath);
			return true;
		}
	}

	private bool CompleteThreadedLoad(string resPath)
	{
		Resource resource = ResourceLoader.LoadThreadedGet(resPath);
		_threadedLoads.Remove(resPath);
		if (resource is AudioStream audioStream && GodotObject.IsInstanceValid(audioStream))
		{
			_cache[resPath] = audioStream;
			return true;
		}
		if (TryCacheRawAudioStream(resPath, out AudioStream _))
		{
			return true;
		}
		LogMissingAsset(resPath, null);
		return true;
	}

	private bool TryCacheRawAudioStream(string resPath, out AudioStream? stream)
	{
		stream = null;
		if (!IsRawWavPath(resPath) || !global::Godot.FileAccess.FileExists(resPath))
		{
			return false;
		}
		stream = AudioStreamWav.LoadFromFile(resPath, new Dictionary());
		if (stream == null || !GodotObject.IsInstanceValid(stream))
		{
			stream = null;
			return false;
		}
		_cache[resPath] = stream;
		return true;
	}

	private static bool IsRawWavPath(string resPath)
	{
		return resPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
	}

	private static float GetStreamLengthSeconds(AudioStream stream)
	{
		try
		{
			return Math.Max(0f, (float)stream.GetLength());
		}
		catch
		{
				SkinLog.Warn("Unable to read audio stream length; using zero duration.");
			return 0f;
		}
	}

	private void LogMissingAsset(string resPath, string? error)
	{
		long tickCount = System.Environment.TickCount64;
		if (!_missingAssetNextLogAt.TryGetValue(resPath, out var value) || tickCount >= value)
		{
			_missingAssetNextLogAt[resPath] = tickCount + 60000;
			if (string.IsNullOrWhiteSpace(error))
			{
				_warn(_missingAssetPrefix + resPath, 2);
			}
			else
			{
				_warn(_loadFailedPrefix + resPath + " err=" + error, 2);
			}
		}
	}

	private static void NoopWarn(string message, int priority)
	{
	}
}
