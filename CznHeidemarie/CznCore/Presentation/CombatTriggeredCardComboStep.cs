namespace CznCore.Presentation;

internal readonly record struct CombatTriggeredCardComboStep(int Index)
{
	public bool IsContinuation => Index > 0;
}
