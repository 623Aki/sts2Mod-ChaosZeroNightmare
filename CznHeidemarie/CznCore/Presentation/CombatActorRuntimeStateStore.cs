using System.Runtime.CompilerServices;

namespace CznCore.Presentation;

internal sealed class CombatActorRuntimeStateStore<TActor, TCard, TAttackSession> where TActor : class where TCard : class where TAttackSession : class
{
	private readonly ConditionalWeakTable<TActor, CombatActorRuntimeState<TCard, TAttackSession>> _states = new ConditionalWeakTable<TActor, CombatActorRuntimeState<TCard, TAttackSession>>();

	private readonly CombatActorRuntimeState<TCard, TAttackSession> _global = new CombatActorRuntimeState<TCard, TAttackSession>();

	public CombatActorRuntimeState<TCard, TAttackSession> Get(TActor? actor)
	{
		if (actor != null)
		{
			return _states.GetOrCreateValue(actor);
		}
		return _global;
	}
}
