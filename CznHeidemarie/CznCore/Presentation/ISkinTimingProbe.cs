namespace CznCore.Presentation;

internal interface ISkinTimingProbe
{
	long StartTimingProbe();

	void LogTimingProbe(string label, long startTimestamp);
}
