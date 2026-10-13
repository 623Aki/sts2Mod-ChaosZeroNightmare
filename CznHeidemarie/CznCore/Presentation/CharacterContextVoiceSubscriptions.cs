using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CharacterContextVoiceSubscriptions
{
	public static void Add(ICollection<IDisposable> subscriptions, CharacterVoiceProfile voice, CharacterContextVoiceRuntime contextVoice, string contextPrefix, Action<string> logPlayed)
	{
		ArgumentNullException.ThrowIfNull(subscriptions, "subscriptions");
		ArgumentNullException.ThrowIfNull(voice, "voice");
		ArgumentNullException.ThrowIfNull(contextVoice, "contextVoice");
		ArgumentNullException.ThrowIfNull(logPlayed, "logPlayed");
		if (voice.TreasureOpenPaths.Count > 0)
		{
			subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Treasure-open voice"), (TreasureChestOpeningEvent _) =>
			{
				if (contextVoice.TryPlayTreasureOpen())
				{
					logPlayed("treasure-open voice");
				}
			}));
		}
		if (voice.PotionUseSelfPaths.Count > 0 || voice.PotionUseEnemyPaths.Count > 0 || voice.TeammateSupportReceivedPaths.Count > 0)
		{
			subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Potion voice"), (PotionUsedEvent gameEvent) =>
			{
				if (contextVoice.TryPlayPotionUsed(gameEvent.Potion, gameEvent.Target))
				{
					logPlayed("potion voice");
				}
			}));
		}
		if (voice.TeammateSupportReceivedPaths.Count > 0)
		{
			subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Teammate skill support voice"), (CardPlayedEvent gameEvent) =>
			{
				if (gameEvent.CardPlay.IsFirstInSeries && contextVoice.TryPlaySkillCardSupportReceived(gameEvent.CardPlay.Card, gameEvent.CardPlay.Target))
				{
					logPlayed("teammate support voice");
				}
			}));
		}
		if (!voice.CurseCardPlayedCue.HasAnyCue)
		{
			return;
		}
		subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Curse-card collapse cue"), (LocalCardPlayedEvent gameEvent) =>
		{
			if (contextVoice.TryPlayCurseCardPlayed(gameEvent.Card))
			{
				logPlayed("curse-card collapse cue");
			}
		}));
	}

	private static string ContextName(string prefix, string label)
	{
		if (string.IsNullOrWhiteSpace(prefix))
		{
			return label;
		}
		return prefix.Trim() + " " + char.ToLowerInvariant(label[0]) + label.Substring(1, label.Length - 1);
	}
}
