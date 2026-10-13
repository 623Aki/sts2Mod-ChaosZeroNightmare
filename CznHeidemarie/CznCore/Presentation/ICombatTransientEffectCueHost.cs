using Godot;

namespace CznCore.Presentation;

internal interface ICombatTransientEffectCueHost<TActor> where TActor : class
{
	bool IsEnabled();

	bool IsEnabledFor(TActor? actor)
	{
		return true;
	}

	bool IsTokenCurrent(TActor? actor, int token);

	bool IsActorValid(TActor? actor);

	void ClearExisting(Node2D spine);

	PackedScene? LoadScene(string scenePath, bool allowSynchronousSceneLoad);

	Node2D? AcquireRoot(CombatPresentationEffectCue cue, PackedScene scene);

	int PrepareRoot(Node2D effectRoot, Node2D spine, CombatPresentationEffectCue cue);

	bool IsEffectActive(Node2D effectRoot, int generation);

	bool MarkEffectVisible(Node2D effectRoot, int generation);

	void ReleaseRoot(Node2D effectRoot, int? generation);

	void StartEffect(Node2D effectRoot, CombatPresentationEffectCue cue);
}
