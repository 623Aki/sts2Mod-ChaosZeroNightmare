using Godot;

namespace CznCore.Presentation;

internal interface IBattleReadyOverlayRuntimeContext<TCard> where TCard : class
{
	LeftSideCardPresentationPolicy<TCard> PresentationPolicy { get; }

	float BattleReadyScale { get; }

	float BattleReadyOpacity { get; }

	float BattleReadyOffsetX { get; }

	float BattleReadyOffsetY { get; }

	ISkinTimingProbe TimingProbe { get; }

	bool IsTargetCard(TCard? card);

	CombatPresentationCardRoute ResolveRoute(TCard card);

	Node? GetOverlayParent();

	Node2D? DetachIdleEffectLayer(Node sourceRoot, string childName);

	void ApplyIdleEffectMaterial(Node root);

	void ApplyIdleEffectMaterialDeferred(Node root, int frames);

	void StartIdleEffects(Node root);

	void LogInfo(string message)
	{
		SkinLog.Debug(message);
	}

	void LogWarn(string message)
	{
		SkinLog.Warn(message);
	}
}
