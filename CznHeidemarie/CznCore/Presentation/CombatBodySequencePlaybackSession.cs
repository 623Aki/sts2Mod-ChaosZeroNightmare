using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatBodySequencePlaybackSession<TActor, TEffectTarget>(TActor? Actor, TEffectTarget? EffectTarget, IReadOnlyList<CombatPresentationBodyStep> PlaybackSteps, string? RestoreLoopAnimation, int Token, bool FromReady) where TActor : class where TEffectTarget : class;
