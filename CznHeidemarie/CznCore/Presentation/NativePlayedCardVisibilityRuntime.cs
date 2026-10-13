using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznCore.Presentation;

internal sealed class NativePlayedCardVisibilityRuntime<TCard> where TCard : class
{
	private static readonly bool EnableNativePlayedCardHiding = true;

	private readonly string _playContainerModulateMeta;

	private readonly Func<TCard?, bool> _isCardEligible;

	private readonly Action<string, int> _warn;

	public NativePlayedCardVisibilityRuntime(string metaKeyPrefix, INativePlayedCardVisibilityHost<TCard> host)
	{
		ArgumentNullException.ThrowIfNull(host, "host");
		_playContainerModulateMeta = (string.IsNullOrWhiteSpace(metaKeyPrefix) ? "character_skin" : metaKeyPrefix.Trim()) + "_native_play_container_modulate";
		_isCardEligible = host.IsCardEligible;
		_warn = host.Warn;
	}

	public void Hide(TCard? card)
	{
		if (EnableNativePlayedCardHiding && card != null && _isCardEligible(card))
		{
			HidePlayContainer();
		}
	}

	public void RestorePlayContainer()
	{
		try
		{
			Control control = FindPlayContainer();
			if (control != null && control.HasMeta(_playContainerModulateMeta))
			{
				control.Modulate = control.GetMeta(_playContainerModulateMeta).AsColor();
				control.RemoveMeta(_playContainerModulateMeta);
			}
		}
		catch (Exception ex)
		{
			_warn("Native played card container restore failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private void HidePlayContainer()
	{
		try
		{
			Control control = FindPlayContainer();
			if (control != null)
			{
				if (!control.HasMeta(_playContainerModulateMeta))
				{
					control.SetMeta(_playContainerModulateMeta, Variant.From<Color>(control.Modulate));
				}
				control.Modulate = Colors.Transparent;
			}
		}
		catch (Exception ex)
		{
			_warn("Native played card container hide failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	private static Control? FindPlayContainer()
	{
		Control control = NCombatRoom.Instance?.Ui?.PlayContainer;
		if (control == null || !GodotObject.IsInstanceValid(control))
		{
			return null;
		}
		return control;
	}
}
