using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CharacterVoiceProfile(IReadOnlyList<string> PrewarmPaths, string? CombatStartPath, string? VictoryPath, string? HitPath, string? DefeatPath)
{
	public IReadOnlyList<string> ReadyIdlePaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> GrowthPaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> TreasureOpenPaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> PotionUseSelfPaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> PotionUseEnemyPaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> TeammateSupportReceivedPaths { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> StressPaths { get; init; } = Array.Empty<string>();

	public CharacterCurseCardCueProfile CurseCardPlayedCue { get; init; } = CharacterCurseCardCueProfile.Empty;

	public string? RestSiteHealPath { get; init; }
}
