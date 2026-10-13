using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznCore.Presentation;

internal static class CombatAttackLethalForecastEvaluator
{
	private const int MaxForecastHitCount = 100;

	private static readonly ConcurrentDictionary<(Type ModelType, string MethodName), bool> HookOverrideCache = new ConcurrentDictionary<(Type, string), bool>();

	public static CombatAttackLethalForecastResult Evaluate(CardModel? card, AttackCommand? attackCommand, object? combatState, int hitCount, CombatAttackEffectTargetResolver targetResolver, Action<string>? warn = null)
	{
		try
		{
			if (attackCommand == null || attackCommand.Attacker == null)
			{
				return Unknown("missing_attack_command");
			}
			if (attackCommand.IsRandomlyTargeted)
			{
				return Unknown("random_target");
			}
			if (hitCount <= 0 || hitCount > 100)
			{
				return Unknown("unsupported_hit_count");
			}
			IReadOnlyList<Creature> readOnlyList = targetResolver.ResolveTargets(card, attackCommand, combatState, warn);
			if (readOnlyList == null || readOnlyList.Count == 0)
			{
				return Unknown("target_unresolved");
			}
			bool flag = false;
			foreach (Creature item in readOnlyList)
			{
				CombatAttackLethalForecastResult result = EvaluateTarget(card, attackCommand, item, hitCount, targetResolver, warn);
				if (result.Forecast == CombatAttackLethalForecast.Lethal)
				{
					return result;
				}
				flag |= result.Forecast == CombatAttackLethalForecast.Unknown;
			}
			return flag ? Unknown("one_or_more_targets_unknown") : new CombatAttackLethalForecastResult(CombatAttackLethalForecast.NotLethal, "direct_damage_below_current_hp");
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat attack lethal forecast failed: " + SkinLog.FormatException(ex));
			return Unknown("forecast_exception");
		}
	}

	private static CombatAttackLethalForecastResult EvaluateTarget(CardModel? card, AttackCommand attackCommand, Creature target, int hitCount, CombatAttackEffectTargetResolver targetResolver, Action<string>? warn)
	{
		if (!target.IsAlive || target.CurrentHp <= 0)
		{
			return Unknown("target_not_alive");
		}
		Creature attacker = attackCommand.Attacker;
		CombatAttackDamagePerHit damagePerHitDetails = targetResolver.GetDamagePerHitDetails(attackCommand, target, warn);
		decimal amount = damagePerHitDetails.Amount;
		if (amount <= 0m)
		{
			return new CombatAttackLethalForecastResult(CombatAttackLethalForecast.NotLethal, "no_direct_damage");
		}
		decimal val = (target.PetOwner?.Creature ?? target).Block;
		int num = target.CurrentHp;
		ICombatState combatState = target.CombatState ?? attacker.CombatState;
		IRunState runState = IRunState.GetFrom(GetRunStateCreatures(target, attacker));
		ValueProp damageProps = attackCommand.DamageProps;
		if (HasHookOverride(target, "BeforeDamageReceived"))
		{
			return Unknown("before_damage_reaction");
		}
		if (hitCount > 1 && (damagePerHitDetails.Modifiers.Any((AbstractModel modifier) => OverridesHook(modifier, "AfterModifyingDamageAmount")) || HasInterHitReaction(target)))
		{
			return Unknown("stateful_multi_hit_reaction");
		}
		for (int num2 = 0; num2 < hitCount; num2++)
		{
			decimal num3 = (damageProps.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(val, amount));
			val -= (decimal)(int)num3;
			decimal num4 = Math.Max(amount - num3, 0m);
			if (num4 <= 0m)
			{
				continue;
			}
			decimal num5 = Hook.ModifyHpLost(runState, combatState, target, num4, damageProps, attacker, card, HpLossHookPhase.BeforeOsty, out IEnumerable<AbstractModel> modifiers);
			if (num5 != num4 || modifiers.Any())
			{
				return Unknown("hp_loss_modifier");
			}
			Creature creature = ((combatState == null) ? target : Hook.ModifyUnblockedDamageTarget(combatState, target, num5, damageProps, attacker));
			if (creature != target)
			{
				return Unknown("damage_redirected");
			}
			decimal num6 = Hook.ModifyHpLost(runState, combatState, creature, num5, damageProps, attacker, card, HpLossHookPhase.AfterOsty, out IEnumerable<AbstractModel> modifiers2);
			if (num6 != num5 || modifiers2.Any())
			{
				return Unknown("hp_loss_modifier");
			}
			if (num > 0 && num6 >= (decimal)num)
			{
				if (!Hook.ShouldDie(runState, combatState, target, out AbstractModel _))
				{
					return Unknown("death_prevented");
				}
				if (combatState != null && !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, target))
				{
					return Unknown("death_not_final");
				}
				return new CombatAttackLethalForecastResult(CombatAttackLethalForecast.Lethal, "direct_damage_reaches_current_hp");
			}
			int num7 = (int)Math.Clamp(num6, 0m, 999999999m);
			num = Math.Max(num - num7, 0);
		}
		return new CombatAttackLethalForecastResult(CombatAttackLethalForecast.NotLethal, "direct_damage_below_current_hp");
	}

	private static IEnumerable<Creature> GetRunStateCreatures(Creature target, Creature dealer)
	{
		yield return target;
		if (target != dealer)
		{
			yield return dealer;
		}
	}

	private static bool HasInterHitReaction(Creature target)
	{
		if (!HasHookOverride(target, "AfterDamageReceived") && !HasHookOverride(target, "AfterBlockBroken"))
		{
			return HasHookOverride(target, "AfterCurrentHpChanged");
		}
		return true;
	}

	private static bool HasHookOverride(Creature target, string methodName)
	{
		if (target.Monster != null && OverridesHook(target.Monster, methodName))
		{
			return true;
		}
		return target.Powers.Any((PowerModel power) => OverridesHook(power, methodName));
	}

	private static bool OverridesHook(AbstractModel model, string methodName)
	{
		return HookOverrideCache.GetOrAdd((model.GetType(), methodName), ((Type ModelType, string MethodName) key) =>
		{
			MethodInfo method = key.ModelType.GetMethod(key.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return method != null && method.DeclaringType != typeof(AbstractModel);
		});
	}

	private static CombatAttackLethalForecastResult Unknown(string reason)
	{
		return new CombatAttackLethalForecastResult(CombatAttackLethalForecast.Unknown, reason);
	}
}
