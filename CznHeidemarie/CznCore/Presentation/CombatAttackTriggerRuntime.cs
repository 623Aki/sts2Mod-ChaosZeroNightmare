using System;

namespace CznCore.Presentation;

internal sealed class CombatAttackTriggerRuntime<TCard, TActor, TCreature, TCreatureNode> where TCard : class where TActor : class where TCreature : class where TCreatureNode : class
{
	public bool TryResolveAdvance(CombatAttackPlaybackSession<TCard, TActor, TCreature, TCreatureNode>? session, TCreature creature, string triggerName, float nativeWaitTime, CombatAttackQueuedHitStartDelayResolver<CombatAttackPlaybackSession<TCard, TActor, TCreature, TCreatureNode>> resolveQueuedHitStartDelaySeconds, CombatAttackHitDelayResolver resolveNativeHitDelaySeconds, CombatAttackHitDelayResolver resolveOfficialHitDelaySeconds, out CombatAttackTriggerAdvance advance)
	{
		ArgumentNullException.ThrowIfNull(resolveQueuedHitStartDelaySeconds, "resolveQueuedHitStartDelaySeconds");
		ArgumentNullException.ThrowIfNull(resolveNativeHitDelaySeconds, "resolveNativeHitDelaySeconds");
		ArgumentNullException.ThrowIfNull(resolveOfficialHitDelaySeconds, "resolveOfficialHitDelaySeconds");
		advance = default;
		if (session == null || !session.TryConsumeHitStep(creature, triggerName, out var hitStep))
		{
			return false;
		}
		float num = ((hitStep.HitIndex == 0) ? Math.Max(Math.Max(0f, session.Plan.OpeningHitDelaySeconds), Math.Max(0f, session.InitialHitStartDelaySeconds)) : Math.Max(0f, resolveQueuedHitStartDelaySeconds(session)));
		float num2 = Math.Max(0f, resolveNativeHitDelaySeconds(hitStep.AnimationName));
		if (session.Plan.UseFullHitAnimationWait)
		{
			num2 = Math.Max(num2, Math.Max(0f, resolveOfficialHitDelaySeconds(hitStep.AnimationName)));
		}
		advance = new CombatAttackTriggerAdvance(hitStep.HitIndex, hitStep.AnimationName, num, Math.Max(nativeWaitTime, num + num2));
		return true;
	}
}
