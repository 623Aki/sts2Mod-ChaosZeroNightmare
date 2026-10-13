using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatCardPresentationProfile
{
	private readonly Dictionary<string, CombatCardPresentationOverride> _overridesByIdentifier = new Dictionary<string, CombatCardPresentationOverride>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<CombatPresentationCardRoute, HashSet<string>> _explicitRouteCards = new Dictionary<CombatPresentationCardRoute, HashSet<string>>();

	public IReadOnlyList<CombatCardPresentationOverride> Overrides { get; }

	public IReadOnlyList<string> BuffU3DescriptionKeywords { get; }

	public IReadOnlyList<string> EnemySkillDebuffDescriptionKeywords { get; }

	public CombatCardPresentationProfile(IReadOnlyList<CombatCardPresentationOverride> overrides, IReadOnlyDictionary<CombatPresentationCardRoute, IReadOnlyList<string>> explicitRouteCards, IReadOnlyList<string> buffU3DescriptionKeywords, IReadOnlyList<string> enemySkillDebuffDescriptionKeywords)
	{
		Overrides = overrides;
		BuffU3DescriptionKeywords = buffU3DescriptionKeywords;
		EnemySkillDebuffDescriptionKeywords = enemySkillDebuffDescriptionKeywords;
		foreach (CombatCardPresentationOverride @override in overrides)
		{
			foreach (string cardIdentifier in @override.CardIdentifiers)
			{
				if (!string.IsNullOrWhiteSpace(cardIdentifier))
				{
					_overridesByIdentifier[cardIdentifier] = @override;
				}
			}
		}
		foreach (KeyValuePair<CombatPresentationCardRoute, IReadOnlyList<string>> explicitRouteCard in explicitRouteCards)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string item in explicitRouteCard.Value)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					hashSet.Add(item);
				}
			}
			_explicitRouteCards[explicitRouteCard.Key] = hashSet;
		}
	}

	public bool TryGetRouteOverride(IEnumerable<string> cardIdentifiers, out CombatPresentationCardRoute route)
	{
		if (TryGetOverride(cardIdentifiers, out CombatCardPresentationOverride cardOverride) && cardOverride != null && cardOverride.Route.HasValue)
		{
			route = cardOverride.Route.Value;
			return true;
		}
		route = CombatPresentationCardRoute.Unknown;
		return false;
	}

	public bool MatchesExplicitRoute(IEnumerable<string> cardIdentifiers, CombatPresentationCardRoute route)
	{
		if (!_explicitRouteCards.TryGetValue(route, out HashSet<string> value))
		{
			return false;
		}
		foreach (string cardIdentifier in cardIdentifiers)
		{
			if (value.Contains(cardIdentifier))
			{
				return true;
			}
		}
		return false;
	}

	public bool HasBuffU3DescriptionSignal(string description)
	{
		return ContainsAny(description, BuffU3DescriptionKeywords);
	}

	public bool HasEnemySkillDebuffDescriptionSignal(string description)
	{
		return ContainsAny(description, EnemySkillDebuffDescriptionKeywords);
	}

	public bool TryGetRoutePlanOverride(CombatPresentationCardContext context, out CombatPresentationRoutePlan plan)
	{
		if (TryGetOverride(context.CardIdentifiers, out CombatCardPresentationOverride cardOverride) && cardOverride != null && cardOverride.RoutePlan != null)
		{
			plan = ApplyCutIn(cardOverride.RoutePlan, cardOverride.CutInStateId);
			return true;
		}
		plan = null;
		return false;
	}

	public bool TryGetAttackPlanOverride(CombatPresentationCardContext context, out CombatPresentationAttackPlan plan)
	{
		if (TryGetOverride(context.CardIdentifiers, out CombatCardPresentationOverride cardOverride) && cardOverride != null && cardOverride.AttackPlan != null)
		{
			plan = ApplyAttackPresentationOverride(cardOverride.AttackPlan, cardOverride);
			return true;
		}
		plan = null;
		return false;
	}

	public CombatPresentationAttackPlan ApplyAttackPresentationOverride(CombatPresentationCardContext context, CombatPresentationAttackPlan plan)
	{
		if (!TryGetOverride(context.CardIdentifiers, out CombatCardPresentationOverride cardOverride) || !(cardOverride != null))
		{
			return plan;
		}
		return ApplyAttackPresentationOverride(plan, cardOverride);
	}

	public bool TryGetCutInStateId(CombatPresentationCardContext context, out string cutInStateId)
	{
		if (TryGetOverride(context.CardIdentifiers, out CombatCardPresentationOverride cardOverride) && cardOverride != null && !string.IsNullOrWhiteSpace(cardOverride.CutInStateId))
		{
			cutInStateId = cardOverride.CutInStateId;
			return true;
		}
		cutInStateId = string.Empty;
		return false;
	}

	private bool TryGetOverride(IEnumerable<string>? cardIdentifiers, out CombatCardPresentationOverride? cardOverride)
	{
		if (cardIdentifiers != null)
		{
			foreach (string cardIdentifier in cardIdentifiers)
			{
				if (_overridesByIdentifier.TryGetValue(cardIdentifier, out cardOverride))
				{
					return true;
				}
			}
		}
		cardOverride = null;
		return false;
	}

	private static CombatPresentationRoutePlan ApplyCutIn(CombatPresentationRoutePlan plan, string? cutInStateId)
	{
		if (!string.IsNullOrWhiteSpace(cutInStateId))
		{
			return plan with
			{
				CutInStateId = cutInStateId
			};
		}
		return plan;
	}

	private static CombatPresentationAttackPlan ApplyCutIn(CombatPresentationAttackPlan plan, string? cutInStateId)
	{
		if (!string.IsNullOrWhiteSpace(cutInStateId))
		{
			return plan with
			{
				CutInStateId = cutInStateId
			};
		}
		return plan;
	}

	private static CombatPresentationAttackPlan ApplyAttackPresentationOverride(CombatPresentationAttackPlan plan, CombatCardPresentationOverride cardOverride)
	{
		CombatPresentationAttackPlan combatPresentationAttackPlan = ApplyCutIn(plan, cardOverride.CutInStateId);
		if (cardOverride.AttackVoiceCue != null)
		{
			combatPresentationAttackPlan = combatPresentationAttackPlan with
			{
				VoiceCue = cardOverride.AttackVoiceCue
			};
		}
		if (cardOverride.AttackOpeningHitDelaySeconds > 0f)
		{
			combatPresentationAttackPlan = combatPresentationAttackPlan with
			{
				OpeningHitDelaySeconds = Math.Max(0f, cardOverride.AttackOpeningHitDelaySeconds)
			};
		}
		return combatPresentationAttackPlan;
	}

	private static bool ContainsAny(string description, IReadOnlyList<string> keywords)
	{
		foreach (string keyword in keywords)
		{
			if (!string.IsNullOrWhiteSpace(keyword) && description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
