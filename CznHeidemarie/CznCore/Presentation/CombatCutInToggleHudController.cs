using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal sealed class CombatCutInToggleHudController : ICombatCutInToggleHudHost
{
	private readonly ICombatCutInToggleHudHost _host;

	private readonly CombatCutInToggleHudRuntime _runtime;

	private bool _available;

	private NCombatUi? _ui;

	public CombatCutInToggleHudController(CombatCutInToggleHudProfile profile, ICombatCutInToggleHudHost host)
	{
		_host = host ?? throw new ArgumentNullException("host");
		_runtime = new CombatCutInToggleHudRuntime(profile, this);
	}

	public void Activate(NCombatUi ui, CombatState state)
	{
		Player me = LocalContext.GetMe(state);
		_available = _host.IsTargetPlayer(me);
		_ui = ui;
		SetCombatUiEnabled(enabled: true, ui);
	}

	public void SetCombatUiEnabled(bool enabled, NCombatUi? ui = null)
	{
		if (ui != null)
		{
			_ui = ui;
		}
		_runtime.SetCombatUiEnabled(enabled, _ui);
	}

	public void Cleanup()
	{
		_available = false;
		_ui = null;
		_runtime.Cleanup();
	}

	public bool IsTargetPlayer(Player? player)
	{
		return _host.IsTargetPlayer(player);
	}

	public bool IsAvailable()
	{
		if (_available)
		{
			return _host.IsAvailable();
		}
		return false;
	}

	public bool IsCutInEnabled()
	{
		return _host.IsCutInEnabled();
	}

	public void SetCutInEnabled(bool enabled, bool persist)
	{
		_host.SetCutInEnabled(enabled, persist);
	}

	public string RuntimeNodeName(string suffix)
	{
		return _host.RuntimeNodeName(suffix);
	}

	public void Warn(string message, int priority)
	{
		_host.Warn(message, priority);
	}

	public void Debug(string message, int priority)
	{
		_host.Debug(message, priority);
	}
}
