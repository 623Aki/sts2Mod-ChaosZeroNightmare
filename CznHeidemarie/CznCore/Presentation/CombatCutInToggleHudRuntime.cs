using System;
using System.Collections.Generic;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace CznCore.Presentation;

internal sealed class CombatCutInToggleHudRuntime
{
	private readonly CombatCutInToggleHudProfile _profile;

	private readonly ICombatCutInToggleHudHost _host;

	private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _warnedMissingTexturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private Button? _button;

	private TextureRect? _icon;

	private NCombatUi? _combatUi;

	private bool _isHovering;

	private bool _uiEnabled = true;

	public CombatCutInToggleHudRuntime(CombatCutInToggleHudProfile profile, ICombatCutInToggleHudHost host)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		_host = host ?? throw new ArgumentNullException("host");
	}

	public void EnsureAttached(NCombatUi? ui)
	{
		try
		{
			if (!_host.IsAvailable() || !GodotNodeUtil.IsAlive(ui))
			{
				Cleanup();
				return;
			}
			_combatUi = ui;
			Button button = ResolveOrCreateButton(ui);
			Refresh(button);
		}
		catch (Exception ex)
		{
			_host.Warn("Combat cut-in toggle HUD failed: " + SkinLog.FormatException(ex), 2);
		}
	}

	public void SetCombatUiEnabled(bool enabled, NCombatUi? ui)
	{
		_uiEnabled = enabled;
		if (enabled)
		{
			EnsureAttached(ui);
		}
		else
		{
			RefreshExistingButton();
		}
	}

	public void Cleanup()
	{
		try
		{
			if (GodotNodeUtil.IsAlive(_button))
			{
				_button.Visible = false;
				_button.QueueFree();
			}
		}
		catch (Exception ex)
		{
			_host.Warn("Combat cut-in toggle HUD cleanup failed: " + SkinLog.FormatException(ex), 2);
		}
		finally
		{
			HideHoverTip();
			_button = null;
			_icon = null;
			_combatUi = null;
			_isHovering = false;
			_uiEnabled = true;
		}
	}

	private Button ResolveOrCreateButton(NCombatUi ui)
	{
		Control control = ResolveButtonParent(ui);
		if (GodotNodeUtil.IsAlive(_button) && _button.GetParent() == control)
		{
			EnsureIcon(_button);
			return _button;
		}
		if (GodotNodeUtil.IsAlive(_button))
		{
			_button.QueueFree();
		}
		_button = null;
		_icon = null;
		string text = _host.RuntimeNodeName(_profile.ButtonNodeNameSuffix);
		Node nodeOrNull = control.GetNodeOrNull(text);
		if (nodeOrNull is Button button)
		{
			_button = button;
			EnsureIcon(button);
			return button;
		}
		if (GodotNodeUtil.IsAlive(nodeOrNull))
		{
			if (nodeOrNull is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
			}
			nodeOrNull.QueueFree();
		}
		Button button2 = CreateButton(text);
		control.AddChild(button2, forceReadableName: false, Node.InternalMode.Disabled);
		_button = button2;
		EnsureIcon(button2);
		return button2;
	}

	private Control ResolveButtonParent(NCombatUi ui)
	{
		if (!_profile.UsePileRelativePlacement || !GodotNodeUtil.IsAlive(ui.DiscardPile))
		{
			return ui;
		}
		return ui.DiscardPile;
	}

	private Button CreateButton(string nodeName)
	{
		float num = Math.Max(1f, _profile.Size);
		Button button = new Button();
		button.Name = nodeName;
		button.Text = string.Empty;
		button.CustomMinimumSize = new Vector2(num, num);
		button.MouseFilter = Control.MouseFilterEnum.Stop;
		button.FocusMode = Control.FocusModeEnum.None;
		button.ZIndex = 1000;
		button.AnchorLeft = 1f;
		button.AnchorTop = 1f;
		button.AnchorRight = 1f;
		button.AnchorBottom = 1f;
		button.OffsetRight = _profile.OffsetRight;
		button.OffsetBottom = _profile.OffsetBottom;
		button.OffsetLeft = _profile.OffsetRight - num;
		button.OffsetTop = _profile.OffsetBottom - num;
		button.Pressed += OnPressed;
		button.MouseEntered += OnMouseEntered;
		button.MouseExited += OnMouseExited;
		ApplyButtonStyle(button);
		return button;
	}

	private void EnsureIcon(Button button)
	{
		if (!GodotNodeUtil.IsAlive(_icon) || _icon.GetParent() != button)
		{
			string text = _host.RuntimeNodeName(_profile.IconNodeNameSuffix);
			_icon = button.GetNodeOrNull<TextureRect>(text);
			if (!GodotNodeUtil.IsAlive(_icon))
			{
				_icon = new TextureRect
				{
					Name = text,
					MouseFilter = Control.MouseFilterEnum.Ignore,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
				};
				_icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				_icon.OffsetLeft = 0f;
				_icon.OffsetTop = 0f;
				_icon.OffsetRight = 0f;
				_icon.OffsetBottom = 0f;
				button.AddChild(_icon, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
	}

	private void Refresh(Button button)
	{
		bool enabled = _host.IsCutInEnabled();
		Texture2D texture = GetTexture(enabled);
		NCombatUi nCombatUi = (GodotNodeUtil.IsAlive(_combatUi) ? _combatUi : null);
		if (nCombatUi != null && _profile.UsePileRelativePlacement)
		{
			PlaceRelativeToDiscardPile(button, nCombatUi);
		}
		button.Visible = texture != null && _uiEnabled && nCombatUi != null && _host.ShouldShow(nCombatUi);
		button.Disabled = !button.Visible;
		if (!button.Visible)
		{
			HideHoverTip(button);
		}
		else if (_isHovering)
		{
			ShowHoverTip(button);
		}
		button.TooltipText = string.Empty;
		if (GodotNodeUtil.IsAlive(_icon))
		{
			_icon.Texture = texture;
		}
	}

	private void RefreshExistingButton()
	{
		if (GodotNodeUtil.IsAlive(_button))
		{
			Refresh(_button);
		}
	}

	private void OnPressed()
	{
		bool flag = !_host.IsCutInEnabled();
		_host.SetCutInEnabled(flag, persist: true);
		if (GodotNodeUtil.IsAlive(_button))
		{
			Refresh(_button);
			if (_isHovering)
			{
				ShowHoverTip(_button);
			}
		}
		_host.Debug("Combat cut-in toggle set to " + (flag ? "on" : "off"), 2);
	}

	private void OnMouseEntered()
	{
		_isHovering = true;
		if (GodotNodeUtil.IsAlive(_button) && _button.Visible)
		{
			ShowHoverTip(_button);
		}
	}

	private void OnMouseExited()
	{
		_isHovering = false;
		if (GodotNodeUtil.IsAlive(_button))
		{
			HideHoverTip(_button);
		}
	}

	private void ShowHoverTip(Button button)
	{
		if (GodotNodeUtil.IsAlive(button))
		{
			HideHoverTip(button);
			HoverTip? hoverTip = CreateHoverTip(_host.IsCutInEnabled());
			if (hoverTip.HasValue)
			{
				NHoverTipSet hoverTipSet = NHoverTipSet.CreateAndShow(button, hoverTip.Value);
				PlaceHoverTip(hoverTipSet, button);
			}
		}
	}

	private void HideHoverTip(Button? button = null)
	{
		Button button2 = button ?? _button;
		if (GodotNodeUtil.IsAlive(button2))
		{
			NHoverTipSet.Remove(button2);
		}
	}

	private Texture2D? GetTexture(bool enabled)
	{
		string text = (enabled ? _profile.EnabledTexturePath : _profile.DisabledTexturePath);
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		if (_textureCache.TryGetValue(text, out Texture2D value) && GodotObject.IsInstanceValid(value))
		{
			return value;
		}
		Texture2D texture2D = LoadTexture2D(text);
		if (texture2D != null && GodotObject.IsInstanceValid(texture2D))
		{
			_textureCache[text] = texture2D;
			return texture2D;
		}
		if (_warnedMissingTexturePaths.Add(text))
		{
			_host.Warn("Combat cut-in toggle texture missing: " + text, 2);
		}
		return null;
	}

	private void PlaceRelativeToDiscardPile(Button button, NCombatUi ui)
	{
		try
		{
			if (GodotNodeUtil.IsAlive(ui.DiscardPile))
			{
				float num = Math.Max(1f, _profile.Size);
				if (button.GetParent() == ui.DiscardPile)
				{
					button.AnchorLeft = 0.5f;
					button.AnchorRight = 0.5f;
					button.AnchorTop = 0f;
					button.AnchorBottom = 0f;
					button.OffsetLeft = num * -0.5f;
					button.OffsetRight = num * 0.5f;
					button.OffsetTop = 0f - num - 8f;
					button.OffsetBottom = -8f;
				}
				else
				{
					Vector2 vector = ToLocal(button, ui.DiscardPile.GlobalPosition + ui.DiscardPile.Size * 0.5f);
					button.AnchorLeft = 0f;
					button.AnchorTop = 0f;
					button.AnchorRight = 0f;
					button.AnchorBottom = 0f;
					button.OffsetLeft = vector.X - num * 0.5f;
					button.OffsetTop = vector.Y - ui.DiscardPile.Size.Y * 0.5f - num - 8f;
					button.OffsetRight = button.OffsetLeft + num;
					button.OffsetBottom = button.OffsetTop + num;
				}
			}
		}
		catch (Exception ex)
		{
			_host.Debug("Combat cut-in toggle discard-relative placement failed: " + SkinLog.FormatException(ex), 3);
		}
	}

	private static Vector2 ToLocal(Control localSpace, Vector2 globalPosition)
	{
		if (!(localSpace.GetParent() is Control control))
		{
			return globalPosition;
		}
		return control.GetGlobalTransform().AffineInverse() * globalPosition;
	}

	private HoverTip? CreateHoverTip(bool enabled)
	{
		string text = (enabled ? _profile.EnabledHoverTipTitleKey : _profile.DisabledHoverTipTitleKey);
		string text2 = (enabled ? _profile.EnabledHoverTipDescriptionKey : _profile.DisabledHoverTipDescriptionKey);
		if (string.IsNullOrWhiteSpace(_profile.HoverTipLocTable) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			return null;
		}
		try
		{
			LocString title = new LocString(_profile.HoverTipLocTable, text);
			LocString description = new LocString(_profile.HoverTipLocTable, text2);
			return new HoverTip(title, description);
		}
		catch (Exception ex)
		{
			_host.Warn("Combat cut-in toggle hover tip localization failed: " + SkinLog.FormatException(ex), 2);
			return null;
		}
	}

	private void PlaceHoverTip(NHoverTipSet? hoverTipSet, Button button)
	{
		if (hoverTipSet == null)
		{
			return;
		}
		try
		{
			if (_profile.UseDiscardPileHoverTipPlacement && GodotNodeUtil.IsAlive(_combatUi))
			{
				NCombatUi combatUi = _combatUi;
				if (combatUi != null && GodotNodeUtil.IsAlive(combatUi.DiscardPile))
				{
					hoverTipSet.GlobalPosition = combatUi.DiscardPile.GlobalPosition + new Vector2(_profile.HoverTipDiscardPileOffsetX, _profile.HoverTipDiscardPileOffsetY);
					return;
				}
			}
			hoverTipSet.SetAlignment(button, HoverTipAlignment.Left);
		}
		catch (Exception ex)
		{
			_host.Debug("Combat cut-in toggle hover tip placement failed: " + SkinLog.FormatException(ex), 3);
		}
	}

	private static Texture2D? LoadTexture2D(string texturePath)
	{
		if (IsRawImagePath(texturePath))
		{
			Texture2D texture2D = LoadImageTexture2D(texturePath);
			if (texture2D != null && GodotObject.IsInstanceValid(texture2D))
			{
				return texture2D;
			}
		}
		try
		{
			return ResourceLoader.Load<Texture2D>(texturePath, null, ResourceLoader.CacheMode.Reuse);
		}
		catch
		{
		}
		if (!IsRawImagePath(texturePath))
		{
			return LoadImageTexture2D(texturePath);
		}
		return null;
	}

	private static bool IsRawImagePath(string texturePath)
	{
		if (!texturePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !texturePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !texturePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			return texturePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static Texture2D? LoadImageTexture2D(string texturePath)
	{
		try
		{
			if (!global::Godot.FileAccess.FileExists(texturePath))
			{
				return null;
			}
			Image image = Image.LoadFromFile(texturePath);
			if (image == null || image.IsEmpty())
			{
				return null;
			}
			return ImageTexture.CreateFromImage(image);
		}
		catch
		{
			return null;
		}
	}

	private static void ApplyButtonStyle(Button button)
	{
		StyleBoxEmpty stylebox = new StyleBoxEmpty();
		button.AddThemeStyleboxOverride("normal", stylebox);
		button.AddThemeStyleboxOverride("hover", stylebox);
		button.AddThemeStyleboxOverride("pressed", stylebox);
		button.AddThemeStyleboxOverride("focus", stylebox);
		button.AddThemeStyleboxOverride("disabled", stylebox);
		button.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0f));
		button.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f, 0f));
		button.AddThemeColorOverride("font_pressed_color", new Color(1f, 1f, 1f, 0f));
		button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0f));
	}
}
