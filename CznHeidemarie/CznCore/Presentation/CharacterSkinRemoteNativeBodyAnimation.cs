using System.Collections.Generic;

namespace CznCore.Presentation;

internal readonly record struct CharacterSkinRemoteNativeBodyAnimation(string AnimationName, bool Loop = false, bool RestoreIdle = true, IReadOnlyList<string>? AnimationSequence = null);
