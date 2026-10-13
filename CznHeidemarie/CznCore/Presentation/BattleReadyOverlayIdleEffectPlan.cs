namespace CznCore.Presentation;

internal sealed record BattleReadyOverlayIdleEffectPlan(CombatPresentationEffectSource Source, float RootPositionX, float RootPositionY, float RootScale, string? AttachBoneName = null)
{
	public static BattleReadyOverlayIdleEffectPlan Scene(string scenePath, float rootPositionX, float rootPositionY, float rootScale)
	{
		return new BattleReadyOverlayIdleEffectPlan(CombatPresentationEffectSource.Scene(scenePath), rootPositionX, rootPositionY, rootScale);
	}

	public static BattleReadyOverlayIdleEffectPlan BoneAttachedScene(string scenePath, string attachBoneName, float rootScale = 1f)
	{
		return new BattleReadyOverlayIdleEffectPlan(CombatPresentationEffectSource.Scene(scenePath), 0f, 0f, rootScale, attachBoneName);
	}
}
