using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record CombatPresentationAnimationFallback(string AnimationName, IReadOnlyList<string> FallbackAnimations);
