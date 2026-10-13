using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationCardContext(CombatPresentationCardRoute Route, string? CardId, string? CardName, string? CardType, string? TargetType, CombatPresentationTargetScope TargetScope, int EnergyCost = 0, bool CostsX = false, string? Rarity = null, int HitCount = 1, float AttackAnimDelaySeconds = 0f, decimal DamagePerHit = 0m, bool IsTargetCharacter = false, bool IsLocalPlayer = false, IReadOnlyList<string>? CardIdentifiers = null, bool IsUpgraded = false, int CurrentUpgradeLevel = 0, string? CardPoolTitle = null, string? CardPoolTypeName = null, string? CardPoolFullTypeName = null, bool IsColorlessCardPool = false, CombatPresentationCardSourceCategory SourceCategory = CombatPresentationCardSourceCategory.Unknown)
{
	public static CombatPresentationCardContext Unknown { get; } = new CombatPresentationCardContext(CombatPresentationCardRoute.Unknown, null, null, null, null, CombatPresentationTargetScope.Unknown);

	public bool IsAttack => Route == CombatPresentationCardRoute.Attack;

	public bool IsAreaTarget
	{
		get
		{
			CombatPresentationTargetScope targetScope = TargetScope;
			if (targetScope == CombatPresentationTargetScope.AllEnemies || targetScope == CombatPresentationTargetScope.All)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsSupplementalCardSource
	{
		get
		{
			CombatPresentationCardSourceCategory sourceCategory = SourceCategory;
			if ((uint)(sourceCategory - 2) <= 2u)
			{
				return true;
			}
			return false;
		}
	}
}
