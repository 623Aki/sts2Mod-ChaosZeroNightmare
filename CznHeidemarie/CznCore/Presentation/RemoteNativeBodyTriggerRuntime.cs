using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class RemoteNativeBodyTriggerRuntime
{
	private readonly CharacterSkinProfile _profile;

	private readonly CombatCreatureNodeResolver _creatures;

	private readonly CombatBodySurfaceCoordinator _bodySurface;

	private readonly Func<Player?, bool> _isLocalPlayer;

	private readonly Func<Player?, bool> _canPresentBody;

	private readonly Action<NCreature, string, bool>? _beforeSetAnimation;

	private readonly Action<NCreature, string>? _onCombatEntryStart;

	private readonly CombatBodySequenceRuntime _entryBodySequences;

	private readonly RemoteCardReadyBodyRuntime _cardReady;

	private readonly string _combatEntryPlayedMeta;

	private readonly string _combatEntryOffsetTokenMeta;

	private readonly HashSet<Player> _pendingCombatEntryPlayers = new HashSet<Player>();

	private int _combatEntryOffsetToken;

	public RemoteNativeBodyTriggerRuntime(CharacterSkinProfile profile, CombatCreatureNodeResolver creatures, CombatBodySurfaceCoordinator bodySurface, Func<Player?, bool> isLocalPlayer, Func<Player?, bool> canPresentBody, Func<CombatBodyReadyPresentationRuntime<Player, CardModel>> getReadyPresentation, Action<NCreature, string, bool>? beforeSetAnimation = null, Action<NCreature, string>? onCombatEntryStart = null)
	{
		RemoteNativeBodyTriggerRuntime remoteNativeBodyTriggerRuntime = this;
		_profile = profile;
		_creatures = creatures;
		_bodySurface = bodySurface;
		_isLocalPlayer = isLocalPlayer;
		_canPresentBody = (Player? player) => player != null && player.Creature.IsAlive && canPresentBody(player);
		_beforeSetAnimation = beforeSetAnimation;
		_onCombatEntryStart = onCombatEntryStart;
		_entryBodySequences = new CombatBodySequenceRuntime(profile.CombatPresentation, profile.CombatBody.ReadyExitAnimation, 0f);
		_cardReady = new RemoteCardReadyBodyRuntime((Player? player) => player != null && profile.AppliesTo(player) && !isLocalPlayer(player), _canPresentBody, getReadyPresentation, ResolveCardReadyTransitionSeconds(profile, _entryBodySequences), (string message, int priority) =>
		{
			SkinLog.Warn("Remote " + remoteNativeBodyTriggerRuntime._profile.ProfileId + " " + message, priority);
		});
		_combatEntryPlayedMeta = profile.RuntimeMetaKey("remote_combat_entry_played");
		_combatEntryOffsetTokenMeta = profile.RuntimeMetaKey("remote_combat_entry_offset_token");
	}

	public void AddCardReadyEventSubscriptions(ICollection<IDisposable> subscriptions, string contextPrefix)
	{
		_cardReady.AddEventSubscriptions(subscriptions, contextPrefix);
	}

	public bool TryConsumeCardReadyActionDelay(CardModel? card, out float delaySeconds)
	{
		return _cardReady.TryConsumeActionHandoffDelay(card, out delaySeconds);
	}

	public void InvalidatePlayerPresentation(Player player)
	{
		_pendingCombatEntryPlayers.Remove(player);
		_cardReady.ClearPlayer(player);
	}

	public void ScheduleCombatEntryForRemotePlayers(IEnumerable<Player>? players)
	{
		_pendingCombatEntryPlayers.Clear();
		if (players == null)
		{
			_cardReady.TrackPlayers(Array.Empty<Player>());
			return;
		}
		List<Player> list = new List<Player>();
		foreach (Player player in players)
		{
			if (player != null && _profile.AppliesTo(player) && !_isLocalPlayer(player))
			{
				list.Add(player);
			}
		}
		_cardReady.TrackPlayers(list);
		if (_profile.CombatBody.EntrySequence.Length == 0)
		{
			return;
		}
		foreach (Player item in list)
		{
			if (_canPresentBody(item))
			{
				_pendingCombatEntryPlayers.Add(item);
				TryPlayPendingCombatEntry(item);
			}
		}
	}

	public void TryPlayPendingCombatEntry(NCreature? creatureNode)
	{
		Player player = creatureNode?.Entity?.Player;
		if (player != null && _pendingCombatEntryPlayers.Contains(player))
		{
			TryPlayPendingCombatEntry(player);
		}
	}

	public bool TryPlay(Creature? creature, string triggerName, float waitTime, out Task handledTask)
	{
		handledTask = Task.CompletedTask;
		if (creature?.Player == null || !_profile.AppliesTo(creature.Player) || _isLocalPlayer(creature.Player) || !_canPresentBody(creature.Player) || !_profile.CombatBody.TryGetRemoteNativeBodyAnimation(triggerName, out var animation))
		{
			return false;
		}
		IReadOnlyList<string> readOnlyList = ResolveAnimationSequence(triggerName, animation);
		if (readOnlyList.Count == 0)
		{
			return false;
		}
		Player player = creature.Player;
		NCreature nCreature = _creatures.ResolvePlayer(NCombatRoom.Instance, player, "Remote " + _profile.ProfileId + " body trigger player node lookup failed", (string message) =>
		{
			SkinLog.Warn(message);
		});
		if (nCreature == null || !GodotObject.IsInstanceValid(nCreature))
		{
			return false;
		}
		ClearCombatEntryOffsetToken(nCreature);
		_bodySurface.Ensure(nCreature, "remote_native_body_trigger");
		if (IsActiveIdleTrigger(triggerName))
		{
			if (!TrySetRemoteIdleState(nCreature, player))
			{
				return false;
			}
			handledTask = WaitForHandledNativeBodyTrigger(waitTime);
			SkinLog.Info("Mapped remote " + _profile.ProfileId + " native body trigger=" + triggerName + " using synced card-ready state");
			return true;
		}
		foreach (string animationName in readOnlyList)
		{
			if (!_bodySurface.HasAnimation(nCreature, animationName, _profile.ProfileId + " remote combat"))
			{
				SkinLog.Debug(() => "Skipped unavailable remote " + _profile.ProfileId + " body animation: " + animationName);
				return false;
			}
		}
		bool loop = animation.Loop;
		if (!TrySetBodyAnimationSequence(nCreature, readOnlyList, loop))
		{
			return false;
		}
		if (!animation.Loop && animation.RestoreIdle)
		{
			AppendPostActionIdleSequence(nCreature, player);
		}
		handledTask = WaitForHandledNativeBodyTrigger(waitTime);
		SkinLog.Info("Mapped remote " + _profile.ProfileId + " native body trigger=" + triggerName + " animation=" + FormatAnimationSequence(readOnlyList));
		return true;
	}

	private void TryPlayPendingCombatEntry(Player player)
	{
		if (TryPlayCombatEntry(player))
		{
			_pendingCombatEntryPlayers.Remove(player);
		}
	}

	private bool TryPlayCombatEntry(Player player)
	{
		try
		{
			if (!_profile.AppliesTo(player) || _isLocalPlayer(player) || !_canPresentBody(player))
			{
				return true;
			}
			NCreature nCreature = _creatures.ResolvePlayer(NCombatRoom.Instance, player, "Remote " + _profile.ProfileId + " combat entry player node lookup failed", (string message) =>
			{
				SkinLog.Warn(message);
			});
			if (nCreature == null || !GodotObject.IsInstanceValid(nCreature))
			{
				return false;
			}
			if (nCreature.HasMeta(_combatEntryPlayedMeta))
			{
				return true;
			}
			if (!_bodySurface.Ensure(nCreature, "remote_combat_entry"))
			{
				return false;
			}
			if (!HasCombatEntryAnimations(nCreature))
			{
				return true;
			}
			IReadOnlyList<CombatPresentationBodyStep> readOnlyList = CombatBodyEntryPlaybackRuntime.BuildEntrySteps(_profile.CombatBody);
			CombatBodyEntryRunInStart entryRunIn = CombatBodyEntryPlaybackRuntime.TryBeginRunIn(_profile.CombatBody, readOnlyList, nCreature, (string message) =>
			{
				SkinLog.Debug(() => "Remote " + _profile.ProfileId + " " + message);
			}, (string message) =>
			{
				SkinLog.Warn("Remote " + _profile.ProfileId + " " + message);
			});
			int num = BeginCombatEntryOffsetToken(nCreature);
			if (!TrySetCombatEntrySequence(nCreature, readOnlyList))
			{
				ClearCombatEntryOffsetToken(nCreature, num);
				return true;
			}
			_onCombatEntryStart?.Invoke(nCreature, "remote_entry_start");
			QueueCombatEntrySegmentOffsets(nCreature, entryRunIn, readOnlyList, num);
			nCreature.SetMeta(_combatEntryPlayedMeta, Variant.From<bool>(true));
			SkinLog.Info("Queued remote " + _profile.ProfileId + " combat entry sequence=" + FormatAnimationSequence(_profile.CombatBody.EntrySequence) + " -> " + _profile.CombatBody.ActiveCombatIdleAnimation + " -> " + _profile.CombatBody.ReadyExitAnimation + " -> " + _profile.CombatBody.IdleAnimation);
			return true;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Remote " + _profile.ProfileId + " combat entry playback failed: " + SkinLog.FormatException(ex));
			return true;
		}
	}

	private IReadOnlyList<string> ResolveAnimationSequence(string triggerName, CharacterSkinRemoteNativeBodyAnimation mapping)
	{
		IReadOnlyList<string> animationSequence = mapping.AnimationSequence;
		if (animationSequence != null && animationSequence.Count > 0)
		{
			return mapping.AnimationSequence;
		}
		if (string.Equals(triggerName, "Dead", StringComparison.OrdinalIgnoreCase) && _profile.CombatBody.DeathSequence.Length != 0)
		{
			return _profile.CombatBody.DeathSequence;
		}
		if (!string.IsNullOrWhiteSpace(mapping.AnimationName))
		{
			return new string[1] { mapping.AnimationName };
		}
		return Array.Empty<string>();
	}

	private static float ResolveCardReadyTransitionSeconds(CharacterSkinProfile profile, CombatBodySequenceRuntime bodySequences)
	{
		string cardReady = profile.CombatPresentation.CommonStateIds.CardReady;
		if (!profile.CombatPresentation.TryGetState(cardReady, out CombatPresentationState state) || state.BodySteps.Count <= 0)
		{
			return 0f;
		}
		return bodySequences.GetSequenceDelaySeconds(state.BodySteps);
	}

	private bool TrySetBodyAnimation(NCreature creatureNode, string animationName, bool loop)
	{
		try
		{
			_beforeSetAnimation?.Invoke(creatureNode, animationName, loop);
			SpineAnimationCompat.SetAnimation(creatureNode.SpineAnimation, animationName, loop);
			return true;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Remote " + _profile.ProfileId + " body animation failed: " + animationName + " " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private bool TrySetBodyAnimationSequence(NCreature creatureNode, IReadOnlyList<string> animationSequence, bool loopLast)
	{
		if (animationSequence.Count == 0)
		{
			return false;
		}
		try
		{
			bool flag = (animationSequence.Count == 1) & loopLast;
			_beforeSetAnimation?.Invoke(creatureNode, animationSequence[0], flag);
			SpineAnimationCompat.SetAnimation(creatureNode.SpineAnimation, animationSequence[0], flag);
			for (int i = 1; i < animationSequence.Count; i++)
			{
				bool loop = (i == animationSequence.Count - 1) & loopLast;
				SpineAnimationCompat.AddAnimation(creatureNode.SpineAnimation, animationSequence[i], 0f, loop);
			}
			return true;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Remote " + _profile.ProfileId + " body animation sequence failed: " + FormatAnimationSequence(animationSequence) + " " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private bool TrySetCombatEntrySequence(NCreature creatureNode, IReadOnlyList<CombatPresentationBodyStep> entrySteps)
	{
		if (entrySteps.Count == 0)
		{
			return false;
		}
		string[] array = new string[entrySteps.Count];
		for (int i = 0; i < entrySteps.Count; i++)
		{
			array[i] = entrySteps[i].BodyAnimation;
		}
		if (!TrySetBodyAnimationSequence(creatureNode, array, loopLast: false))
		{
			return false;
		}
		AppendReadyExitToIdleSequence(creatureNode);
		return true;
	}

	private int BeginCombatEntryOffsetToken(NCreature creatureNode)
	{
		_combatEntryOffsetToken++;
		if (_combatEntryOffsetToken == 0)
		{
			_combatEntryOffsetToken = 1;
		}
		creatureNode.SetMeta(_combatEntryOffsetTokenMeta, Variant.From(in _combatEntryOffsetToken));
		return _combatEntryOffsetToken;
	}

	private void ClearCombatEntryOffsetToken(NCreature creatureNode, int? expectedToken = null, bool completeRunIn = true)
	{
		if (creatureNode == null || !GodotObject.IsInstanceValid(creatureNode))
		{
			return;
		}
		if (!creatureNode.HasMeta(_combatEntryOffsetTokenMeta) || (expectedToken.HasValue && creatureNode.GetMeta(_combatEntryOffsetTokenMeta).AsInt32() != expectedToken.Value))
		{
			return;
		}
		if (completeRunIn)
		{
			CombatBodyEntryPlaybackRuntime.CompleteActiveRunIn(creatureNode, "remote_entry_token_clear", (string message) =>
			{
				SkinLog.Debug(() => "Remote " + _profile.ProfileId + " " + message);
			}, (string message) =>
			{
				SkinLog.Warn("Remote " + _profile.ProfileId + " " + message);
			});
		}
		creatureNode.RemoveMeta(_combatEntryOffsetTokenMeta);
	}

	private bool IsCombatEntryOffsetTokenCurrent(NCreature creatureNode, int expectedToken)
	{
		if (creatureNode != null && GodotObject.IsInstanceValid(creatureNode) && creatureNode.HasMeta(_combatEntryOffsetTokenMeta))
		{
			return creatureNode.GetMeta(_combatEntryOffsetTokenMeta).AsInt32() == expectedToken;
		}
		return false;
	}

	private void QueueCombatEntrySegmentOffsets(NCreature creatureNode, CombatBodyEntryRunInStart entryRunIn, IReadOnlyList<CombatPresentationBodyStep> entrySteps, int entryOffsetToken)
	{
		CombatBodyEntryPlaybackRuntime.ScheduleCompletion(creatureNode, entryRunIn, _entryBodySequences.GetSequenceDelaySeconds(entrySteps), (string message) =>
		{
			SkinLog.Debug(() => "Remote " + _profile.ProfileId + " " + message);
		}, (string message) =>
		{
			SkinLog.Warn("Remote " + _profile.ProfileId + " " + message);
		});
		if (!entryRunIn.UsesSegmentOffsets || entrySteps.Count <= 1)
		{
			ClearCombatEntryOffsetToken(creatureNode, entryOffsetToken, completeRunIn: false);
			return;
		}
		IReadOnlyList<CombatBodySequenceStepSchedule> readOnlyList = _entryBodySequences.BuildStepSchedule(entrySteps);
		int num = -1;
		for (int num2 = 1; num2 < readOnlyList.Count; num2++)
		{
			if (CombatBodyEntryPlaybackRuntime.TryGetSegmentOffset(_profile.CombatBody, num2, out var _))
			{
				num = num2;
			}
		}
		if (num < 0)
		{
			ClearCombatEntryOffsetToken(creatureNode, entryOffsetToken, completeRunIn: false);
			return;
		}
		for (int num3 = 1; num3 < readOnlyList.Count; num3++)
		{
			if (!CombatBodyEntryPlaybackRuntime.TryGetSegmentOffset(_profile.CombatBody, num3, out var offset2))
			{
				continue;
			}
			bool isLastOffset = num3 == num;
			float delaySeconds = readOnlyList[num3].DelaySeconds;
			GodotScheduler.Delay(Math.Max(0.05f, delaySeconds), () =>
			{
				if (IsCombatEntryOffsetTokenCurrent(creatureNode, entryOffsetToken))
				{
					CombatBodyEntryPlaybackRuntime.ApplySegmentOffset(creatureNode, entryRunIn.TargetPosition, offset2);
					if (isLastOffset)
					{
						CombatBodyEntryPlaybackRuntime.CompleteRunInIfCurrent(creatureNode, entryRunIn, "remote_entry_last_segment", (string message) =>
						{
							SkinLog.Debug(() => "Remote " + _profile.ProfileId + " " + message);
						}, (string message) =>
						{
							SkinLog.Warn("Remote " + _profile.ProfileId + " " + message);
						});
						ClearCombatEntryOffsetToken(creatureNode, entryOffsetToken, completeRunIn: false);
					}
				}
			});
		}
	}

	private void AppendReadyExitToIdleSequence(NCreature creatureNode)
	{
		string activeCombatIdleAnimation = _profile.CombatBody.ActiveCombatIdleAnimation;
		TryAddBodyAnimation(creatureNode, activeCombatIdleAnimation, loop: false, "combat entry ready");
		string readyExitAnimation = _profile.CombatBody.ReadyExitAnimation;
		TryAddBodyAnimation(creatureNode, readyExitAnimation, loop: false, "combat entry ready exit");
		string idleAnimation = _profile.CombatBody.IdleAnimation;
		TryAddBodyAnimation(creatureNode, idleAnimation, loop: true, "combat entry idle");
	}

	private bool TrySetRemoteIdleState(NCreature creatureNode, Player player)
	{
		if (_cardReady.IsPresentationActive(player))
		{
			return true;
		}
		string idleAnimation = _profile.CombatBody.IdleAnimation;
		if (!string.IsNullOrWhiteSpace(idleAnimation) && _bodySurface.HasAnimation(creatureNode, idleAnimation, _profile.ProfileId + " remote synced idle"))
		{
			return TrySetBodyAnimation(creatureNode, idleAnimation, loop: true);
		}
		return false;
	}

	private void AppendPostActionIdleSequence(NCreature creatureNode, Player player)
	{
		if (!_cardReady.IsPresentationActive(player))
		{
			TryAddBodyAnimation(creatureNode, _profile.CombatBody.ReadyExitAnimation, loop: false, "post-action ready exit");
			TryAddBodyAnimation(creatureNode, _profile.CombatBody.IdleAnimation, loop: true, "post-action idle");
		}
	}

	private bool TryAddBodyAnimation(NCreature creatureNode, string? animationName, bool loop, string reason)
	{
		if (string.IsNullOrWhiteSpace(animationName) || !_bodySurface.HasAnimation(creatureNode, animationName, _profile.ProfileId + " remote " + reason))
		{
			return false;
		}
		try
		{
			SpineAnimationCompat.AddAnimation(creatureNode.SpineAnimation, animationName, 0f, loop);
			return true;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Remote " + _profile.ProfileId + " body animation queue failed: " + animationName + " " + SkinLog.FormatException(ex));
			return false;
		}
	}

	private bool HasCombatEntryAnimations(NCreature creatureNode)
	{
		string[] entrySequence = _profile.CombatBody.EntrySequence;
		foreach (string animationName in entrySequence)
		{
			if (!_bodySurface.HasAnimation(creatureNode, animationName, _profile.ProfileId + " remote combat entry"))
			{
				return false;
			}
		}
		string activeCombatIdleAnimation = _profile.CombatBody.ActiveCombatIdleAnimation;
		if (!string.IsNullOrWhiteSpace(activeCombatIdleAnimation) && !_bodySurface.HasAnimation(creatureNode, activeCombatIdleAnimation, _profile.ProfileId + " remote combat entry"))
		{
			return false;
		}
		return HasReadyExitOrIdleAnimation(creatureNode);
	}

	private bool HasReadyExitOrIdleAnimation(NCreature creatureNode)
	{
		string readyExitAnimation = _profile.CombatBody.ReadyExitAnimation;
		if (!string.IsNullOrWhiteSpace(readyExitAnimation) && !_bodySurface.HasAnimation(creatureNode, readyExitAnimation, _profile.ProfileId + " remote combat idle restore"))
		{
			return false;
		}
		string idleAnimation = _profile.CombatBody.IdleAnimation;
		if (!string.IsNullOrWhiteSpace(idleAnimation))
		{
			return _bodySurface.HasAnimation(creatureNode, idleAnimation, _profile.ProfileId + " remote combat idle restore");
		}
		return true;
	}

	private static string FormatAnimationSequence(IReadOnlyList<string> animationSequence)
	{
		return string.Join(" -> ", animationSequence);
	}

	private static bool IsActiveIdleTrigger(string triggerName)
	{
		return string.Equals(triggerName, "Idle", StringComparison.OrdinalIgnoreCase);
	}

	private static Task WaitForHandledNativeBodyTrigger(float waitTime)
	{
		float num = MathF.Max(0f, waitTime);
		return Cmd.CustomScaledWait(MathF.Min(num * 0.5f, 0.25f), num);
	}
}
