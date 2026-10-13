using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;

namespace CznCore.Presentation;

internal sealed partial class CharacterSelectSettingsMenu : Control
{
	private sealed class ItemBinding
	{
		public CharacterSelectMenuItemDescriptor Descriptor { get; }

		public BaseButton Button { get; }

		public Label Title { get; }

		public Label? State { get; }

		public Panel? ToggleTrack { get; }

		public Panel? ToggleKnob { get; }

		public ItemBinding(CharacterSelectMenuItemDescriptor descriptor, BaseButton button, Label title, Label? state = null, Panel? toggleTrack = null, Panel? toggleKnob = null)
		{
			Descriptor = descriptor;
			Button = button;
			Title = title;
			State = state;
			ToggleTrack = toggleTrack;
			ToggleKnob = toggleKnob;
		}
	}

	internal const string SharedNodeName = "CznCharacterSelectMenu";

	internal const string OwnerMeta = "czn_character_select_menu_owner";

	internal const string OpenStateMeta = "czn_character_select_menu_open";

	internal const string NavigationOwnerMeta = "czn_character_select_menu_navigation_owner";

	internal const string NativeLeftNeighborMeta = "czn_character_select_menu_native_left_neighbor";

	private const string CharacterListPath = "CharSelectButtons/ButtonContainer";

	private const float HandleSize = 64f;

	private const float DrawerWidth = 392f;

	private const float DrawerHeight = 438f;

	private const float MenuWidth = 456f;

	private const int ContentPadding = 18;

	private const int SectionGap = 12;

	private const int CardGap = 8;

	private const int ActionCardHeight = 68;

	private const int ToggleCardHeight = 52;

	private const float ToggleTrackWidth = 48f;

	private const float ToggleTrackHeight = 26f;

	private const float ToggleKnobSize = 20f;

	private const float ToggleInset = 3f;

	private static readonly Color Accent = new Color(1f, 0.61f, 0f);

	private static readonly Color Text = new Color(0.12f, 0.13f, 0.14f);

	private static readonly Color Muted = new Color(0.38f, 0.4f, 0.42f);

	private static readonly Color Surface = new Color(0.982f, 0.982f, 0.972f, 0.99f);

	private static readonly Color SurfaceRaised = new Color(1f, 1f, 0.995f);

	private static readonly Color Border = new Color(0.15f, 0.16f, 0.17f, 0.16f);

	private static readonly Color Dark = new Color(0.105f, 0.112f, 0.12f, 0.98f);

	private static readonly Color ToggleOff = new Color(0.57f, 0.59f, 0.61f);

	private static readonly Color ToggleKnob = new Color(1f, 1f, 1f);

	private readonly List<ItemBinding> _itemBindings = new List<ItemBinding>();

	private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

	private IReadOnlyList<CharacterSelectMenuSectionDescriptor> _sections = Array.Empty<CharacterSelectMenuSectionDescriptor>();

	private Control? _screen;

	private PanelContainer? _drawerPanel;

	private Label? _titleLabel;

	private Button? _handle;

	private Label? _handleLabel;

	private Control? _nativeCharacterButton;

	private NodePath _nativeLeftNeighbor = new NodePath(string.Empty);

	private string _ownerId = string.Empty;

	private string _title = string.Empty;

	private bool _drawerOpen;

	private bool _externalNavigationCaptured;

	internal static CharacterSelectSettingsMenu Attach(Control screen, string ownerId, string title, IReadOnlyList<CharacterSelectMenuSectionDescriptor> sections)
	{
		Control nodeOrNull = screen.GetNodeOrNull<Control>("CznCharacterSelectMenu");
		if (nodeOrNull is CharacterSelectSettingsMenu characterSelectSettingsMenu && string.Equals(ReadOwner(nodeOrNull), ownerId, StringComparison.Ordinal))
		{
			characterSelectSettingsMenu.UpdateContent(title, sections);
			return characterSelectSettingsMenu;
		}
		if (nodeOrNull != null)
		{
			nodeOrNull.GetParent()?.RemoveChild(nodeOrNull);
			nodeOrNull.QueueFree();
		}
		CharacterSelectSettingsMenu characterSelectSettingsMenu2 = new CharacterSelectSettingsMenu
		{
			Name = "CznCharacterSelectMenu"
		};
		characterSelectSettingsMenu2.SetMeta("czn_character_select_menu_owner", Variant.From(in ownerId));
		screen.AddChild(characterSelectSettingsMenu2, forceReadableName: false, InternalMode.Disabled);
		characterSelectSettingsMenu2.Activate(screen, ownerId, title, sections);
		return characterSelectSettingsMenu2;
	}

