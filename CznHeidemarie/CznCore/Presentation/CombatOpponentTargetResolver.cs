using System;
using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed class CombatOpponentTargetResolver
{
	private readonly Dictionary<Type, PropertyInfo?> _combatStateProperties = new Dictionary<Type, PropertyInfo>();

	private readonly Dictionary<Type, PropertyInfo?> _hittableEnemiesProperties = new Dictionary<Type, PropertyInfo>();

	private readonly Dictionary<Type, MethodInfo?> _getOpponentsOfMethods = new Dictionary<Type, MethodInfo>();

	private readonly object _reflectionCacheLock = new object();

	public IReadOnlyList<Creature>? ResolveAliveOpponents(object? combatState, Creature? actor, Action<string>? warn = null)
	{
		if (combatState == null || actor == null)
		{
			return null;
		}
		try
		{
			IEnumerable<Creature>? obj = (actor.IsPlayer ? (ResolveHittableEnemies(combatState) ?? ResolveOpponents(combatState, actor)) : ResolveOpponents(combatState, actor));
			List<Creature> list = new List<Creature>();
			HashSet<Creature> hashSet = new HashSet<Creature>();
			foreach (Creature item in obj)
			{
				if (item.IsAlive && hashSet.Add(item))
				{
					list.Add(item);
				}
			}
			return (list.Count == 0) ? null : list;
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat opponent target resolution failed: " + SkinLog.FormatException(ex));
			return null;
		}
	}

	public object? ResolveCombatState(CardModel? card, Creature? actor = null, Action<string>? warn = null)
	{
		try
		{
			if (actor == null)
			{
				actor = card?.Owner?.Creature;
			}
			return ResolvePropertyValue(actor, "CombatState", _combatStateProperties) ?? ResolvePropertyValue(card, "CombatState", _combatStateProperties);
		}
		catch (Exception ex)
		{
			warn?.Invoke("Combat state resolution failed: " + SkinLog.FormatException(ex));
			return null;
		}
	}

	private IEnumerable<Creature>? ResolveHittableEnemies(object combatState)
	{
		return ResolvePropertyValue(combatState, "HittableEnemies", _hittableEnemiesProperties) as IEnumerable<Creature>;
	}

	private IEnumerable<Creature> ResolveOpponents(object combatState, Creature actor)
	{
		if (!(GetCachedMethod(combatState.GetType(), "GetOpponentsOf", _getOpponentsOfMethods, new Type[1] { typeof(Creature) })?.Invoke(combatState, new object[1] { actor }) is IEnumerable<Creature> result))
		{
			return Array.Empty<Creature>();
		}
		return result;
	}

	private object? ResolvePropertyValue(object? source, string propertyName, Dictionary<Type, PropertyInfo?> cache)
	{
		if (source == null)
		{
			return null;
		}
		Type type = source.GetType();
		PropertyInfo value;
		lock (_reflectionCacheLock)
		{
			if (!cache.TryGetValue(type, out value))
			{
				value = (cache[type] = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			}
		}
		return value?.GetValue(source);
	}

	private MethodInfo? GetCachedMethod(Type sourceType, string methodName, Dictionary<Type, MethodInfo?> cache, Type[] parameterTypes)
	{
		lock (_reflectionCacheLock)
		{
			if (cache.TryGetValue(sourceType, out MethodInfo value))
			{
				return value;
			}
			return cache[sourceType] = sourceType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
		}
	}
}
