using Godot;

namespace CznCore.Presentation;

internal readonly record struct CombatBodySurfaceState(string Reason, bool FeatureEnabled, bool CreatureValid, bool SpineFound, bool Applied, Vector2? SpineScale, string? SpineClass);