	internal static void RemoveOwned(Control screen, string ownerId)
	{
		Control nodeOrNull = screen.GetNodeOrNull<Control>("CznCharacterSelectMenu");
		if (nodeOrNull != null && string.Equals(ReadOwner(nodeOrNull), ownerId, StringComparison.Ordinal))
		{
			nodeOrNull.GetParent()?.RemoveChild(nodeOrNull);
			nodeOrNull.QueueFree();
		}
	}

	public override void _ExitTree()
	{
		RestoreExternalNavigation();
		_screen = null;
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (_drawerOpen && IsVisibleInTree() && !inputEvent.IsEcho() && inputEvent.IsActionPressed(MegaInput.cancel))
		{
			SetDrawerOpen(open: false, focus: true);
			GetViewport().SetInputAsHandled();
		}
	}

	private void Activate(Control screen, string ownerId, string title, IReadOnlyList<CharacterSelectMenuSectionDescriptor> sections)
	{
		_screen = screen;
		_ownerId = ownerId;
		_title = title;
		_sections = sections;
		MouseFilter = MouseFilterEnum.Ignore;
		ZIndex = 120;
		SetAnchorsPreset(LayoutPreset.CenterLeft);
		OffsetLeft = 0f;
		OffsetRight = 456f;
		OffsetTop = -219f;
		OffsetBottom = 219f;
		Build();
		CaptureExternalNavigation();
		SetDrawerOpen(ReadOpenState(screen), focus: false);
		SkinLog.Info("Character-select options menu attached: owner=" + ownerId + " node=" + GetPath());
	}

	private void UpdateContent(string title, IReadOnlyList<CharacterSelectMenuSectionDescriptor> sections)
	{
		_title = title;
		_sections = sections;
		RebuildDrawerContent();
		ConfigureNavigation();
	}

	private void Build()
	{
		_drawerPanel = new PanelContainer
		{
			Name = "Drawer",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
			OffsetLeft = 64f,
			OffsetRight = 456f,
			OffsetTop = 0f,
			OffsetBottom = 438f
		};
		_drawerPanel.AddThemeStyleboxOverride("panel", CreateFlatStyle(Surface, Border, 1, 14, 12));
		AddChild(_drawerPanel, forceReadableName: false, InternalMode.Disabled);
		RebuildDrawerContent();
		_handle = new Button
		{
			Name = "Handle",
			TooltipText = "CZN Options",
			MouseFilter = MouseFilterEnum.Stop,
			FocusMode = FocusModeEnum.All,
			OffsetLeft = 0f,
			OffsetRight = 64f,
			OffsetTop = 187f,
			OffsetBottom = 251f,
			Text = string.Empty
		};
		_handleLabel = CreateLabel("≡", 28, Accent);
		_handleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_handleLabel.VerticalAlignment = VerticalAlignment.Center;
		_handleLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		_handle.AddChild(_handleLabel, forceReadableName: false, InternalMode.Disabled);
		_handle.Pressed += () =>
		{
			SetDrawerOpen(!_drawerOpen, focus: true);
		};
		AddChild(_handle, forceReadableName: false, InternalMode.Disabled);
	}

	private void RebuildDrawerContent()
	{
		if (_drawerPanel == null)
		{
			return;
		}
		foreach (Node child in _drawerPanel.GetChildren())
		{
			_drawerPanel.RemoveChild(child);
			child.QueueFree();
		}
		_itemBindings.Clear();
		_drawerPanel.AddChild(CreateContent(), forceReadableName: false, InternalMode.Disabled);
	}

