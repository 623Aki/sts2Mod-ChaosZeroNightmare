using System;
using CznCore.Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed class CombatDamageVoiceRuntime
{
	private const float DamageVoiceCategoryCooldownSeconds = 2f;

	private const float StressVoiceCategoryCooldownSeconds = 2f;

	private const float StressVoiceGlobalCooldownSeconds = 0.25f;

	private const float PostHitStressPaddingSeconds = 0.05f;

	private const int HeavyDamageStressThresholdPercent = 30;

	private const int CriticalHpStressThresholdPercent = 20;

	private readonly CharacterVoiceProfile _voice;

	private readonly CharacterSkinVoicePlayerRuntime _voicePlayer;

	private readonly Func<Player?, bool> _appliesTo;

	private readonly CharacterVoicePlaybackGate _voiceGate = new CharacterVoicePlaybackGate();

	private bool _stressPlayedThisCombat;

	private bool _criticalHpStressPending;

	private int _stressToken;

	public CombatDamageVoiceRuntime(CharacterSkinProfile profile, CharacterSkinVoicePlayerRuntime voicePlayer, Func<Player?, bool> isTargetPlayer)
		: this((profile ?? throw new ArgumentNullException("profile")).Voice, voicePlayer, (Player? player) => profile.AppliesTo(player) && isTargetPlayer(player))
	{
		ArgumentNullException.ThrowIfNull(isTargetPlayer, "isTargetPlayer");
	}

	public CombatDamageVoiceRuntime(CharacterVoiceProfile voice, CharacterSkinVoicePlayerRuntime voicePlayer, Func<Player?, bool> appliesTo)
	{
		_voice = voice ?? throw new ArgumentNullException("voice");
		_voicePlayer = voicePlayer ?? throw new ArgumentNullException("voicePlayer");
		_appliesTo = appliesTo ?? throw new ArgumentNullException("appliesTo");
	}

	public bool TryPlayHitVoice(Creature? target, DamageResult result, float volume = 1f)
	{
		int unblockedDamage = result.UnblockedDamage;
		if (target == null || !IsLocalTargetPlayer(target) || unblockedDamage <= 0 || !IsAlive(target))
		{
			return false;
		}
		if (!_stressPlayedThisCombat && IsCriticalHp(target))
		{
			_criticalHpStressPending = true;
		}
		bool flag = _voiceGate.TryPlayWithResult("damage", _voice.HitPath, (string path) => _voicePlayer.PlayWithResult(path, volume), out var result2, 2f);
		if (flag && IsHeavyDamage(unblockedDamage, target))
		{
			QueueStressAfterHit(target, result2.DurationSeconds, volume);
		}
		return flag;
	}

	public bool TryPlayDefeatVoice(Creature? creature, bool wasRemovalPrevented, float volume = 1f)
	{
		if (wasRemovalPrevented || !IsLocalTargetPlayer(creature))
		{
			return false;
		}
		CancelStressForDefeat();
		return TryPlay(_voice.DefeatPath, volume);
	}

	public void ResetCombatStressState()
	{
		_stressToken++;
		_stressPlayedThisCombat = false;
		_criticalHpStressPending = false;
	}

	public bool TryPlayPendingCriticalStress(Player? player, float volume = 1f)
	{
		if (player == null || _stressPlayedThisCombat || !_criticalHpStressPending || !IsStressPlayer(player))
		{
			return false;
		}
		Creature creature = player.Creature;
		if (creature == null || !IsAlive(creature))
		{
			_criticalHpStressPending = false;
			return false;
		}
		if (!IsCriticalHp(creature))
		{
			_criticalHpStressPending = false;
			return false;
		}
		if (!TryPlayStressForPlayer(player, volume))
		{
			return false;
		}
		MarkStressPlayed();
		return true;
	}

	private bool IsLocalTargetPlayer(Creature? creature)
	{
		if (creature != null && creature.IsPlayer && LocalContext.IsMe(creature))
		{
			return _appliesTo(creature.Player);
		}
		return false;
	}

	private bool IsStressPlayer(Player? player)
	{
		if (player != null && CharacterSkinVoicePlayerRuntime.Allows(player))
		{
			return _appliesTo(player);
		}
		return false;
	}

	private void QueueStressAfterHit(Creature target, float hitDurationSeconds, float volume)
	{
		if (_stressPlayedThisCombat || _voice.StressPaths.Count == 0)
		{
			return;
		}
		Player player = target.Player;
		if (player == null || !IsStressPlayer(player))
		{
			return;
		}
		Player player2 = player;
		int expectedToken = _stressToken;
		GodotScheduler.Delay(Math.Max(hitDurationSeconds, 0.25f) + 0.05f, () =>
		{
			if (_stressToken == expectedToken && !_stressPlayedThisCombat && IsLocalTargetPlayer(target) && IsAlive(target))
			{
				Player player3 = target.Player;
				if (player3 != null && IsStressPlayer(player3) && player2 == player3 && TryPlayStressForPlayer(player3, volume))
				{
					MarkStressPlayed();
				}
			}
		});
	}

	private bool TryPlayStressForPlayer(Player player, float volume)
	{
		return _voiceGate.TryPlay("stress", _voice.StressPaths, (string path) => _voicePlayer.PlayFor(player, path, volume), 2f, 0.25f);
	}

	private void MarkStressPlayed()
	{
		_stressPlayedThisCombat = true;
		_criticalHpStressPending = false;
		_stressToken++;
	}

	private void CancelStressForDefeat()
	{
		_stressPlayedThisCombat = true;
		_criticalHpStressPending = false;
		_stressToken++;
	}

	private static bool IsHeavyDamage(int actualDamage, Creature target)
	{
		if (target.MaxHp > 0)
		{
			return actualDamage * 100 >= target.MaxHp * 30;
		}
		return false;
	}

	private static bool IsCriticalHp(Creature target)
	{
		if (target.MaxHp > 0)
		{
			return target.CurrentHp * 100 <= target.MaxHp * 20;
		}
		return false;
	}

	private static bool IsAlive(Creature target)
	{
		return target.IsAlive;
	}

	private bool TryPlay(string? resPath, float volume)
	{
		if (!string.IsNullOrWhiteSpace(resPath))
		{
			return _voicePlayer.Play(resPath, volume);
		}
		return false;
	}
}
