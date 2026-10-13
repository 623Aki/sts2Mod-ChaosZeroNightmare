using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class CombatDamageVoiceSubscriptions
{
	public static void Add(ICollection<IDisposable> subscriptions, CharacterVoiceProfile voice, CombatDamageVoiceRuntime damageVoice, string contextPrefix, Action<string> logPlayed)
	{
		ArgumentNullException.ThrowIfNull(subscriptions, "subscriptions");
		ArgumentNullException.ThrowIfNull(voice, "voice");
		ArgumentNullException.ThrowIfNull(damageVoice, "damageVoice");
		ArgumentNullException.ThrowIfNull(logPlayed, "logPlayed");
		if (voice.StressPaths.Count == 0)
		{
			return;
		}
		subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Combat stress voice reset"), (CombatStartingEvent _) =>
		{
			damageVoice.ResetCombatStressState();
		}));
		subscriptions.Add(SkinEventHub.Subscribe(ContextName(contextPrefix, "Critical stress voice"), (PlayerTurnStartedEvent gameEvent) =>
		{
			if (damageVoice.TryPlayPendingCriticalStress(gameEvent.Player))
			{
				logPlayed("stress voice");
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
