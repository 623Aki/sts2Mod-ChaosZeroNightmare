using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed class CharacterContextVoiceRuntime
{
	private const float DefaultCategoryCooldownSeconds = 2f;

	private const float DefaultGlobalCooldownSeconds = 0.25f;

	private const float CurseCardCueCategoryCooldownSeconds = 0f;

	private const float CurseCardCueGlobalCooldownSeconds = 0f;

	private readonly CharacterVoiceProfile _voice;

	private readonly Func<Player?, bool> _appliesTo;

	private readonly CharacterSkinVoicePlayerRuntime _voicePlayer;

	private readonly CharacterSkinSfxPlayerRuntime? _sfxPlayer;

	private readonly CharacterContextCutInRuntime? _cutInRuntime;

	private readonly CharacterVoicePlaybackGate _voiceGate = new CharacterVoicePlaybackGate();

	public CharacterContextVoiceRuntime(CharacterSkinProfile profile, CharacterSkinVoicePlayerRuntime voicePlayer, CharacterSkinSfxPlayerRuntime? sfxPlayer = null, CharacterContextCutInRuntime? cutInRuntime = null)
		: this(GetVoiceProfile(profile), voicePlayer, profile.AppliesTo, sfxPlayer, cutInRuntime)
	{
	}

	public CharacterContextVoiceRuntime(CharacterVoiceProfile voice, CharacterSkinVoicePlayerRuntime voicePlayer, Func<Player?, bool> appliesTo, CharacterSkinSfxPlayerRuntime? sfxPlayer = null, CharacterContextCutInRuntime? cutInRuntime = null)
	{
		_voice = voice ?? throw new ArgumentNullException("voice");
		_appliesTo = appliesTo ?? throw new ArgumentNullException("appliesTo");
		_voicePlayer = voicePlayer ?? throw new ArgumentNullException("voicePlayer");
		_sfxPlayer = sfxPlayer;
		_cutInRuntime = cutInRuntime;
	}

	public void PrewarmContextCues()
	{
		CharacterCurseCardCueProfile curseCardPlayedCue = _voice.CurseCardPlayedCue;
		_voicePlayer.Prewarm(curseCardPlayedCue.VoicePaths);
		if (!string.IsNullOrWhiteSpace(curseCardPlayedCue.SfxPath))
		{
			_sfxPlayer?.Prewarm(curseCardPlayedCue.SfxPath);
		}
		if (!string.IsNullOrWhiteSpace(curseCardPlayedCue.CutInStateId))
		{
			_cutInRuntime?.Preload(curseCardPlayedCue.CutInStateId);
		}
	}

	public bool TryPlayTreasureOpen(float volume = 1f)
	{
		Player localRunPlayer = CharacterSkinVoicePlayerRuntime.GetLocalRunPlayer();
		if (localRunPlayer == null || !_appliesTo(localRunPlayer))
		{
			return false;
		}
		return TryPlayForPlayer("treasure_open", _voice.TreasureOpenPaths, localRunPlayer, volume);
	}

	public bool TryPlayPotionUsed(PotionModel? potion, Creature? target, float volume = 1f)
	{
		if (potion == null)
		{
			return false;
		}
		Player owner = potion.Owner;
		Creature creature = owner.Creature;
		if (IsTeammateTarget(creature, target))
		{
			return TryPlaySupportReceived(target, volume);
		}
		if (target == creature)
		{
			return TryPlayPotionForPlayer("potion_use_self", _voice.PotionUseSelfPaths, owner, volume);
		}
		if (IsEnemyPotion(potion, creature, target))
		{
			return TryPlayPotionForPlayer("potion_use_enemy", _voice.PotionUseEnemyPaths, owner, volume);
		}
		return false;
	}

	public bool TryPlaySkillCardSupportReceived(CardModel? card, Creature? target, float volume = 1f)
	{
		if (card == null || card.Type != CardType.Skill)
		{
			return false;
		}
		if (IsSingleTeammateSkillTarget(card, target))
		{
			return TryPlaySupportReceived(target, volume);
		}
		return false;
	}

	public bool TryPlayCurseCardPlayed(CardModel? card, float volume = 1f)
	{
		if (card == null || card.Type != CardType.Curse)
		{
			return false;
		}
		Player owner = card.Owner;
		if (!_appliesTo(owner) || !CharacterSkinVoicePlayerRuntime.Allows(owner))
		{
			return false;
		}
		CharacterCurseCardCueProfile curseCardPlayedCue = _voice.CurseCardPlayedCue;
		if (!curseCardPlayedCue.HasAnyCue)
		{
			return false;
		}
		bool flag = false;
		if (!string.IsNullOrWhiteSpace(curseCardPlayedCue.CutInStateId))
		{
			CharacterContextCutInRuntime? cutInRuntime = _cutInRuntime;
			if (cutInRuntime != null && cutInRuntime.TryPlay(owner, curseCardPlayedCue.CutInStateId))
			{
				flag = true;
			}
		}
		if (!string.IsNullOrWhiteSpace(curseCardPlayedCue.SfxPath) && _sfxPlayer != null && _sfxPlayer.IsEnabled)
		{
			_sfxPlayer.Play(curseCardPlayedCue.SfxPath, curseCardPlayedCue.SfxVolume);
			flag = true;
		}
		return TryPlayForPlayer("curse_card_played", curseCardPlayedCue.VoicePaths, owner, volume, 0f, 0f) | flag;
	}

	private bool TryPlaySupportReceived(Creature? target, float volume)
	{
		Player targetPlayer = target?.Player;
		if (targetPlayer == null || !_appliesTo(targetPlayer))
		{
			return false;
		}
		return TryPlay("teammate_support_received", _voice.TeammateSupportReceivedPaths, (string path) => _voicePlayer.PlayFor(targetPlayer, path, volume));
	}

	private bool TryPlaySupportReceived(Creature source, Creature? target, float volume)
	{
		if (IsTeammateTarget(source, target))
		{
			return TryPlaySupportReceived(target, volume);
		}
		return false;
	}

	private bool TryPlayPotionForPlayer(string category, IReadOnlyList<string> paths, Player player, float volume)
	{
		return TryPlayForPlayer(category, paths, player, volume, 2f, 0.25f, avoidImmediateRepeat: false);
	}

	private bool TryPlayForPlayer(string category, IReadOnlyList<string> paths, Player player, float volume, float categoryCooldownSeconds = 2f, float globalCooldownSeconds = 0.25f, bool avoidImmediateRepeat = true)
	{
		if (!_appliesTo(player))
		{
			return false;
		}
		return TryPlay(category, paths, (string path) => _voicePlayer.PlayFor(player, path, volume), categoryCooldownSeconds, globalCooldownSeconds, avoidImmediateRepeat);
	}

	private bool TryPlay(string category, IReadOnlyList<string> paths, Func<string, bool> play, float categoryCooldownSeconds = 2f, float globalCooldownSeconds = 0.25f, bool avoidImmediateRepeat = true)
	{
		return _voiceGate.TryPlay(category, paths, play, categoryCooldownSeconds, globalCooldownSeconds, 1f, avoidImmediateRepeat);
	}

	private static bool IsEnemyTarget(Creature source, Creature? target)
	{
		if (target != null)
		{
			return target.Side != source.Side;
		}
		return false;
	}

	private static bool IsEnemyPotion(PotionModel potion, Creature source, Creature? target)
	{
		bool flag = IsEnemyTarget(source, target);
		if (!flag)
		{
			TargetType targetType = potion.TargetType;
			bool flag2 = (uint)(targetType - 2) <= 2u;
			flag = flag2;
		}
		return flag;
	}

	private static bool IsSingleTeammateSkillTarget(CardModel card, Creature? target)
	{
		if (card.TargetType == TargetType.AnyAlly)
		{
			return IsTeammateTarget(card.Owner.Creature, target);
		}
		return false;
	}

	private static bool IsTeammateTarget(Creature source, Creature? target)
	{
		if (target != null && target.IsPlayer && target.Player != null && target != source)
		{
			return target.Side == source.Side;
		}
		return false;
	}

	private static CharacterVoiceProfile GetVoiceProfile(CharacterSkinProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile, "profile");
		return profile.Voice;
	}
}
