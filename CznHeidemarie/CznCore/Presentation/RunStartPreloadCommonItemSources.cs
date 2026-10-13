using System;
using System.Threading.Tasks;
using Godot;

namespace CznCore.Presentation;

internal sealed record RunStartPreloadCommonItemSources(CharacterSkinProfile SkinProfile, Func<bool> EffectsEnabled)
{
	public RunStartPreloadCommonItemPolicy Policy { get; init; } = RunStartPreloadCommonItemPolicy.Default;

	public Func<bool>? IdleEffectsEnabled { get; init; }

	public Func<Node, Task>? PreloadBodyTexture { get; init; }

	public Func<Node, Task>? PreloadIdleEffectsScene { get; init; }

	public Action<RunStartPreloadItemBuilder>? AddBeforeEffectCues { get; init; }

	public Action<RunStartPreloadItemBuilder, CombatPresentationEffectCue>? AddEffectCue { get; init; }
}
