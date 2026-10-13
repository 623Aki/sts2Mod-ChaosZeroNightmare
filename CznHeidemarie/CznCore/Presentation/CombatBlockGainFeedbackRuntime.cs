using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatBlockGainFeedbackRuntime
{
	public const string CueAnimation = "block_gain_feedback";

	private readonly Func<Player, bool> _appliesTo;

	private readonly Func<Player, NCreature?> _resolveActor;

	private readonly Action<NCreature, int> _playEffect;

	private readonly Func<bool> _isSfxEnabled;

	private readonly Func<Player, bool> _isSfxEnabledFor;

	private readonly Func<IReadOnlyList<CombatPresentationSfxCue>?> _resolveSfxCues;

	private readonly Func<string, float, bool> _tryPlaySfx;

	private readonly string _guardContext;

	public CombatBlockGainFeedbackRuntime(Func<Player, bool> appliesTo, Func<Player, NCreature?> resolveActor, Action<NCreature, int> playEffect, Func<bool> isSfxEnabled, Func<IReadOnlyList<CombatPresentationSfxCue>?> resolveSfxCues, Func<string, float, bool> tryPlaySfx, string guardContext, Func<Player, bool>? isSfxEnabledFor = null)
	{
		_appliesTo = appliesTo ?? throw new ArgumentNullException("appliesTo");
		_resolveActor = resolveActor ?? throw new ArgumentNullException("resolveActor");
		_playEffect = playEffect ?? throw new ArgumentNullException("playEffect");
		_isSfxEnabled = isSfxEnabled ?? throw new ArgumentNullException("isSfxEnabled");
		_isSfxEnabledFor = isSfxEnabledFor ?? ((Func<Player, bool>)((Player _) => true));
		_resolveSfxCues = resolveSfxCues ?? throw new ArgumentNullException("resolveSfxCues");
		_tryPlaySfx = tryPlaySfx ?? throw new ArgumentNullException("tryPlaySfx");
		_guardContext = (string.IsNullOrWhiteSpace(guardContext) ? "Block gain effect-only feedback" : guardContext);
	}

	public void RegisterNativeSfxReplacement(NativeBlockGainSfxReplacementRequest request)
	{
		Player player = request.Receiver.Player;
		if (player == null || !_appliesTo(player) || !_isSfxEnabled() || !_isSfxEnabledFor(player))
		{
			return;
		}
		IReadOnlyList<CombatPresentationSfxCue> cues = _resolveSfxCues();
		if (cues != null && cues.Count != 0 && HasImmediateSfxCue(cues))
		{
			request.Register(() => TryPlayNativeSfxReplacement(player, cues));
		}
	}

	public void Schedule(BlockActuallyIncreasedEvent gameEvent)
	{
		if (gameEvent.CurrentBlock <= gameEvent.PreviousBlock)
		{
			return;
		}
		Player player = gameEvent.Receiver.Player;
		if (player == null || !_appliesTo(player))
		{
			return;
		}
		GodotScheduler.Deferred(() =>
		{
			SkinPatchGuard.Run(_guardContext, () =>
			{
				if (_appliesTo(player))
				{
					NCreature nCreature = _resolveActor(player);
					if (nCreature != null && GodotObject.IsInstanceValid(nCreature))
					{
						_playEffect(nCreature, -1);
					}
				}
			});
		});
	}

	private bool TryPlayNativeSfxReplacement(Player player, IReadOnlyList<CombatPresentationSfxCue> cues)
	{
		if (!_appliesTo(player) || !_isSfxEnabled() || !_isSfxEnabledFor(player))
		{
			return false;
		}
		bool flag = false;
		foreach (CombatPresentationSfxCue cue in cues)
		{
			if (cue.DelaySeconds <= 0f && !string.IsNullOrWhiteSpace(cue.Path))
			{
				flag |= _tryPlaySfx(cue.Path, cue.AdapterGainLinear);
			}
		}
		return flag;
	}

	private static bool HasImmediateSfxCue(IReadOnlyList<CombatPresentationSfxCue> cues)
	{
		foreach (CombatPresentationSfxCue cue in cues)
		{
			if (cue.DelaySeconds <= 0f && !string.IsNullOrWhiteSpace(cue.Path))
			{
				return true;
			}
		}
		return false;
	}
}
