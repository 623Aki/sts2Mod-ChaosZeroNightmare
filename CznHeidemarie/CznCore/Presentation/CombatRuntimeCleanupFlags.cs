using System;

namespace CznCore.Presentation;

[Flags]
internal enum CombatRuntimeCleanupFlags
{
	None = 0,
	StopWarmups = 1,
	LiveEffects = 2,
	PooledEffects = 4,
	SceneCaches = 8,
	SfxCache = 0x10,
	StopSfxPlayback = 0x20,
	NodeAnchorCaches = 0x40
}
