using Godot;

namespace CznCore.Presentation;

internal sealed record CombatVideoCutInProfile(string StateId, string VideoPath, Vector2 SourceSize, float LifetimeSeconds, float DimAlpha = 0f, float FadeInSeconds = 0f, float FadeOutDelaySeconds = 0f, float FadeOutSeconds = 0f, float StartDelaySeconds = 0f, int CanvasLayer = 980);
