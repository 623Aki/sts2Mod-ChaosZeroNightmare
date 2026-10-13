using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace CznCore.Presentation;

internal sealed class CombatCardRouteResolver : ICombatPresentationRouteResolver<CardModel>
{
	private readonly CombatCardPresentationProfile _profile;

	public CombatCardRouteResolver(CombatCardPresentationProfile profile)
	{
		_profile = profile;
	}

	public CombatPresentationCardRoute Resolve(CardModel? card)
	{
		return Resolve(card, null);
	}

	public CombatPresentationCardRoute Resolve(CardModel? card, IReadOnlyList<string>? cardIdentifiers)
	{
		if (card == null)
		{
			return CombatPresentationCardRoute.Unknown;
		}
		IReadOnlyList<string> readOnlyList = cardIdentifiers ?? CombatCardIdentifiers.Collect(card);
		if (_profile.TryGetRouteOverride(readOnlyList, out var route))
		{
			return route;
		}
		if (ShouldUseGeneratedCardRoute(card))
		{
			return CombatPresentationCardRoute.GeneratedCard;
		}
		if (_profile.MatchesExplicitRoute(readOnlyList, CombatPresentationCardRoute.Skill))
		{
			return CombatPresentationCardRoute.Skill;
		}
		if (_profile.MatchesExplicitRoute(readOnlyList, CombatPresentationCardRoute.BuffU3))
		{
			return CombatPresentationCardRoute.BuffU3;
		}
		if (_profile.MatchesExplicitRoute(readOnlyList, CombatPresentationCardRoute.BuffU2))
		{
			return CombatPresentationCardRoute.BuffU2;
		}
		if (card.Type == CardType.Attack)
		{
			return CombatPresentationCardRoute.Attack;
		}
		if (card.Type == CardType.Skill)
		{
			return ResolveSkillRoute(card, readOnlyList);
		}
		if (card.Type == CardType.Power)
		{
			if (!HasBuffU3DescriptionSignal(GetCardDescriptionText(card)))
			{
				return CombatPresentationCardRoute.BuffU2;
			}
			return CombatPresentationCardRoute.BuffU3;
		}
		return CombatPresentationCardRoute.Unknown;
	}

	private static bool ShouldUseBlockSkillRoute(CardModel card)
	{
		if (card.Type == CardType.Skill)
		{
			return card.GainsBlock;
		}
		return false;
	}

	private static bool ShouldUseGeneratedCardRoute(CardModel card)
	{
		CardType type = card.Type;
		if ((uint)(type - 2) <= 1u)
		{
			return CombatGeneratedCardSignalDetector.HasGeneratedCardOnPlaySignal(card);
		}
		return false;
	}

	private CombatPresentationCardRoute ResolveSkillRoute(CardModel card, IReadOnlyList<string> identifiers)
	{
		if (ShouldUseBlockSkillRoute(card))
		{
			return CombatPresentationCardRoute.Skill;
		}
		if (ShouldUseEnemySkillDebuffRoute(card, identifiers, out string description))
		{
			return CombatPresentationCardRoute.EnemySkillDebuff;
		}
		if (description == null)
		{
			description = GetCardDescriptionText(card);
		}
		if (HasBuffU3DescriptionSignal(description))
		{
			return CombatPresentationCardRoute.BuffU3;
		}
		if (!IsBuffRouteSkill(card))
		{
			return CombatPresentationCardRoute.Skill;
		}
		return CombatPresentationCardRoute.BuffU2;
	}

	private bool ShouldUseEnemySkillDebuffRoute(CardModel card, IReadOnlyList<string> identifiers, out string? description)
	{
		description = null;
		if (!IsEnemyTargetedSkill(card))
		{
			return false;
		}
		if (ShouldUseAoeEnemySkillDebuffRoute(card))
		{
			return true;
		}
		if (_profile.MatchesExplicitRoute(identifiers, CombatPresentationCardRoute.EnemySkillDebuff) || CombatGeneratedCardSignalDetector.HasEnemyDebuffOnPlaySignal(card))
		{
			return true;
		}
		description = GetCardDescriptionText(card);
		return HasEnemySkillDebuffDescriptionSignal(description);
	}

	private static bool ShouldUseAoeEnemySkillDebuffRoute(CardModel card)
	{
		if (card.Type == CardType.Skill && !card.GainsBlock)
		{
			return card.TargetType == TargetType.AllEnemies;
		}
		return false;
	}

	private static bool IsEnemyTargetedSkill(CardModel card)
	{
		if (card.Type != CardType.Skill)
		{
			return false;
		}
		CombatPresentationTargetScope combatPresentationTargetScope = CombatPresentationTargetScopeResolver.Resolve(card.TargetType.ToString());
		if ((uint)(combatPresentationTargetScope - 3) <= 2u)
		{
			return true;
		}
		return false;
	}

	private static bool IsBuffRouteSkill(CardModel card)
	{
		TargetType targetType = card.TargetType;
		if ((uint)targetType <= 1u || (uint)(targetType - 5) <= 2u)
		{
			return true;
		}
		return false;
	}

	private bool HasBuffU3DescriptionSignal(string description)
	{
		return _profile.HasBuffU3DescriptionSignal(description);
	}

	private bool HasEnemySkillDebuffDescriptionSignal(string description)
	{
		return _profile.HasEnemySkillDebuffDescriptionSignal(description);
	}

	private static string GetCardDescriptionText(CardModel card)
	{
		try
		{
			PileType pileType = card.Pile?.Type ?? PileType.None;
			return card.GetDescriptionForPile(pileType, card.CurrentTarget) ?? string.Empty;
		}
		catch
		{
			return card.Description?.ToString() ?? string.Empty;
		}
	}
}
