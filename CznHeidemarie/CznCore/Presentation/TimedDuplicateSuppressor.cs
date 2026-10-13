namespace CznCore.Presentation;

internal sealed class TimedDuplicateSuppressor
{
	private ulong _lastAcceptedMs;

	public bool TryEnter(ulong nowMs, ulong suppressMs)
	{
		if (_lastAcceptedMs != 0L && nowMs >= _lastAcceptedMs && nowMs - _lastAcceptedMs < suppressMs)
		{
			return false;
		}
		_lastAcceptedMs = nowMs;
		return true;
	}
}
