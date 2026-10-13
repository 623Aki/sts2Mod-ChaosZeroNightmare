using System.Collections.Generic;

namespace CznCore.Presentation;

internal readonly record struct CombatCameraShakeTrack(float StartSeconds, IReadOnlyList<CombatCameraShakeKey> Keys);
