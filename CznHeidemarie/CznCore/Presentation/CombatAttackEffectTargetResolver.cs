using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Presentation;

internal sealed class CombatAttackEffectTargetResolver
{
	private readonly FieldInfo? _attackerAnimDelayField = typeof(AttackCommand).GetField("_attackerAnimDelay", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo? _damagePerHitField = typeof(AttackCommand).GetField("_damagePerHit", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo? _calculatedDamageVarField = typeof(AttackCommand).GetField("_calculatedDamageVar", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly FieldInfo? _singleTargetField = typeof(AttackCommand).GetField("_singleTarget", BindingFlags.Instance | BindingFlags.NonPublic);

	private readonly MethodInfo? _getPossibleTargetsMethod = typeof(AttackCommand).GetMethod("GetPossibleTargets", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private readonly CombatOpponentTargetResolver _opponentTargets = new CombatOpponentTargetResolver();

	public float GetAttackAnimDelaySeconds(AttackCommand? attackCommand, Player? player, Action<string>? warn = null)
	{
		try
		{
			if (attackCommand != null && _attackerAnimDelayField?.GetValue(attackCommand) is float num && num > 0f)
			{
				return num;
			}
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat attack anim delay reflection failed: " + SkinLog.FormatException(ex));
		}
		return (player?.Character?.AttackAnimDelay).GetValueOrDefault();
	}

	public decimal GetDamagePerHit(AttackCommand? attackCommand, Creature? target = null, Action<string>? warn = null)
	{
		return GetDamagePerHitDetails(attackCommand, target, warn).Amount;
	}

	public CombatAttackDamagePerHit GetDamagePerHitDetails(AttackCommand? attackCommand, Creature? target = null, Action<string>? warn = null)
	{
		try
		{
			if (attackCommand == null)
			{
				return new CombatAttackDamagePerHit(0m, Array.Empty<AbstractModel>());
			}
			CardModel cardSource = attackCommand.ModelSource as CardModel;
			Creature target2 = ResolveDamageTarget(attackCommand, target, cardSource);
			decimal? num = null;
			if (_calculatedDamageVarField?.GetValue(attackCommand) is CalculatedDamageVar calculatedDamageVar)
			{
				num = calculatedDamageVar.Calculate(target2);
			}
			else if (_damagePerHitField?.GetValue(attackCommand) is decimal value)
			{
				num = value;
			}
			if (num.HasValue)
			{
				return ModifyDamagePerHit(attackCommand, target2, cardSource, num.Value);
			}
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat attack damage-per-hit reflection failed: " + SkinLog.FormatException(ex));
		}
		return new CombatAttackDamagePerHit(0m, Array.Empty<AbstractModel>());
	}

	private Creature? ResolveDamageTarget(AttackCommand attackCommand, Creature? target, CardModel? cardSource)
	{
		object obj = target;
		if (obj == null)
		{
			obj = _singleTargetField?.GetValue(attackCommand) as Creature;
			if (obj == null)
			{
				if (cardSource == null)
				{
					return null;
				}
				obj = cardSource.CurrentTarget;
			}
		}
		return (Creature?)obj;
	}

	private static CombatAttackDamagePerHit ModifyDamagePerHit(AttackCommand attackCommand, Creature? target, CardModel? cardSource, decimal damagePerHit)
	{
		Creature attacker = attackCommand.Attacker;
		if (target == null && attacker == null)
		{
			return new CombatAttackDamagePerHit(Math.Max(0m, damagePerHit), Array.Empty<AbstractModel>());
		}
		decimal val = HookCompat.ModifyDamage(IRunState.GetFrom(GetRunStateCreatures(target, attacker)), target?.CombatState ?? attacker?.CombatState ?? cardSource?.CombatState, target, attacker, damagePerHit, attackCommand.DamageProps, cardSource, ModifyDamageHookType.All, CardPreviewMode.None, out IEnumerable<AbstractModel> modifiers);
		return new CombatAttackDamagePerHit(Math.Max(0m, val), (modifiers as IReadOnlyList<AbstractModel>) ?? modifiers.ToArray());
	}

	private static IEnumerable<Creature> GetRunStateCreatures(Creature? target, Creature? dealer)
	{
		if (target != null)
		{
			yield return target;
		}
		if (dealer != null)
		{
			yield return dealer;
		}
	}

	public IReadOnlyList<Creature>? ResolveTargets(CardModel? card, AttackCommand? attackCommand, object? combatState, Action<string>? warn = null)
	{
		try
		{
			if (attackCommand != null && combatState != null && attackCommand.Attacker != null && !attackCommand.IsRandomlyTargeted)
			{
				if (attackCommand.IsMultiTargeted)
				{
					return _opponentTargets.ResolveAliveOpponents(combatState, attackCommand.Attacker, warn);
				}
				if (attackCommand.IsSingleTargeted)
				{
					Creature creature = NormalizeAttackTarget(_singleTargetField?.GetValue(attackCommand) as Creature) ?? NormalizeAttackTarget(card?.CurrentTarget);
					if (creature != null)
					{
						return new _003C_003Ez__ReadOnlySingleElementList<Creature>(creature);
					}
					if (_getPossibleTargetsMethod?.Invoke(attackCommand, null) is IReadOnlyList<Creature> { Count: 1 } readOnlyList && readOnlyList[0].IsAlive)
					{
						return new _003C_003Ez__ReadOnlySingleElementList<Creature>(readOnlyList[0]);
					}
				}
			}
			Creature creature2 = NormalizeAttackTarget(card?.CurrentTarget);
			if (creature2 != null)
			{
				return new _003C_003Ez__ReadOnlySingleElementList<Creature>(creature2);
			}
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat attack effect target resolution failed: " + SkinLog.FormatException(ex));
		}
		return null;
	}

	private static Creature? NormalizeAttackTarget(Creature? target)
	{
		if (target == null || !target.IsAlive)
		{
			return null;
		}
		return target;
	}
}
