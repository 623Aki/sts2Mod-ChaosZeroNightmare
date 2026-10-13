using Godot;

namespace CznCore.Presentation;

internal sealed record CharacterSkinCombatEffectsProfile(Vector2 IdleScaleMultiplier, Vector2 IdleSourceScreenOffset, Color IdleModulate, Color BuffModulate, string IdleBackLayerName, string IdleFrontLayerName, string BehindParentLayerName, Vector2I IdleViewportSize, Vector2 IdleViewportOrigin, CombatEffectMaterialPolicyProfile MaterialPolicy, float TargetEffectAlphaMultiplier = 1f);
