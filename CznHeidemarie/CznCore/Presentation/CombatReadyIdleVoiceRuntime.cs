using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace CznCore.Presentation;

internal sealed class CombatReadyIdleVoiceRuntime<TActor> where TActor : class
{
	private sealed class State
	{
		public object? LastAttemptScope { get; set; }

		public ulong LastAttemptMsec { get; set; }
	}

	private readonly ConditionalWeakTable<TActor, State> _states = new ConditionalWeakTable<TActor, State>();

	private readonly IReadOnlyList<string> _voicePaths;

	private readonly Func<TActor, int?> _resolveTurnNumber;

	private readonly Func<TActor, bool> _canAttempt;

	private readonly Func<TActor, string?, float, bool> _playVoice;

	private readonly Func<TActor, object?> _resolveAttemptScope;

	private readonly float _chance;

	private readonly float _volume;

	private readonly float _cooldownSeconds;

	public CombatReadyIdleVoiceRuntime(IReadOnlyList<string> voicePaths, Func<TActor, int?> resolveTurnNumber, Func<TActor, bool> canAttempt, Func<TActor, string?, float, bool> playVoice, Func<TActor, object?>? resolveAttemptScope = null, float chance = 1f, float volume = 1f, float cooldownSeconds = 5f)
	{
		_voicePaths = voicePaths ?? throw new ArgumentNullException("voicePaths");
		_resolveTurnNumber = resolveTurnNumber ?? throw new ArgumentNullException("resolveTurnNumber");
		_canAttempt = canAttempt ?? throw new ArgumentNullException("canAttempt");
		_playVoice = playVoice ?? throw new ArgumentNullException("playVoice");
		_resolveAttemptScope = resolveAttemptScope ?? ((Func<TActor, object>)((TActor _) => (object?)null));
		_chance = Math.Clamp(chance, 0f, 1f);
		_volume = volume;
		_cooldownSeconds = Math.Max(0f, cooldownSeconds);
	}

	public bool TryPlayForReadyTransition(TActor? actor)
	{
		if (actor == null || _voicePaths.Count == 0 || !_canAttempt(actor))
		{
			return false;
		}
		if (!_resolveTurnNumber(actor).HasValue)
		{
			return false;
		}
		State orCreateValue = _states.GetOrCreateValue(actor);
		object obj = _resolveAttemptScope(actor);
		if (orCreateValue.LastAttemptScope != obj)
		{
			orCreateValue.LastAttemptScope = obj;
			orCreateValue.LastAttemptMsec = 0uL;
		}
		ulong ticksMsec = Time.GetTicksMsec();
		if (orCreateValue.LastAttemptMsec != 0 && ((ticksMsec >= orCreateValue.LastAttemptMsec) ? ((float)(ticksMsec - orCreateValue.LastAttemptMsec) / 1000f) : _cooldownSeconds) < _cooldownSeconds)
		{
			return false;
		}
		orCreateValue.LastAttemptMsec = ticksMsec;
		if (Random.Shared.NextDouble() >= (double)_chance)
		{
			return false;
		}
		string arg = _voicePaths[Random.Shared.Next(_voicePaths.Count)];
		return _playVoice(actor, arg, _volume);
	}

	public void RefreshCooldown(TActor? actor)
	{
		if (actor != null)
		{
			State orCreateValue = _states.GetOrCreateValue(actor);
			orCreateValue.LastAttemptScope = _resolveAttemptScope(actor);
			orCreateValue.LastAttemptMsec = Time.GetTicksMsec();
		}
	}
}
