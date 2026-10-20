using System;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace CznCore.Settings;

internal partial class CznSettingsNativeInputOwner : NButton
{
	private Panel _focusRing;

	private BaseButton? _visualControl;

	private Action? _onReleased;

	private Action<int>? _onDirection;

	private bool _lastDisabled;

	internal Control VisualControl
	{
		get
		{
			if (_visualControl == null)
			{
				return this;
			}
			return _visualControl;
		}
	}

	internal void Bind(BaseButton visualControl, Action onReleased, Action<int>? onDirection)
	{
		_visualControl = visualControl;
		_onReleased = onReleased;
		_onDirection = onDirection;
		_lastDisabled = visualControl.Disabled;
		_focusRing = new Panel
		{
			Name = "ControllerFocusRing",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_focusRing.AddThemeStyleboxOverride("panel", CreateFocusRingStyle());
		AddChild(_focusRing, forceReadableName: false, InternalMode.Disabled);
		_focusRing.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
	}

	public override void _Ready()
	{
		ConnectSignals();
		SyncEnabledState(force: true);
	}

	public override void _Process(double delta)
	{
		SyncEnabledState(force: false);
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (_visualControl == null || _visualControl.Disabled)
		{
			return;
		}
		if (_onDirection != null)
		{
			int num = 0;
			if (inputEvent.IsActionPressed(MegaInput.left))
			{
				num = -1;
			}
			else if (inputEvent.IsActionPressed(MegaInput.right))
			{
				num = 1;
			}
			if (num != 0)
			{
				_onDirection(num);
				AcceptEvent();
				return;
			}
		}
		base._GuiInput(inputEvent);
	}

	protected override void OnFocus()
	{
		base.OnFocus();
		_focusRing.Visible = true;
	}

	protected override void OnUnfocus()
	{
		base.OnUnfocus();
		_focusRing.Visible = false;
	}

	protected override void OnRelease()
	{
		base.OnRelease();
		_onReleased?.Invoke();
	}

	private void SyncEnabledState(bool force)
	{
		if (_visualControl == null)
		{
			return;
		}
		bool disabled = _visualControl.Disabled;
		if (force || disabled != _lastDisabled)
		{
			_lastDisabled = disabled;
			SetEnabled(!disabled);
			if (disabled)
			{
				_focusRing.Visible = false;
			}
		}
	}

	private static StyleBoxFlat CreateFocusRingStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = new Color(0.95f, 0.83f, 0.52f, 0.95f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		};
	}
}
