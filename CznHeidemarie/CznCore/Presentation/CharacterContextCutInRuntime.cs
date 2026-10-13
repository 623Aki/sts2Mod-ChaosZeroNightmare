using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed class CharacterContextCutInRuntime
{
	private sealed class OverlayHost : ICombatVideoCutInOverlayHost<Player>
	{
		private readonly CharacterContextCutInRuntime _runtime;

		public OverlayHost(CharacterContextCutInRuntime runtime)
		{
			_runtime = runtime;
		}

		public bool IsEnabled()
		{
			return _runtime._host.IsEnabled();
		}

		public bool IsTokenCurrent(Player? actor, int token)
		{
			return _runtime.IsTokenCurrent(actor, token);
		}

		public string RuntimeNodeName(string suffix)
		{
			return _runtime._host.RuntimeNodeName(suffix);
		}

		public void Warn(string message, int priority)
		{
			_runtime._host.Warn(message, priority);
		}

		public void Info(string message, int priority)
		{
			_runtime._host.Info(message, priority);
		}
	}

	private readonly ICharacterContextCutInHost _host;

	private readonly CombatVideoCutInOverlayRuntime<Player> _overlay;

	private int _token;

	public CharacterContextCutInRuntime(IReadOnlyDictionary<string, CombatVideoCutInProfile> profiles, ICharacterContextCutInHost host, string logPrefix = "Character context cut-in")
	{
		ArgumentNullException.ThrowIfNull(profiles, "profiles");
		_host = host ?? throw new ArgumentNullException("host");
		_overlay = new CombatVideoCutInOverlayRuntime<Player>(profiles, new OverlayHost(this), null, null, logPrefix);
	}

	public void Preload(string? stateId)
	{
		_overlay.Preload(stateId);
	}

	public bool TryPlay(Player? player, string? stateId)
	{
		if (string.IsNullOrWhiteSpace(stateId) || !_host.IsEnabled() || !_host.IsPlayerCurrent(player))
		{
			return false;
		}
		_token++;
		if (_token == 0)
		{
			_token++;
		}
		_overlay.TryPlay(player, stateId, _token);
		return true;
	}

	public void Cleanup(bool releaseNodes)
	{
		_overlay.Cleanup(releaseNodes);
	}

	public void ClearVideoCache()
	{
		_overlay.ClearVideoCache();
	}

	private bool IsTokenCurrent(Player? player, int token)
	{
		if (token == _token)
		{
			return _host.IsPlayerCurrent(player);
		}
		return false;
	}
}
