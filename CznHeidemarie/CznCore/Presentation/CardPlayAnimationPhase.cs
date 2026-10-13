namespace CznCore.Presentation;

internal enum CardPlayAnimationPhase
{
	None,
	Begin,
	Guarded,
	Windup,
	Ready,
	ImpactScheduled,
	Impact,
	Recovery,
	Recycle,
	Complete,
	Cancelled
}
