using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal sealed class CombatAttackPlaybackSession<TCard, TActor, TCreature, TCreatureNode> where TCard : class where TActor : class where TCreature : class where TCreatureNode : class
{
	private const string AttackTriggerName = "Attack";

	private const string HeavyAttackTriggerName = "heavyAttack";

	private const string CastTriggerName = "Cast";

	private const string HitTriggerName = "Hit";

	public TCard? Card { get; init; }

	public TActor? Actor { get; init; }

	public TCreature? ActorCreature { get; init; }

	public TCreatureNode? OwnerCreatureNode { get; init; }

	public IReadOnlyList<TCreature>? EffectTargets { get; init; }

	public IReadOnlyList<TCreatureNode>? EffectTargetNodes { get; init; }

	public CombatPresentationCardContext Context { get; init; } = CombatPresentationCardContext.Unknown;

	public CombatPresentationAttackPlan Plan { get; init; }

	public int HitCount { get; init; }

	public int Token { get; init; }

	public float InitialHitStartDelaySeconds { get; init; }

	private int NextHitIndex { get; set; }

	public bool HasConsumedFinalHit
	{
		get
		{
			if (NextHitIndex > 0)
			{
				return NextHitIndex >= HitCount;
			}
			return false;
		}
	}

	public bool Matches(TCard? card, TActor? actor)
	{
		if (Card == card)
		{
			return Actor == actor;
		}
		return false;
	}

	public bool TryConsumeHitStep(TCreature creature, string triggerName, out CombatAttackHitStep hitStep)
	{
		hitStep = default;
		if (ActorCreature != creature || !IsAttackStepTrigger(triggerName) || NextHitIndex >= HitCount)
		{
			return false;
		}
		int nextHitIndex = NextHitIndex;
		hitStep = new CombatAttackHitStep(nextHitIndex, Plan.GetHitAnimationName(nextHitIndex));
		NextHitIndex++;
		return true;
	}

	private bool IsAttackStepTrigger(string triggerName)
	{
		if (string.Equals(triggerName, "Hit", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (Plan.SuppressedNativeBodyTriggers != null)
		{
			foreach (string suppressedNativeBodyTrigger in Plan.SuppressedNativeBodyTriggers)
			{
				if (string.Equals(suppressedNativeBodyTrigger, triggerName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		if (!string.Equals(triggerName, "Attack", StringComparison.OrdinalIgnoreCase) && !string.Equals(triggerName, "heavyAttack", StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(triggerName, "Cast", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public bool TryGetLastConsumedHitAnimation(out string animationName)
	{
		animationName = string.Empty;
		if (NextHitIndex <= 0)
		{
			return false;
		}
		animationName = Plan.GetHitAnimationName(NextHitIndex - 1);
		return true;
	}

	public TCreatureNode? ResolveOwnerCreatureNode(Func<TCreatureNode?, bool> isUsable, Func<TCreatureNode?> resolveFallback)
	{
		if (isUsable(OwnerCreatureNode))
		{
			return OwnerCreatureNode;
		}
		return resolveFallback();
	}

	public IReadOnlyList<TCreatureNode>? ResolveEffectTargetNodes(Func<IReadOnlyList<TCreature>?, IReadOnlyList<TCreatureNode>?> resolveFallback)
	{
		if (EffectTargetNodes != null && EffectTargetNodes.Count > 0)
		{
			return EffectTargetNodes;
		}
		return resolveFallback(EffectTargets);
	}
}
