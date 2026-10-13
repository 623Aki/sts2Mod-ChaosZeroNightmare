using Godot;

namespace CznCore.Presentation;

internal sealed record CombatEffectMaterialPolicyProfile(Color IdleTint, float IdleBrightness, float IdleAlphaBoost, float IdleAlphaPower, Color AttackTint, float AttackBrightness, float AttackAlphaBoost, float AttackAlphaPower, float IdleBackLayerAlpha, CombatEffectViewportMaterialProfile IdleBackViewport, CombatEffectViewportMaterialProfile IdleFrontViewport);
