using System;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed class CombatPresentationAudiencePolicy
{
	private readonly ICombatPresentationAudienceSettings _settings;

	private readonly Func<Player?, bool> _isTargetPlayer;

	public CombatPresentationAudiencePolicy(ICombatPresentationAudienceSettings settings, Func<Player?, bool> isTargetPlayer)
	{
		_settings = settings ?? throw new ArgumentNullException("settings");
		_isTargetPlayer = isTargetPlayer ?? throw new ArgumentNullException("isTargetPlayer");
	}

	public CombatPresentationAudience Resolve(Player? player)
	{
		if (player == null || !_isTargetPlayer(player))
		{
			return CombatPresentationAudience.Unsupported;
		}
		if (!LocalContext.IsMe(player))
		{
			return CombatPresentationAudience.Teammate;
		}
		return CombatPresentationAudience.LocalPlayer;
	}

	public bool IsLocal(Player? player)
	{
		return Resolve(player) == CombatPresentationAudience.LocalPlayer;
	}

	public bool IsTeammate(Player? player)
	{
		return Resolve(player) == CombatPresentationAudience.Teammate;
	}

	public bool Allows(Player? player, CombatPresentationChannel channel)
	{
		return Resolve(player) switch
		{
			CombatPresentationAudience.LocalPlayer => channel != CombatPresentationChannel.CutIn || _settings.LocalCutInEnabled, 
			CombatPresentationAudience.Teammate => AllowsTeammateChannel(channel), 
			_ => false, 
		};
	}

	public bool Allows(CardModel? card, CombatPresentationChannel channel)
	{
		if (card?.Owner != null)
		{
			return Allows(card.Owner, channel);
		}
		return false;
	}

	public CombatPresentationRoutePlan ApplyCutInPolicy(Player? player, CombatPresentationRoutePlan plan)
	{
		if (!Allows(player, CombatPresentationChannel.CutIn))
		{
			return CombatCutInPresentationPolicy.StripCutInPresentation(plan);
		}
		return plan;
	}

	public CombatPresentationAttackPlan ApplyCutInPolicy(Player? player, CombatPresentationAttackPlan plan)
	{
		if (!Allows(player, CombatPresentationChannel.CutIn))
		{
			return CombatCutInPresentationPolicy.StripCutInPresentation(plan);
		}
		return plan;
	}

	private bool AllowsTeammateChannel(CombatPresentationChannel channel)
	{
		if (!_settings.TeammatePresentationEnabled)
		{
			return false;
		}
		return channel switch
		{
			CombatPresentationChannel.Body => true, 
			CombatPresentationChannel.Effects => _settings.TeammateEffectsEnabled, 
			CombatPresentationChannel.CombatSfx => _settings.TeammateCombatSfxEnabled, 
			CombatPresentationChannel.Voice => _settings.TeammateVoiceEnabled, 
			CombatPresentationChannel.CutIn => _settings.TeammateCutInEnabled, 
			_ => false, 
		};
	}
}
