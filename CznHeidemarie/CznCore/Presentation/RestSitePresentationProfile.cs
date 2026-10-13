using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed record RestSitePresentationProfile(string PrimaryAnimationName, IReadOnlyList<string> FallbackAnimationNames, string VoicePath, int VoiceCooldownMs, Vector2 BodyOffset, string BodyBasePositionMeta, IReadOnlyList<RestSiteCampingLayerProfile> CampingLayers, RestSiteSurfaceProfile? Surface = null, IReadOnlyList<string>? HiddenSlotNames = null);
