using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznCore.Presentation;

internal static class HookCompat
{
	private static readonly Type[] ModifyDamageCurrentParameterTypes = new Type[11]
	{
		typeof(IRunState),
		typeof(ICombatState),
		typeof(Creature),
		typeof(Creature),
		typeof(decimal),
		typeof(ValueProp),
		typeof(CardModel),
		typeof(CardPlay),
		typeof(ModifyDamageHookType),
		typeof(CardPreviewMode),
		typeof(IEnumerable<AbstractModel>).MakeByRefType()
	};

	private static readonly Type[] ModifyDamageLegacyParameterTypes = new Type[10]
	{
		typeof(IRunState),
		typeof(ICombatState),
		typeof(Creature),
		typeof(Creature),
		typeof(decimal),
		typeof(ValueProp),
		typeof(CardModel),
		typeof(ModifyDamageHookType),
		typeof(CardPreviewMode),
		typeof(IEnumerable<AbstractModel>).MakeByRefType()
	};

	private static readonly MethodInfo? ModifyDamageCurrentMethod = typeof(Hook).GetMethod("ModifyDamage", ModifyDamageCurrentParameterTypes);

	private static readonly MethodInfo ModifyDamageMethod = ModifyDamageCurrentMethod ?? typeof(Hook).GetMethod("ModifyDamage", ModifyDamageLegacyParameterTypes) ?? throw new MissingMethodException(typeof(Hook).FullName, "ModifyDamage");

	public static decimal ModifyDamage(IRunState runState, ICombatState? combatState, Creature? target, Creature? dealer, decimal damage, ValueProp props, CardModel? cardSource, ModifyDamageHookType modifyDamageHookType, CardPreviewMode previewMode, out IEnumerable<AbstractModel> modifiers)
	{
		object[] array = ((!(ModifyDamageCurrentMethod != null)) ? new object[10] { runState, combatState, target, dealer, damage, props, cardSource, modifyDamageHookType, previewMode, null } : new object[11]
		{
			runState, combatState, target, dealer, damage, props, cardSource, null, modifyDamageHookType, previewMode,
			null
		});
		object obj = Invoke(ModifyDamageMethod, array);
		modifiers = (array[^1] as IEnumerable<AbstractModel>) ?? Array.Empty<AbstractModel>();
		if (obj is decimal)
		{
			return (decimal)obj;
		}
		return damage;
	}

	private static object? Invoke(MethodInfo method, object?[] args)
	{
		try
		{
			return method.Invoke(null, args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}
}
