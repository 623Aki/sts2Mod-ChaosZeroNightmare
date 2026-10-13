using Godot;

namespace CznCore.Presentation;

internal interface ICombatTransientEffectCueRouterHost<TActor, TTarget> where TActor : class where TTarget : class
{
	bool IsEnabled();

	bool IsEnabledFor(TActor? actor)
	{
		return true;
	}

	Node2D? FindOwnerAnchor(TActor actor);

	Node2D? FindTargetAnchor(TTarget? target);

	Node2D? FindTargetSurfaceAnchor(TTarget? target);

	Node2D? FindScreenAnchor();

	Node2D? FindScreenBackAnchor();

	void Queue(TActor actor, Node2D anchor, CombatPresentationEffectCue cue, int token);
}
