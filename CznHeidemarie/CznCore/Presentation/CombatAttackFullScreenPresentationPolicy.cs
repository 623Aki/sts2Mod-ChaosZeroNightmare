using System;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal static class CombatAttackFullScreenPresentationPolicy
{
	public static bool Allows(CharacterSkinPresentationGates gates, CombatPresentationAudiencePolicy audiencePolicy, CardModel? card, AttackCommand? attackCommand, object? combatState, int hitCount, CombatAttackEffectTargetResolver targetResolver, out CombatAttackLethalForecastResult forecast, Action<string>? warn = null, Action<string>? debug = null)
	{
		ArgumentNullException.ThrowIfNull(gates, "gates");
		ArgumentNullException.ThrowIfNull(audiencePolicy, "audiencePolicy");
		ArgumentNullException.ThrowIfNull(targetResolver, "targetResolver");
		if (!audiencePolicy.Allows(card, CombatPresentationChannel.CutIn))
		{
			forecast = new CombatAttackLethalForecastResult(CombatAttackLethalForecast.NotRequired, "full_screen_disabled");
			return false;
		}
		if (ResolveMode(gates, audiencePolicy, card?.Owner) == AttackFullScreenPresentationMode.Always)
		{
			forecast = new CombatAttackLethalForecastResult(CombatAttackLethalForecast.NotRequired, "mode_always");
			return true;
		}
		forecast = CombatAttackLethalForecastEvaluator.Evaluate(card, attackCommand, combatState, hitCount, targetResolver, warn);
		debug?.Invoke("Attack full-screen presentation forecast=" + forecast.Forecast.ToString() + " reason=" + forecast.Reason);
		return forecast.Forecast == CombatAttackLethalForecast.Lethal;
	}

	public static AttackFullScreenPresentationMode ResolveMode(CharacterSkinPresentationGates gates, CombatPresentationAudiencePolicy audiencePolicy, Player? player)
	{
		ArgumentNullException.ThrowIfNull(gates, "gates");
		ArgumentNullException.ThrowIfNull(audiencePolicy, "audiencePolicy");
		if (!audiencePolicy.IsTeammate(player))
		{
			return gates.AttackFullScreenPresentationMode;
		}
		return gates.TeammateAttackFullScreenPresentationMode;
	}
}
