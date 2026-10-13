namespace CznCore.Presentation;

internal sealed record CombatRoutePlaybackSession<TActor>(TActor? Actor, CombatPresentationRoutePlan Plan, int Token, float GuardReleaseDelaySeconds, bool SuppressesNativeBodyTriggers) where TActor : class;
