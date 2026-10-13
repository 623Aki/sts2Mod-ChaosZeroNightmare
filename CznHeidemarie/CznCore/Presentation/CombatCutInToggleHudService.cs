using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznCore.Presentation;

internal static class CombatCutInToggleHudService
{
	private static CombatCutInToggleHudController? _controller;

	public static bool IsRegistered => _controller != null;

	public static void Register(CombatCutInToggleHudProfile profile, ICombatCutInToggleHudHost host)
	{
		_controller?.Cleanup();
		_controller = new CombatCutInToggleHudController(profile, host);
	}

	public static void Register(CombatCutInToggleHudProfile profile, Func<Player?, bool> isTargetPlayer, Func<bool> isAvailable, Func<bool> isCutInEnabled, Action<bool, bool> setCutInEnabled, Func<string, string> runtimeNodeName, Action<string, int> warn, Action<string, int>? debug = null)
	{
		Register(profile, new DelegateCombatCutInToggleHudHost(isTargetPlayer, isAvailable, isCutInEnabled, setCutInEnabled, runtimeNodeName, warn, debug));
	}

	public static void Activate(NCombatUi ui, CombatState state)
	{
		_controller?.Activate(ui, state);
	}

	public static void Enable(NCombatUi ui)
	{
		_controller?.SetCombatUiEnabled(enabled: true, ui);
	}

	public static void Disable()
	{
		_controller?.SetCombatUiEnabled(enabled: false);
	}

	public static void Deactivate()
	{
		_controller?.Cleanup();
	}
}
