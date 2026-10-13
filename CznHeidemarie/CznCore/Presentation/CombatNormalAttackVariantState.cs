namespace CznCore.Presentation;

internal sealed class CombatNormalAttackVariantState
{
	private object? _room;

	private int _count;

	public int NextIndex(object? room)
	{
		if (_room != room)
		{
			_room = room;
			_count = 0;
		}
		int count = _count;
		_count++;
		return count;
	}
}