	private MarginContainer CreateContent()
	{
		MarginContainer marginContainer = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		marginContainer.AddThemeConstantOverride("margin_left", 18);
		marginContainer.AddThemeConstantOverride("margin_top", 18);
		marginContainer.AddThemeConstantOverride("margin_right", 18);
		marginContainer.AddThemeConstantOverride("margin_bottom", 18);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		vBoxContainer.AddThemeConstantOverride("separation", 12);
		marginContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		vBoxContainer.AddChild(CreateHeader(), forceReadableName: false, InternalMode.Disabled);
		ScrollContainer scrollContainer = new ScrollContainer
		{
			Name = "SectionsScroll",
			MouseFilter = MouseFilterEnum.Pass,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		vBoxContainer.AddChild(scrollContainer, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer2 = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer2.AddThemeConstantOverride("separation", 12);
		scrollContainer.AddChild(vBoxContainer2, forceReadableName: false, InternalMode.Disabled);
		foreach (CharacterSelectMenuSectionDescriptor section in _sections)
		{
			vBoxContainer2.AddChild(CreateSection(section), forceReadableName: false, InternalMode.Disabled);
		}
		return marginContainer;
	}

	private PanelContainer CreateHeader()
	{
		PanelContainer panelContainer = new PanelContainer();
		panelContainer.CustomMinimumSize = new Vector2(0f, 82f);
		panelContainer.MouseFilter = MouseFilterEnum.Ignore;
		panelContainer.AddThemeStyleboxOverride("panel", CreateFlatStyle(SurfaceRaised, Border, 1, 10));
		MarginContainer marginContainer = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		marginContainer.AddThemeConstantOverride("margin_left", 14);
		marginContainer.AddThemeConstantOverride("margin_top", 10);
		marginContainer.AddThemeConstantOverride("margin_right", 14);
		marginContainer.AddThemeConstantOverride("margin_bottom", 10);
		panelContainer.AddChild(marginContainer, forceReadableName: false, InternalMode.Disabled);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		hBoxContainer.AddThemeConstantOverride("separation", 12);
		marginContainer.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(new ColorRect
		{
			Color = Accent,
			CustomMinimumSize = new Vector2(4f, 56f),
			MouseFilter = MouseFilterEnum.Ignore
		}, forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		vBoxContainer.AddThemeConstantOverride("separation", 0);
		Label label = CreateLabel("CZN OPTION / CHARACTER SELECT", 12, Muted);
		label.VerticalAlignment = VerticalAlignment.Bottom;
		label.CustomMinimumSize = new Vector2(0f, 22f);
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		_titleLabel = CreateLabel(_title, 24, Text);
		_titleLabel.ClipText = true;
		_titleLabel.VerticalAlignment = VerticalAlignment.Top;
		_titleLabel.CustomMinimumSize = new Vector2(0f, 36f);
		vBoxContainer.AddChild(_titleLabel, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		return panelContainer;
	}

	private VBoxContainer CreateSection(CharacterSelectMenuSectionDescriptor section)
	{
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		vBoxContainer.AddThemeConstantOverride("separation", 8);
		Label label = CreateLabel(Resolve(section.Title, section.Id), 14, Muted);
		label.Name = "Section_" + section.Id;
		label.Uppercase = true;
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		foreach (CharacterSelectMenuItemDescriptor item in section.Items)
		{
			vBoxContainer.AddChild(CreateMenuItem(item), forceReadableName: false, InternalMode.Disabled);
		}
		return vBoxContainer;
	}

	private BaseButton CreateMenuItem(CharacterSelectMenuItemDescriptor descriptor)
	{
		if (!(descriptor is CharacterSelectMenuActionDescriptor descriptor2))
		{
			if (descriptor is CharacterSelectMenuToggleDescriptor descriptor3)
			{
				return CreateToggleItem(descriptor3);
			}
			throw new NotSupportedException("Unsupported character-select menu item: " + descriptor.GetType().Name);
		}
		return CreateActionItem(descriptor2);
	}

	private Button CreateActionItem(CharacterSelectMenuActionDescriptor descriptor)
	{
		Button button = CreateItemButton("Action_" + descriptor.Id, 68, toggleMode: false, pressed: false);
		ApplyActionStyle(button);
		HBoxContainer hBoxContainer = CreateItemRow(7f);
		hBoxContainer.AddChild(CreateActionIcon(descriptor.IconPath), forceReadableName: false, InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		vBoxContainer.AddThemeConstantOverride("separation", 1);
		Label label = CreateLabel(string.Empty, 17, Text);
		label.Name = "Title";
		label.ClipText = true;
		vBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Label label2 = CreateLabel(string.Empty, 14, descriptor.Accent ? Accent : Muted);
		label2.Name = "State";
		label2.ClipText = true;
		vBoxContainer.AddChild(label2, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, InternalMode.Disabled);
		Label label3 = CreateLabel("›", 27, Accent);
		label3.VerticalAlignment = VerticalAlignment.Center;
		label3.HorizontalAlignment = HorizontalAlignment.Center;
		label3.CustomMinimumSize = new Vector2(20f, 0f);
		hBoxContainer.AddChild(label3, forceReadableName: false, InternalMode.Disabled);
		button.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		ItemBinding binding = new ItemBinding(descriptor, button, label, label2);
		_itemBindings.Add(binding);
		button.Pressed += () =>
		{
			ActivateAction(binding, descriptor);
		};
		RefreshItem(binding);
		return button;
	}

	private Button CreateToggleItem(CharacterSelectMenuToggleDescriptor descriptor)
	{
		bool pressed = Resolve(descriptor.Value, fallback: true);
		Button button = CreateItemButton("Toggle_" + descriptor.Id, 52, toggleMode: true, pressed);
		ApplyToggleStyle(button);
		HBoxContainer hBoxContainer = CreateItemRow(6f);
		Label label = CreateLabel(string.Empty, 16, Text);
		label.Name = "Title";
		label.ClipText = true;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hBoxContainer.AddChild(label, forceReadableName: false, InternalMode.Disabled);
		Panel panel = new Panel
		{
			Name = "SwitchTrack",
			CustomMinimumSize = new Vector2(48f, 26f),
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		Panel panel2 = new Panel
		{
			Name = "SwitchKnob",
			MouseFilter = MouseFilterEnum.Ignore
		};
		panel.AddChild(panel2, forceReadableName: false, InternalMode.Disabled);
		hBoxContainer.AddChild(panel, forceReadableName: false, InternalMode.Disabled);
		button.AddChild(hBoxContainer, forceReadableName: false, InternalMode.Disabled);
		ItemBinding binding = new ItemBinding(descriptor, button, label, null, panel, panel2);
		_itemBindings.Add(binding);
		button.Toggled += (bool value) =>
		{
			ActivateToggle(binding, descriptor, value);
		};
		RefreshItem(binding);
		return button;
	}

	private static Button CreateItemButton(string name, int height, bool toggleMode, bool pressed)
	{
		return new Button
		{
			Name = name,
			ButtonPressed = pressed,
			CustomMinimumSize = new Vector2(0f, height),
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Text = string.Empty,
			ToggleMode = toggleMode
		};
	}

	private static HBoxContainer CreateItemRow(float verticalPadding)
	{
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.MouseFilter = MouseFilterEnum.Ignore;
		hBoxContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize);
		hBoxContainer.OffsetLeft = 14f;
		hBoxContainer.OffsetTop = verticalPadding;
		hBoxContainer.OffsetRight = -14f;
		hBoxContainer.OffsetBottom = 0f - verticalPadding;
		hBoxContainer.AddThemeConstantOverride("separation", 12);
		return hBoxContainer;
	}

	private TextureRect CreateActionIcon(string iconPath)
	{
		return new TextureRect
		{
			Name = "Icon",
			Texture = LoadTexture(iconPath),
			CustomMinimumSize = new Vector2(30f, 30f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = Muted,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
	}

	private void ActivateAction(ItemBinding binding, CharacterSelectMenuActionDescriptor descriptor)
	{
		try
		{
			descriptor.Activate();
			RefreshItemLabels();
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Character-select option failed: owner=" + _ownerId + " action=" + descriptor.Id + " " + SkinLog.FormatException(ex));
			RefreshItem(binding);
		}
	}

	private void ActivateToggle(ItemBinding binding, CharacterSelectMenuToggleDescriptor descriptor, bool enabled)
	{
		try
		{
			descriptor.SetValue(enabled);
			RefreshItemLabels();
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Character-select toggle failed: owner=" + _ownerId + " action=" + descriptor.Id + " " + SkinLog.FormatException(ex));
			RefreshItem(binding);
		}
	}

	private void RefreshItemLabels()
	{
		if (_titleLabel != null)
		{
			_titleLabel.Text = _title;
		}
		for (int i = 0; i < _itemBindings.Count; i++)
		{
			RefreshItem(_itemBindings[i]);
		}
	}

	private static void RefreshItem(ItemBinding binding)
	{
		CharacterSelectMenuItemDescriptor descriptor = binding.Descriptor;
		binding.Title.Text = Resolve(descriptor.Title, descriptor.Id);
		if (!(descriptor is CharacterSelectMenuActionDescriptor characterSelectMenuActionDescriptor))
		{
			if (descriptor is CharacterSelectMenuToggleDescriptor characterSelectMenuToggleDescriptor)
			{
				bool flag = Resolve(characterSelectMenuToggleDescriptor.Value, binding.Button.ButtonPressed);
				binding.Button.SetPressedNoSignal(flag);
				UpdateToggleVisual(binding, flag);
			}
		}
		else if (binding.State != null)
		{
			binding.State.Text = Resolve(characterSelectMenuActionDescriptor.State, string.Empty);
		}
		binding.Button.TooltipText = Resolve(descriptor.Tooltip, binding.Title.Text);
	}

	private void SetDrawerOpen(bool open, bool focus)
	{
		_drawerOpen = open;
		if (_drawerPanel != null)
		{
			_drawerPanel.Visible = open;
		}
		if (_handle != null)
		{
			_handle.TooltipText = (open ? "Close CZN Options" : "CZN Options");
			ApplyHandleStyle(_handle, open);
		}
		if (_handleLabel != null)
		{
			_handleLabel.Text = (open ? "×" : "≡");
			_handleLabel.AddThemeColorOverride("font_color", open ? Dark : Accent);
		}
		if (GodotObject.IsInstanceValid(_screen))
		{
			_screen.SetMeta("czn_character_select_menu_open", Variant.From(in open));
		}
		ConfigureNavigation();
		if (focus && IsVisibleInTree())
		{
			if (open && _itemBindings.Count > 0)
			{
				_itemBindings[0].Button.GrabFocus();
			}
			else
			{
				_handle?.GrabFocus();
			}
		}
	}

	private void CaptureExternalNavigation()
	{
		if (_externalNavigationCaptured || !GodotObject.IsInstanceValid(_screen))
		{
			return;
		}
		Node nodeOrNull = _screen.GetNodeOrNull<Node>("CharSelectButtons/ButtonContainer");
		_nativeCharacterButton = FindFirstCharacterButton(nodeOrNull);
		if (_nativeCharacterButton != null)
		{
			if (_nativeCharacterButton.HasMeta("czn_character_select_menu_native_left_neighbor"))
			{
				_nativeLeftNeighbor = _nativeCharacterButton.GetMeta("czn_character_select_menu_native_left_neighbor").AsNodePath();
			}
			else
			{
				_nativeLeftNeighbor = _nativeCharacterButton.FocusNeighborLeft;
				_nativeCharacterButton.SetMeta("czn_character_select_menu_native_left_neighbor", Variant.From(in _nativeLeftNeighbor));
			}
			_nativeCharacterButton.SetMeta("czn_character_select_menu_navigation_owner", Variant.From(in _ownerId));
			_externalNavigationCaptured = true;
		}
	}

	private void ConfigureNavigation()
	{
		if (_handle == null || !GodotObject.IsInstanceValid(_handle))
		{
			return;
		}
		CaptureExternalNavigation();
		NodePath path = _handle.GetPath();
		NodePath nodePath = (GodotObject.IsInstanceValid(_nativeCharacterButton) ? _nativeCharacterButton.GetPath() : path);
		if (GodotObject.IsInstanceValid(_nativeCharacterButton))
		{
			_nativeCharacterButton.FocusNeighborLeft = path;
		}
		_handle.FocusNeighborLeft = path;
		_handle.FocusNeighborTop = path;
		_handle.FocusNeighborBottom = path;
		if (!_drawerOpen || _itemBindings.Count == 0)
		{
			_handle.FocusNeighborRight = nodePath;
			_handle.FocusPrevious = nodePath;
			_handle.FocusNext = nodePath;
			return;
		}
		for (int i = 0; i < _itemBindings.Count; i++)
		{
			BaseButton button = _itemBindings[i].Button;
			BaseButton button2 = _itemBindings[(i - 1 + _itemBindings.Count) % _itemBindings.Count].Button;
			BaseButton button3 = _itemBindings[(i + 1) % _itemBindings.Count].Button;
			button.FocusNeighborLeft = path;
			button.FocusNeighborRight = nodePath;
			button.FocusNeighborTop = button2.GetPath();
			button.FocusNeighborBottom = button3.GetPath();
			button.FocusPrevious = button2.GetPath();
			button.FocusNext = button3.GetPath();
		}
		_handle.FocusNeighborRight = _itemBindings[0].Button.GetPath();
		Button? handle = _handle;
		List<ItemBinding> itemBindings = _itemBindings;
		handle.FocusPrevious = itemBindings[itemBindings.Count - 1].Button.GetPath();
		_handle.FocusNext = _itemBindings[0].Button.GetPath();
	}

	private void RestoreExternalNavigation()
	{
		if (_externalNavigationCaptured && GodotObject.IsInstanceValid(_nativeCharacterButton))
		{
			if (!_nativeCharacterButton.HasMeta("czn_character_select_menu_navigation_owner") || !string.Equals(_nativeCharacterButton.GetMeta("czn_character_select_menu_navigation_owner").AsString(), _ownerId, StringComparison.Ordinal))
			{
				_externalNavigationCaptured = false;
				return;
			}
			_nativeCharacterButton.FocusNeighborLeft = _nativeLeftNeighbor;
			_nativeCharacterButton.RemoveMeta("czn_character_select_menu_navigation_owner");
			_nativeCharacterButton.RemoveMeta("czn_character_select_menu_native_left_neighbor");
			_externalNavigationCaptured = false;
		}
	}

	private static Control? FindFirstCharacterButton(Node? root)
	{
		if (root == null)
		{
			return null;
		}
		foreach (Node child in root.GetChildren())
		{
			if (child is Control { Visible: not false } control && (control.FocusMode != FocusModeEnum.None || control is BaseButton))
			{
				return control;
			}
			Control control2 = FindFirstCharacterButton(child);
			if (control2 != null)
			{
				return control2;
			}
		}
		return null;
	}

	private Texture2D? LoadTexture(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		if (_textures.TryGetValue(path, out Texture2D value) && GodotObject.IsInstanceValid(value))
		{
			return value;
		}
		Texture2D texture2D = LoadTextureResource(path);
		if (texture2D != null && GodotObject.IsInstanceValid(texture2D))
		{
			_textures[path] = texture2D;
		}
		return texture2D;
	}

	private static Texture2D? LoadTextureResource(string path)
	{
		if (IsRawImagePath(path))
		{
			Texture2D texture2D = LoadRawImage(path);
			if (texture2D != null)
			{
				return texture2D;
			}
		}
		try
		{
			return ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
		}
		catch
		{
			return IsRawImagePath(path) ? null : LoadRawImage(path);
		}
	}

	private static Texture2D? LoadRawImage(string path)
	{
		try
		{
			if (!global::Godot.FileAccess.FileExists(path))
			{
				return null;
			}
			Image image = Image.LoadFromFile(path);
			return (image == null || image.IsEmpty()) ? null : ImageTexture.CreateFromImage(image);
		}
		catch
		{
			return null;
		}
	}

	private static bool IsRawImagePath(string path)
	{
		if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
		{
			return path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static Label CreateLabel(string text, int fontSize, Color color)
	{
		Label label = new Label();
		label.Text = text;
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static void ApplyActionStyle(BaseButton button)
	{
		button.AddThemeStyleboxOverride("normal", CreateFlatStyle(SurfaceRaised, Border, 1, 10));
		button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1f, 0.965f, 0.91f), new Color(1f, 0.61f, 0f, 0.36f), 1, 10));
		button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(1f, 0.93f, 0.84f), Accent, 1, 10));
		StyleBoxFlat styleBoxFlat = CreateFlatStyle(Colors.Transparent, Accent, 2, 10);
		styleBoxFlat.DrawCenter = false;
		button.AddThemeStyleboxOverride("focus", styleBoxFlat);
		ApplyTransparentFont(button);
	}

	private static void ApplyToggleStyle(BaseButton button)
	{
		button.AddThemeStyleboxOverride("normal", CreateFlatStyle(SurfaceRaised, Border, 1, 10));
		button.AddThemeStyleboxOverride("hover", CreateFlatStyle(new Color(1f, 0.975f, 0.94f), new Color(1f, 0.61f, 0f, 0.28f), 1, 10));
		button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(1f, 0.955f, 0.9f), new Color(1f, 0.61f, 0f, 0.48f), 1, 10));
		button.AddThemeStyleboxOverride("hover_pressed", CreateFlatStyle(new Color(1f, 0.94f, 0.86f), Accent, 1, 10));
		StyleBoxFlat styleBoxFlat = CreateFlatStyle(Colors.Transparent, Accent, 2, 10);
		styleBoxFlat.DrawCenter = false;
		button.AddThemeStyleboxOverride("focus", styleBoxFlat);
		ApplyTransparentFont(button);
	}

	private static void UpdateToggleVisual(ItemBinding binding, bool enabled)
	{
		if (binding.ToggleTrack != null && binding.ToggleKnob != null)
		{
			binding.ToggleTrack.AddThemeStyleboxOverride("panel", CreateFlatStyle(enabled ? Accent : ToggleOff, Colors.Transparent, 0, 13));
			binding.ToggleKnob.AddThemeStyleboxOverride("panel", CreateFlatStyle(ToggleKnob, Colors.Transparent, 0, 10, 2));
			float num = (enabled ? 25f : 3f);
			binding.ToggleKnob.OffsetLeft = num;
			binding.ToggleKnob.OffsetTop = 3f;
			binding.ToggleKnob.OffsetRight = num + 20f;
			binding.ToggleKnob.OffsetBottom = 23f;
		}
	}

	private static void ApplyHandleStyle(Button button, bool open)
	{
		Color background = (open ? Accent : Dark);
		Color border = (open ? Accent : new Color(1f, 0.61f, 0f, 0.72f));
		button.AddThemeStyleboxOverride("normal", CreateFlatStyle(background, border, 1, 0));
		button.AddThemeStyleboxOverride("hover", CreateFlatStyle(open ? new Color(1f, 0.69f, 0.16f) : new Color(0.15f, 0.16f, 0.17f), Accent, 2, 0));
		button.AddThemeStyleboxOverride("pressed", CreateFlatStyle(new Color(0.92f, 0.53f, 0f), Accent, 2, 0));
		StyleBoxFlat styleBoxFlat = CreateFlatStyle(Colors.Transparent, Accent, 3, 0);
		styleBoxFlat.DrawCenter = false;
		button.AddThemeStyleboxOverride("focus", styleBoxFlat);
		ApplyTransparentFont(button);
	}

	private static void ApplyTransparentFont(BaseButton button)
	{
		Color color = new Color(1f, 1f, 1f, 0f);
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color);
		button.AddThemeColorOverride("font_pressed_color", color);
		button.AddThemeColorOverride("font_focus_color", color);
	}

	private static StyleBoxFlat CreateFlatStyle(Color background, Color border, int borderWidth, int radius, int shadowSize = 0)
	{
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			AntiAliasing = true,
			AntiAliasingSize = 0.75f
		};
		if (shadowSize > 0)
		{
			styleBoxFlat.ShadowColor = new Color(0f, 0f, 0f, 0.2f);
			styleBoxFlat.ShadowSize = shadowSize;
			styleBoxFlat.ShadowOffset = new Vector2(4f, 6f);
		}
		return styleBoxFlat;
	}

	private static string ReadOwner(Control node)
	{
		if (!node.HasMeta("czn_character_select_menu_owner"))
		{
			return string.Empty;
		}
		return node.GetMeta("czn_character_select_menu_owner").AsString();
	}

	private static bool ReadOpenState(Control screen)
	{
		if (screen.HasMeta("czn_character_select_menu_open"))
		{
			return screen.GetMeta("czn_character_select_menu_open").AsBool();
		}
		return false;
	}

	private static string Resolve(Func<string> value, string fallback)
	{
		try
		{
			string text = value();
			return string.IsNullOrWhiteSpace(text) ? fallback : text;
		}
		catch
		{
			return fallback;
		}
	}

	private static bool Resolve(Func<bool>? value, bool fallback)
	{
		try
		{
			return value?.Invoke() ?? fallback;
		}
		catch
		{
			return fallback;
		}
	}
}
