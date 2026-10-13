using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed record DefeatCgMotionLayerProfile(string Name, int ZIndex, Color Tint, bool Additive, IReadOnlyList<DefeatCgMotionKeyProfile> Keyframes);
