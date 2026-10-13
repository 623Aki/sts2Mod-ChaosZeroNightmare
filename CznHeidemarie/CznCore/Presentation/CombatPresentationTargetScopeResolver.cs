namespace CznCore.Presentation;

internal static class CombatPresentationTargetScopeResolver
{
	public static CombatPresentationTargetScope Resolve(string? targetType)
	{
		if (string.IsNullOrEmpty(targetType))
		{
			return CombatPresentationTargetScope.Unknown;
		}
		if (ContainsNormalized(targetType, "None"))
		{
			return CombatPresentationTargetScope.None;
		}
		if (ContainsNormalized(targetType, "Self"))
		{
			return CombatPresentationTargetScope.Self;
		}
		if (ContainsNormalized(targetType, "AllEnemies") || ContainsNormalized(targetType, "AllEnemy") || ContainsNormalized(targetType, "Aoe"))
		{
			return CombatPresentationTargetScope.AllEnemies;
		}
		if (ContainsNormalized(targetType, "Random") && ContainsNormalized(targetType, "Enemy"))
		{
			return CombatPresentationTargetScope.RandomEnemy;
		}
		if (ContainsNormalized(targetType, "All"))
		{
			return CombatPresentationTargetScope.All;
		}
		if (ContainsNormalized(targetType, "Enemy") || ContainsNormalized(targetType, "Monster") || ContainsNormalized(targetType, "Target"))
		{
			return CombatPresentationTargetScope.SingleEnemy;
		}
		return CombatPresentationTargetScope.Unknown;
	}

	private static bool ContainsNormalized(string value, string needle)
	{
		int num = 0;
		foreach (char c in value)
		{
			if ((c == ' ' || c == '-' || c == '_') ? true : false)
			{
				continue;
			}
			if (char.ToUpperInvariant(c) == char.ToUpperInvariant(needle[num]))
			{
				num++;
				if (num == needle.Length)
				{
					return true;
				}
			}
			else
			{
				num = ((char.ToUpperInvariant(c) == char.ToUpperInvariant(needle[0])) ? 1 : 0);
			}
		}
		return false;
	}
}
