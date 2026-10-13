using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed record DefeatFrameSequenceLayerProfile(string Name, IReadOnlyList<string> FramePaths, int ZIndex, bool Additive, float Alpha, float StartDelaySeconds, float FrameSeconds, float VisibleSeconds, float RuntimeScaleAdapter = 1f);
