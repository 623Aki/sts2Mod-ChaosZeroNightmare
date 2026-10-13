using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatHitResponseGuardRuntime
{
	public static bool ShouldSuppressCardPlayHitResponse(int animationToken, int hitResponseGuardToken, CombatPresentationHitResponsePolicy policy, Player? targetPlayer, Creature? dealer, CardModel? cardSource, bool hasProtectedActivePlayback, out string suppressionReason)
	{
		suppressionReason = string.Empty;
		if (targetPlayer == null || hitResponseGuardToken <= 0 || hitResponseGuardToken != animationToken || policy != CombatPresentationHitResponsePolicy.SuppressDuringSelfDamage)
		{
			return false;
		}
		if (IsSelfDamageFromPlayer(targetPlayer, dealer, cardSource))
		{
			suppressionReason = "self_damage";
			return true;
		}
		if (hasProtectedActivePlayback)
		{
			suppressionReason = "active_card_playback_damage";
			return true;
		}
		return false;
	}

	public static bool IsSelfDamageFromPlayer(Player targetPlayer, Creature? dealer, CardModel? cardSource)
	{
		if (cardSource?.Owner == targetPlayer)
		{
			return true;
		}
		if (dealer != null && dealer.IsPlayer)
		{
			return dealer.Player == targetPlayer;
		}
		return false;
	}
}
