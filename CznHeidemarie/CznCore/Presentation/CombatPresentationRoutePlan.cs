using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationRoutePlan(CombatPresentationCardRoute Route, CombatPresentationRouteBodyMode BodyMode = CombatPresentationRouteBodyMode.None, string? StateId = null, IReadOnlyList<CombatPresentationBodyStep>? BodySteps = null, string? LoopAnimation = null, IReadOnlyList<CombatPresentationSfxCue>? SfxCues = null, CombatPresentationVoiceCue? VoiceCue = null, string? VariantId = null, CombatPresentationHitResponsePolicy HitResponsePolicy = CombatPresentationHitResponsePolicy.Allow, IReadOnlyList<string>? SuppressedNativeBodyTriggers = null, string? CutInStateId = null, float MinimumDurationSeconds = 0f);
