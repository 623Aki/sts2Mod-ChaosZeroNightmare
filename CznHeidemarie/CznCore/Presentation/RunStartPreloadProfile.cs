using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class RunStartPreloadProfile
{
	public CharacterSkinProfile SkinProfile { get; }

	public string DisplayName { get; }

	public Func<IReadOnlyList<RunStartPreloadItem>> BuildItems { get; }

	public IReadOnlyList<RunStartPreloadStep> FinalSteps { get; init; } = Array.Empty<RunStartPreloadStep>();

	public string LoadingTitle { get; init; }

	public string PreparingStatus { get; init; }

	public string ReadyStatus { get; init; } = "Ready";

	public float MinimumVisibleSeconds { get; init; } = 0.35f;

	public float ItemProgressEnd { get; init; } = 0.95f;

	public RunStartPreloadProfile(CharacterSkinProfile skinProfile, string displayName, Func<IReadOnlyList<RunStartPreloadItem>> buildItems)
	{
		SkinProfile = skinProfile ?? throw new ArgumentNullException("skinProfile");
		DisplayName = (string.IsNullOrWhiteSpace(displayName) ? skinProfile.ProfileId : displayName.Trim());
		BuildItems = buildItems ?? throw new ArgumentNullException("buildItems");
		LoadingTitle = "Loading " + DisplayName;
		PreparingStatus = "Preparing " + DisplayName + " assets";
	}
}
