using Godot;

namespace CznCore.Presentation;

internal interface ICombatWarmupHost
{
	bool AreEffectsEnabled();

	bool IsSfxEnabled();

	int CountEffectTargets();

	string GetPoolKey(CombatPresentationEffectCue cue);

	int GetUsablePooledCount(string poolKey);

	PackedScene? LoadScene(string scenePath);

	bool PreloadScene(string scenePath, bool allowSynchronousFallback);

	Node2D? AcquireRoot(CombatPresentationEffectCue cue, PackedScene scene, bool warmup);

	bool PrimeRoot(Node2D effectRoot, CombatPresentationEffectCue cue);

	void ReleaseRoot(Node2D effectRoot);

	bool PrewarmSfx(string path);

	bool ShouldDeferWarmup();
}
