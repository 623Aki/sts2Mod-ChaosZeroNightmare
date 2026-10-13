using System;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace CznCore.Presentation;

internal sealed class NativeBlockGainSfxReplacementRequest(Creature receiver)
{
	private Func<bool>? _tryReplace;

	public Creature Receiver { get; } = receiver ?? throw new ArgumentNullException("receiver");

	public bool HasReplacement => _tryReplace != null;

	public void Register(Func<bool> tryReplace)
	{
		ArgumentNullException.ThrowIfNull(tryReplace, "tryReplace");
		if (_tryReplace == null)
		{
			_tryReplace = tryReplace;
		}
	}

	public bool TryReplace()
	{
		return _tryReplace?.Invoke() ?? false;
	}
}
