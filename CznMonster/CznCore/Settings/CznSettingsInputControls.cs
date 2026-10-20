using System;
using Godot;

namespace CznCore.Settings;

internal static class CznSettingsInputControls
{
	private const string NativeOwnerName = "CznSettingsNativeInputOwner";

	internal static Button AttachNativeOwner(Button button)
	{
		AttachNativeOwner(button, () =>
		{
			if (!button.Disabled)
			{
				if (button.ToggleMode)
				{
					button.ButtonPressed = !button.ButtonPressed;
				}
				button.EmitSignal(BaseButton.SignalName.Pressed);
			}
		});
		return button;
	}

	internal static CheckButton AttachNativeOwner(CheckButton button)
	{
		AttachNativeOwner(button, () =>
		{
			if (!button.Disabled)
			{
				button.ButtonPressed = !button.ButtonPressed;
				button.EmitSignal(BaseButton.SignalName.Pressed);
			}
		});
		return button;
	}

	internal static OptionButton AttachNativeOwner(OptionButton dropdown)
	{
		AttachNativeOwner(dropdown, () =>
		{
			MoveSelection(dropdown, 1);
		}, (int direction) =>
		{
			MoveSelection(dropdown, direction);
		});
		return dropdown;
	}

	internal static Control ResolveFocusOwner(Control visualControl)
	{
		return visualControl.GetNodeOrNull<CznSettingsNativeInputOwner>(new NodePath("CznSettingsNativeInputOwner")) ?? visualControl;
	}

	internal static Control ResolveVisualControl(Control focusControl)
	{
		if (!(focusControl is CznSettingsNativeInputOwner cznSettingsNativeInputOwner))
		{
			return focusControl;
		}
		return cznSettingsNativeInputOwner.VisualControl;
	}

	private static void AttachNativeOwner(BaseButton visualControl, Action onReleased, Action<int>? onDirection = null)
	{
		if (visualControl.GetNodeOrNull<CznSettingsNativeInputOwner>(new NodePath("CznSettingsNativeInputOwner")) == null)
		{
			Control.FocusModeEnum focusMode = visualControl.FocusMode;
			visualControl.FocusMode = Control.FocusModeEnum.None;
			CznSettingsNativeInputOwner cznSettingsNativeInputOwner = new CznSettingsNativeInputOwner
			{
				Name = "CznSettingsNativeInputOwner",
				FocusMode = focusMode,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			cznSettingsNativeInputOwner.Bind(visualControl, onReleased, onDirection);
			visualControl.AddChild(cznSettingsNativeInputOwner, forceReadableName: false, Node.InternalMode.Disabled);
			cznSettingsNativeInputOwner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		}
	}

	private static void MoveSelection(OptionButton dropdown, int direction)
	{
		if (dropdown.Disabled || dropdown.ItemCount <= 0)
		{
			return;
		}
		int num = ((dropdown.Selected >= 0) ? dropdown.Selected : 0);
		for (int i = 1; i <= dropdown.ItemCount; i++)
		{
			int num2 = (num + direction * i) % dropdown.ItemCount;
			if (num2 < 0)
			{
				num2 += dropdown.ItemCount;
			}
			if (!dropdown.IsItemDisabled(num2))
			{
				if (num2 != dropdown.Selected)
				{
					dropdown.Select(num2);
					dropdown.EmitSignal(OptionButton.SignalName.ItemSelected, num2);
				}
				break;
			}
		}
	}
}
