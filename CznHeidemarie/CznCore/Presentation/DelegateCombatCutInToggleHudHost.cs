using System;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal sealed class DelegateCombatCutInToggleHudHost : ICombatCutInToggleHudHost
{
	private readonly Func<Player?, bool> _isTargetPlayer;

	private readonly Func<bool> _isAvailable;

	private readonly Func<bool> _isCutInEnabled;

	private readonly Action<bool, bool> _setCutInEnabled;

	private readonly Func<string, string> _runtimeNodeName;

	private readonly Action<string, int> _warn;

	private readonly Action<string, int>? _debug;

	public DelegateCombatCutInToggleHudHost(Func<Player?, bool> isTargetPlayer, Func<bool> isAvailable, Func<bool> isCutInEnabled, Action<bool, bool> setCutInEnabled, Func<string, string> runtimeNodeName, Action<string, int> warn, Action<string, int>? debug)
	{
		_isTargetPlayer = isTargetPlayer ?? throw new ArgumentNullException("isTargetPlayer");
		_isAvailable = isAvailable ?? throw new ArgumentNullException("isAvailable");
		_isCutInEnabled = isCutInEnabled ?? throw new ArgumentNullException("isCutInEnabled");
		_setCutInEnabled = setCutInEnabled ?? throw new ArgumentNullException("setCutInEnabled");
		_runtimeNodeName = runtimeNodeName ?? throw new ArgumentNullException("runtimeNodeName");
		_warn = warn ?? throw new ArgumentNullException("warn");
		_debug = debug;
	}

	public bool IsTargetPlayer(Player? player)
	{
		return _isTargetPlayer(player);
	}

	public bool IsAvailable()
	{
		return _isAvailable();
	}

	public bool IsCutInEnabled()
	{
		return _isCutInEnabled();
	}

	public void SetCutInEnabled(bool enabled, bool persist)
	{
		_setCutInEnabled(enabled, persist);
	}

	public string RuntimeNodeName(string suffix)
	{
		return _runtimeNodeName(suffix);
	}

	public void Warn(string message, int priority)
	{
		_warn(message, priority);
	}

	public void Debug(string message, int priority)
	{
		_debug?.Invoke(message, priority);
	}
}
