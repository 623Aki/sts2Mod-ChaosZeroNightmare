using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CznCore.Godot;
using CznCore.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;

namespace CznCore.Settings;

internal static class SkinSettingsUi
{
	private enum RebuildScope
	{
		Global,
		Multiplayer,
		Extensions,
		Shared,
		Character
	}

	private sealed record SettingsUiShell(VBoxContainer PanelVBox, HBoxContainer Shell, VBoxContainer Sidebar, VBoxContainer ContentHost);

	private sealed record SettingsUiExtensionContext(NSettingsScreen Screen, NSettingsPanel Panel, VBoxContainer VBox, RichTextLabel TemplateLabel, PackedScene SliderScene, PackedScene? TickboxVisualScene, ISkinSettingsUiText Text);

	private enum SettingsControlRowRole
	{
		Standalone,
		Parent,
		Child
	}

	private const string ClipperPath = "ScrollContainer/Mask/Clipper";

	private const string SoundSettingsPanelPath = "ScrollContainer/Mask/Clipper/SoundSettings";

	private const string TemplateLabelPath = "ScrollContainer/Mask/Clipper/SoundSettings/VBoxContainer/SfxVolume/Label";

	private const string ModTabScenePath = "res://scenes/screens/settings_tab.tscn";

	private const string SettingsSliderScenePath = "res://scenes/screens/settings_slider.tscn";

	private const string TickboxVisualScenePath = "res://scenes/ui/tickbox.tscn";

	private const string TabBindingMeta = "czn_mod_settings_tab_bound";

	private const string SliderBindingMeta = "czn_mod_slider_bound";

	private const string ShellVersionMeta = "czn_mod_settings_shell_version";

	private const int CurrentShellVersion = 7;

	private const string SelectedPageMeta = "czn_mod_settings_selected_page";

	private const string PageIdMeta = "czn_mod_settings_page_id";

	private const string SelectedGlobalCategoryMeta = "czn_mod_settings_selected_global_category";

	private const string GlobalCategoryMeta = "czn_mod_settings_global_category";

	private const string GlobalCategoryIdMeta = "czn_mod_settings_global_category_id";

	private const string GlobalCategorySignatureMeta = "czn_mod_settings_global_category_signature";

	private const string SelectedCharacterCategoryMeta = "czn_mod_settings_selected_character_category";

	private const string CharacterCategoryMeta = "czn_mod_settings_character_category";

	private const string CategoryIdMeta = "czn_mod_settings_category_id";

	private const string FocusPreferredMeta = "czn_mod_settings_focus_preferred";

	private const string GlobalPageId = "global";

	private const string GlobalCategoryGeneral = "general";

	private const string GlobalCategoryMultiplayer = "multiplayer";

	private const string CharacterCategoryVisual = "visual";

	private const string CharacterCategoryCombat = "combat";

	private const string CharacterCategoryEffects = "effects";

	private const string CharacterCategoryAudio = "audio";

	private const string ShellName = "CznModSettingsShell";

	private const string SidebarName = "CznModSettingsSidebar";

	private const string ContentHostName = "CznModSettingsContentHost";

	private const string GlobalPageName = "CznModSettingsPage_Global";

	private const string CharacterPageNamePrefix = "CznModSettingsPage_";

	private const string GlobalNavName = "CznModSettingsNav_Global";

	private const string CharacterNavNamePrefix = "CznModSettingsNav_";

	private const float ShellWidth = 1012f;

	private const float SidebarWidth = 118f;

	private const float ShellGap = 12f;

	private const float NavButtonWidth = 96f;

	private const float NavButtonHeight = 96f;

	private const float NavIconSize = 48f;

	private const float NavLabelHeight = 22f;

	private const float ContentWidth = 882f;

	private const float LabelWidth = 330f;

	private const float SliderWidth = 324f;

	private const float DropdownWidth = 320f;

	private const float CategoryButtonWidth = 206f;

	private const float CombatModeButtonWidth = 106f;

	private const float CombatModeSegmentWidth = 334f;

	private const float ToggleWidth = 64f;

	private const float RowHeight = 56f;

	private const float DescribedRowHeight = 74f;

	private const int MinSettingsFontSize = 14;

	private const int MaxSettingsFontSize = 28;

	private const int RowFontDelta = 2;

	private const int DescriptionFontDelta = -2;

	private static readonly Color CreamGold = new Color(0.831f, 0.784f, 0.557f);

	private static readonly Color DimText = new Color(0.541f, 0.494f, 0.361f);

	private static readonly Color TextColor = new Color(0.9f, 0.85f, 0.75f);

	private static readonly Color DisabledRowModulate = new Color(1f, 1f, 1f, 0.46f);

	private static readonly Color ModHeaderBg = new Color(0.173f, 0.263f, 0.31f);

	private static readonly Color SubHeaderBg = new Color(0.105f, 0.147f, 0.165f, 0.82f);

	private static readonly Color StatusHeaderBg = new Color(0.098f, 0.137f, 0.153f, 0.92f);

	private static readonly Color SubHeaderLineColor = new Color(0.831f, 0.784f, 0.557f, 0.46f);

	private static readonly Color RowSeparatorColor = new Color(0.52f, 0.49f, 0.42f, 0.55f);

	private static readonly Color NavIdleBg = new Color(0.118f, 0.157f, 0.173f, 0.84f);

	private static readonly Color NavHoverBg = new Color(0.18f, 0.25f, 0.29f, 0.92f);

	private static readonly Color NavSelectedBg = new Color(0.2f, 0.31f, 0.36f, 0.96f);

	private const string CznTabName = "CznModSettingsTab";

	private const string CznPanelName = "CznModSettingsPanel";

	private const string RootHeaderSectionName = "CznModSettingsRootHeader";

	private const string GlobalCategoryNavSectionName = "CznModGlobalCategoryNav";

	private const string GlobalHeaderSectionName = "CznModGlobalHeader";

	private const string MultiplayerHeaderSectionName = "CznModMultiplayerHeader";

	private const string ExtensionSectionNamePrefix = "CznModExtension_";

	private const string MultiplayerIntroSectionName = "CznModMultiplayerIntro";

	private const string MultiplayerSummarySectionName = "CznModMultiplayerSummary";

	private const string TeammateVisualHeaderSectionName = "CznModTeammateVisualHeader";

	private const string TeammateAudioHeaderSectionName = "CznModTeammateAudioHeader";

	private const string DebugLogSectionName = "CznModDebugLogEnabled";

	private const string GlobalEffectsSectionName = "CznModGlobalEffectsEnabled";

	private const string GlobalParticlesSectionName = "CznModGlobalParticlesEnabled";

	private const string GlobalCombatSfxSectionName = "CznModGlobalCombatSfxEnabled";

	private const string GlobalVoiceSectionName = "CznModGlobalVoiceEnabled";

	private const string TeammatePresentationSectionName = "CznModTeammatePresentationEnabled";

	private const string TeammateEffectsSectionName = "CznModTeammateEffectsEnabled";

	private const string TeammateCombatSfxSectionName = "CznModTeammateCombatSfxEnabled";

	private const string TeammateVoiceSectionName = "CznModTeammateVoiceEnabled";

	private const string TeammateVoiceCooldownSectionName = "CznModTeammateVoiceCooldown";

	private const string TeammateCutInSectionName = "CznModTeammateCutInEnabled";

	private const string TeammateAttackFullScreenPresentationModeSectionName = "CznModTeammateAttackFullScreenPresentationMode";

	private const string GlobalCombatPresentationApplySectionName = "CznModGlobalCombatPresentationApply";

	private const string CombatPresentationModeSectionName = "CznModCombatPresentationMode";

	private const string CombatPresentationSummarySectionName = "CznModCombatPresentationSummary";

	private const string CharacterStatusSectionName = "CznModCharacterStatus";

	private const string CharacterCategoryNavSectionName = "CznModCharacterCategoryNav";

	private const string CharacterHeaderSectionName = "CznModCharacterHeader";

	private const string CharacterExtensionSectionNamePrefix = "CznModCharacterExtension_";

	private const string CharacterEffectsSectionName = "CznModCharacterEffectsEnabled";

	private const string ParticlesSectionName = "CznModParticlesEnabled";

	private const string CombatSfxSectionName = "CznModCombatSfxEnabled";

	private const string CharacterVoiceSectionName = "CznModCharacterVoiceEnabled";

	private const string VisualHeaderSectionName = "CznModVisualHeader";

	private const string CombatEffectsHeaderSectionName = "CznModCombatEffectsHeader";

	private const string AudioHeaderSectionName = "CznModAudioHeader";

	private const string VoiceSectionName = "CznModVoiceVolume";

	private const string VoiceCooldownSectionName = "CznModVoiceCooldown";

	private const string EffectVolumeSectionName = "CznModEffectVolume";

	private const string EffectBrightnessSectionName = "CznModEffectBrightness";

	private const string EffectOpacitySectionName = "CznModEffectOpacity";

	private const string CharacterSelectBackgroundSectionName = "CznModCharacterSelectBackground";

	private const string CardAnimationHeaderSectionName = "CznModCardAnimationHeader";

	private const string CombatPresentationCustomHeaderSectionName = "CznModCombatPresentationCustomHeader";

	private const string LeftSideCardPresentationHeaderSectionName = "CznModLeftSideCardPresentationHeader";

	private const string BattleReadyEnabledSectionName = "CznModBattleReadyEnabled";

	private const string CardPlayPresentationEnabledSectionName = "CznModCardPlayPresentationEnabled";

	private const string AttackFullScreenPresentationModeSectionName = "CznModAttackFullScreenPresentationMode";

	private const string CardSelectionPresentationTriggerSectionName = "CznModCardSelectionPresentationTrigger";

	private const string MeleeApproachMovementEnabledSectionName = "CznModMeleeApproachMovementEnabled";

	private const string OfficialAttackTimingEnabledSectionName = "CznModOfficialAttackTimingEnabled";

	private const string BattleReadyScaleSectionName = "CznModBattleReadyScale";

	private const string BattleReadyOpacitySectionName = "CznModBattleReadyOpacity";

	private const string BattleReadyOffsetXSectionName = "CznModBattleReadyOffsetX";

	private const string BattleReadyOffsetYSectionName = "CznModBattleReadyOffsetY";

	private const string BattleReadyResetPositionSectionName = "CznModBattleReadyResetPosition";

	private const string DefeatCgEnabledSectionName = "CznModDefeatCgEnabled";

	public static void TryInjectShared(NSettingsScreen screen, ISkinSettingsUiText text)
	{
		try
		{
			if (!EnsureModSettingsTabAndPanel(screen, text, out NSettingsPanel panel) || panel == null)
			{
				return;
			}
			VBoxContainer nodeOrNull = panel.GetNodeOrNull<VBoxContainer>(new NodePath("VBoxContainer"));
			if (nodeOrNull == null)
			{
				SkinLog.Warn("Shared settings UI inject skipped: mod panel missing VBoxContainer.");
				return;
			}
			RichTextLabel nodeOrNull2 = screen.GetNodeOrNull<RichTextLabel>(new NodePath("ScrollContainer/Mask/Clipper/SoundSettings/VBoxContainer/SfxVolume/Label"));
			PackedScene packedScene = ResourceLoader.Load<PackedScene>("res://scenes/screens/settings_slider.tscn", null, ResourceLoader.CacheMode.Reuse);
			PackedScene tickboxVisualScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/tickbox.tscn", null, ResourceLoader.CacheMode.Reuse);
			if (nodeOrNull2 == null || packedScene == null)
			{
				SkinLog.Warn("Shared settings UI inject skipped: missing settings template.");
				return;
			}
			SettingsUiShell settingsUiShell = EnsureSettingsShell(panel, nodeOrNull);
			VBoxContainer vBoxContainer = EnsureSettingsPage(settingsUiShell.ContentHost, "CznModSettingsPage_Global", "global");
			SettingsUiExtensionContext ctx = new SettingsUiExtensionContext(screen, panel, vBoxContainer, nodeOrNull2, packedScene, tickboxVisualScene, text);
			bool flag = EnsureGlobalNavigationButton(panel, settingsUiShell.Sidebar, nodeOrNull2, string.Empty, text);
			flag |= TryCreateTopSpacerSection(vBoxContainer, "CznModSettingsRootHeader");
			flag |= EnsureGlobalCategoryNavSection(ctx, includeCharacterCategories: false);
			flag |= TryCreateExtensionSettingsSections(ctx);
			RefreshPanelLayout(panel, nodeOrNull);
			RefreshSettingsShellSelection(panel);
			RefreshGlobalCategorySelection(panel);
			RefreshFocusNeighbors(panel);
			SkinLog.InfoOnce("czn-mod-config-shared-host", "CZN Mod Config shared host: built=" + flag + ", categories=" + CountGlobalCategories(settingsUiShell) + ".");
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Shared settings UI inject failed: " + ex);
		}
	}

	private static int CountGlobalCategories(SettingsUiShell shell)
	{
		List<string> list = new List<string>();
		foreach (Node child in shell.ContentHost.GetChildren())
		{
			if (child is Control control && control.HasMeta("czn_mod_settings_page_id") && string.Equals(control.GetMeta("czn_mod_settings_page_id").ToString(), "global", StringComparison.Ordinal))
			{
				CollectGlobalCategoryIds(control, list);
			}
		}
		return list.Count;
	}

	private static bool EnsureGlobalCategoryNavSection(SettingsUiExtensionContext ctx, bool includeCharacterCategories)
	{
		includeCharacterCategories |= ctx.VBox.GetNodeOrNull(new NodePath("CznModGlobalHeader")) != null;
		List<(string, string)> list = new List<(string, string)>();
		if (includeCharacterCategories)
		{
			list.Add(("general", ctx.Text.GlobalSettingsHeader));
			list.Add(("multiplayer", ctx.Text.MultiplayerSettingsHeader));
		}
		foreach (SharedSettingsExtensionCategory category in SharedSettingsExtensionRegistry.GetCategories())
		{
			list.Add((category.Id, category.Label()));
		}
		string text = BuildGlobalCategorySignature(list);
		Node nodeOrNull = ctx.VBox.GetNodeOrNull(new NodePath("CznModGlobalCategoryNav"));
		if (nodeOrNull != null)
		{
			if (nodeOrNull.HasMeta("czn_mod_settings_global_category_signature") && string.Equals(nodeOrNull.GetMeta("czn_mod_settings_global_category_signature").ToString(), text, StringComparison.Ordinal))
			{
				return false;
			}
			ctx.VBox.RemoveChild(nodeOrNull);
			nodeOrNull.QueueFree();
		}
		MarginContainer marginContainer = CreateSection("CznModGlobalCategoryNav", 0, 12, 46f);
		marginContainer.SetMeta("czn_mod_settings_global_category_signature", text);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(858f, 38f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 8);
		foreach (var (categoryId, label) in list)
		{
			hBoxContainer.AddChild(CreateGlobalCategoryButton(ctx, categoryId, label), forceReadableName: false, Node.InternalMode.Disabled);
		}
		marginContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		ctx.VBox.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		if (nodeOrNull != null)
		{
			ctx.VBox.MoveChild(marginContainer, 1);
		}
		return true;
	}

	private static string BuildGlobalCategorySignature(IReadOnlyList<(string Id, string Label)> categories)
	{
		List<string> list = new List<string>();
		foreach (var category in categories)
		{
			string item = category.Id;
			list.Add(item);
		}
		return string.Join("|", list);
	}

	private static Button CreateGlobalCategoryButton(SettingsUiExtensionContext ctx, string categoryId, string label)
	{
		CznSettingsButton cznSettingsButton = new CznSettingsButton();
		cznSettingsButton.Text = label;
		cznSettingsButton.ToggleMode = false;
		cznSettingsButton.FocusMode = Control.FocusModeEnum.All;
		cznSettingsButton.CustomMinimumSize = new Vector2(206f, 36f);
		cznSettingsButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		cznSettingsButton.Set("layout_mode", 2);
		cznSettingsButton.SetMeta("czn_mod_settings_global_category_id", categoryId);
		cznSettingsButton.AddThemeFontOverride("font", ctx.TemplateLabel.GetThemeFont("normal_font"));
		cznSettingsButton.AddThemeFontSizeOverride("font_size", SettingsFontSize(ctx.TemplateLabel, -2));
		cznSettingsButton.AddThemeColorOverride("font_color", CreamGold);
		cznSettingsButton.Pressed += () =>
		{
			SetSelectedGlobalCategory(ctx.Panel, categoryId);
		};
		ApplyCategoryButtonStyle(cznSettingsButton, string.Equals(GetSelectedGlobalCategory(ctx.Panel), categoryId, StringComparison.Ordinal));
		return CznSettingsInputControls.AttachNativeOwner(cznSettingsButton);
	}

	private static void AssignGlobalCategory(VBoxContainer vbox, string categoryId, params string[] sectionNames)
	{
		foreach (string path in sectionNames)
		{
			vbox.GetNodeOrNull(new NodePath(path))?.SetMeta("czn_mod_settings_global_category", categoryId);
		}
	}

	private static bool EnsureModSettingsTabAndPanel(NSettingsScreen screen, ISkinSettingsUiText text, out NSettingsPanel? panel)
	{
		panel = null;
		NSettingsTabManager nSettingsTabManager = screen.GetNodeOrNull<NSettingsTabManager>(new NodePath("SettingsTabManager")) ?? screen.GetNodeOrNull<NSettingsTabManager>(new NodePath("%SettingsTabManager"));
		if (nSettingsTabManager == null)
		{
			SkinLog.Warn("Settings UI inject skipped: missing SettingsTabManager.");
			return false;
		}
		Control nodeOrNull = screen.GetNodeOrNull<Control>(new NodePath("ScrollContainer/Mask/Clipper"));
		if (nodeOrNull == null)
		{
			SkinLog.Warn("Settings UI inject skipped: missing settings clipper.");
			return false;
		}
		panel = nodeOrNull.GetNodeOrNull<NSettingsPanel>(new NodePath("CznModSettingsPanel"));
		if (panel == null)
		{
			NSettingsPanel nSettingsPanel = screen.GetNodeOrNull<NSettingsPanel>(new NodePath("%SoundSettings")) ?? screen.GetNodeOrNull<NSettingsPanel>(new NodePath("ScrollContainer/Mask/Clipper/SoundSettings"));
			if (nSettingsPanel == null)
			{
				SkinLog.Warn("Settings UI inject skipped: missing SoundSettings template panel.");
				return false;
			}
			panel = nSettingsPanel.Duplicate() as NSettingsPanel;
			if (panel == null)
			{
				SkinLog.Warn("Settings UI inject skipped: duplicate settings panel failed.");
				return false;
			}
			panel.Name = "CznModSettingsPanel";
			panel.UniqueNameInOwner = true;
			panel.Visible = false;
			VBoxContainer nodeOrNull2 = panel.GetNodeOrNull<VBoxContainer>(new NodePath("VBoxContainer"));
			if (nodeOrNull2 == null)
			{
				SkinLog.Warn("Settings UI inject skipped: duplicated panel missing VBoxContainer.");
				return false;
			}
			nodeOrNull2.CustomMinimumSize = new Vector2(1012f, 0f);
			nodeOrNull2.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			nodeOrNull2.AddThemeConstantOverride("separation", 0);
			NSettingsPanel createdPanel = panel;
			VBoxContainer createdVBox = nodeOrNull2;
			nodeOrNull2.Resized += () =>
			{
				RefreshPanelLayout(createdPanel, createdVBox);
			};
			nodeOrNull.AddChild(panel, forceReadableName: false, Node.InternalMode.Disabled);
			GodotNodeUtil.ClearChildren(nodeOrNull2);
		}
		NSettingsTab nSettingsTab = nSettingsTabManager.GetNodeOrNull<NSettingsTab>(new NodePath("CznModSettingsTab"));
		if (nSettingsTab == null)
		{
			PackedScene packedScene = ResourceLoader.Load<PackedScene>("res://scenes/screens/settings_tab.tscn", null, ResourceLoader.CacheMode.Reuse);
			if (packedScene == null)
			{
				SkinLog.Warn("Settings UI inject skipped: missing settings tab scene.");
				return false;
			}
			nSettingsTab = packedScene.Instantiate<NSettingsTab>(PackedScene.GenEditState.Disabled);
			nSettingsTab.Name = "CznModSettingsTab";
			nSettingsTab.UniqueNameInOwner = true;
			Node node = nSettingsTabManager.GetNodeOrNull(new NodePath("RightTabIcon")) ?? nSettingsTabManager.GetNodeOrNull(new NodePath("RightTriggerIcon"));
			nSettingsTabManager.AddChild(nSettingsTab, forceReadableName: false, Node.InternalMode.Disabled);
			if (node != null)
			{
				nSettingsTabManager.MoveChild(nSettingsTab, node.GetIndex());
			}
		}
		nSettingsTab.Set("layout_mode", 2);
		SetTabLabelDeferred(nSettingsTab, text);
		EnsureTabBinding(nSettingsTabManager, nSettingsTab, panel);
		return true;
	}

	private static void EnsureTabBinding(NSettingsTabManager tabManager, NSettingsTab tab, NSettingsPanel panel)
	{
		try
		{
			if (AccessTools.Field(typeof(NSettingsTabManager), "_tabs")?.GetValue(tabManager) is Dictionary<NSettingsTab, NSettingsPanel> dictionary)
			{
				dictionary[tab] = panel;
			}
			if (!tab.HasMeta("czn_mod_settings_tab_bound"))
			{
				tab.Connect(NClickableControl.SignalName.Released, Callable.From((NButton _) =>
				{
					SwitchTabTo(tabManager, tab);
				}));
				tab.SetMeta("czn_mod_settings_tab_bound", true);
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Settings tab binding failed: " + SkinLog.FormatException(ex));
		}
	}

	private static void SwitchTabTo(NSettingsTabManager tabManager, NSettingsTab tab)
	{
		try
		{
			AccessTools.Method(typeof(NSettingsTabManager), "SwitchTabTo")?.Invoke(tabManager, new object[1] { tab });
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Settings tab switch failed: " + SkinLog.FormatException(ex));
		}
	}

	private static void SetTabLabelDeferred(NSettingsTab tab, ISkinSettingsUiText text)
	{
		GodotScheduler.Deferred(() =>
		{
			if (GodotNodeUtil.IsAlive(tab) && tab.IsNodeReady())
			{
				tab.SetLabel(text.CznSettingsTab);
			}
		});
	}

	private static void RebuildExtensionSectionsDeferred(SettingsUiExtensionContext ctx)
	{
		GodotScheduler.Deferred(() =>
		{
			if (GodotNodeUtil.IsAlive(ctx.Screen) && GodotNodeUtil.IsAlive(ctx.Panel))
			{
				VBoxContainer nodeOrNull = ctx.Panel.GetNodeOrNull<VBoxContainer>(new NodePath("VBoxContainer"));
				if (nodeOrNull != null)
				{
					int insertionIndex = RemoveExtensionSections(ctx.VBox);
					TryCreateExtensionSettingsSections(ctx);
					MoveExtensionSectionsToIndex(ctx.VBox, insertionIndex);
					RefreshPanelLayout(ctx.Panel, nodeOrNull);
					RefreshGlobalCategorySelection(ctx.Panel);
					RefreshFocusNeighbors(ctx.Panel);
				}
			}
		});
	}

	private static int RemoveExtensionSections(VBoxContainer vbox)
	{
		return RemoveSections(vbox, (Node child) => child.Name.ToString().StartsWith("CznModExtension_", StringComparison.Ordinal));
	}

	private static int RemoveSections(VBoxContainer vbox, Func<Node, bool> shouldRemove)
	{
		int num = -1;
		foreach (Node child in vbox.GetChildren())
		{
			if (shouldRemove(child))
			{
				num = ((num < 0) ? child.GetIndex() : Math.Min(num, child.GetIndex()));
				vbox.RemoveChild(child);
				child.QueueFree();
			}
		}
		return num;
	}

	private static void MoveExtensionSectionsToIndex(VBoxContainer vbox, int insertionIndex)
	{
		MoveSectionsToIndex(vbox, (Node child) => child.Name.ToString().StartsWith("CznModExtension_", StringComparison.Ordinal), insertionIndex);
	}

	private static void MoveSectionsToIndex(VBoxContainer vbox, Func<Node, bool> shouldMove, int insertionIndex)
	{
		if (insertionIndex < 0)
		{
			return;
		}
		List<Node> list = new List<Node>();
		foreach (Node child in vbox.GetChildren())
		{
			if (shouldMove(child))
			{
				list.Add(child);
			}
		}
		int num = Math.Min(insertionIndex, Math.Max(0, vbox.GetChildCount() - list.Count));
		foreach (Node item in list)
		{
			vbox.MoveChild(item, Math.Min(num, vbox.GetChildCount() - 1));
			num++;
		}
	}

	private static void UpdateTickboxVisualState(Control visual, bool ticked)
	{
		if (GodotNodeUtil.IsAlive(visual))
		{
			CanvasItem nodeOrNull = visual.GetNodeOrNull<CanvasItem>(new NodePath("Ticked"));
			CanvasItem nodeOrNull2 = visual.GetNodeOrNull<CanvasItem>(new NodePath("NotTicked"));
			if (nodeOrNull != null)
			{
				nodeOrNull.Visible = ticked;
			}
			if (nodeOrNull2 != null)
			{
				nodeOrNull2.Visible = !ticked;
			}
		}
	}

	private static void AddTransparentButtonStyles(Button button)
	{
		button.AddThemeStyleboxOverride("normal", CreateTransparentStyle());
		button.AddThemeStyleboxOverride("hover", CreateTransparentStyle());
		button.AddThemeStyleboxOverride("pressed", CreateTransparentStyle());
		button.AddThemeStyleboxOverride("focus", CreateTransparentStyle());
		button.AddThemeStyleboxOverride("disabled", CreateTransparentStyle());
	}

	private static void RefreshPanelLayout(NSettingsPanel panel, VBoxContainer vbox)
	{
		if (GodotNodeUtil.IsAlive(panel) && GodotNodeUtil.IsAlive(vbox))
		{
			panel.CustomMinimumSize = new Vector2(panel.CustomMinimumSize.X, Math.Max(640f, vbox.Size.Y + 160f));
		}
	}

	private static void RefreshPanelLayout(NSettingsPanel panel)
	{
		VBoxContainer nodeOrNull = panel.GetNodeOrNull<VBoxContainer>(new NodePath("VBoxContainer"));
		if (nodeOrNull != null)
		{
			RefreshPanelLayout(panel, nodeOrNull);
		}
	}

	private static void RefreshFocusNeighbors(NSettingsPanel panel)
	{
		try
		{
			if (TryGetSettingsShell(panel, out SettingsUiShell shell))
			{
				List<Control> list = CollectSidebarFocusables(shell.Sidebar);
				Control control = ResolveSelectedNavigation(panel, list);
				Control control2 = ResolveSelectedPage(panel, shell.ContentHost);
				List<List<Control>> rows = new List<List<Control>>();
				if (control2 != null)
				{
					CollectFocusRows(control2, rows);
				}
				Control control3 = ResolveContentEntry(rows);
				ApplySidebarNavigation(list, control, control3);
				ApplyContentNavigation(rows, control);
				Control value = control ?? ((list.Count > 0) ? list[0] : null) ?? control3;
				AccessTools.Field(typeof(NSettingsPanel), "_firstControl")?.SetValue(panel, value);
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Settings focus graph refresh failed: " + SkinLog.FormatException(ex));
		}
	}

	private static List<Control> CollectSidebarFocusables(VBoxContainer sidebar)
	{
		List<Control> list = new List<Control>();
		foreach (Node child in sidebar.GetChildren())
		{
			if (child is Control visualControl)
			{
				Control control = CznSettingsInputControls.ResolveFocusOwner(visualControl);
				if (IsFocusCandidate(control))
				{
					list.Add(control);
				}
			}
		}
		return list;
	}

	private static Control? ResolveSelectedNavigation(NSettingsPanel panel, IReadOnlyList<Control> sidebar)
	{
		string selectedPage = GetSelectedPage(panel);
		foreach (Control item in sidebar)
		{
			Control control = CznSettingsInputControls.ResolveVisualControl(item);
			if (control.HasMeta("czn_mod_settings_page_id") && string.Equals(control.GetMeta("czn_mod_settings_page_id").ToString(), selectedPage, StringComparison.Ordinal))
			{
				return item;
			}
		}
		return null;
	}

	private static Control? ResolveSelectedPage(NSettingsPanel panel, VBoxContainer contentHost)
	{
		string selectedPage = GetSelectedPage(panel);
		foreach (Node child in contentHost.GetChildren())
		{
			if (child is Control { Visible: not false } control && child.HasMeta("czn_mod_settings_page_id") && string.Equals(child.GetMeta("czn_mod_settings_page_id").ToString(), selectedPage, StringComparison.Ordinal))
			{
				return control;
			}
		}
		return null;
	}

	private static void CollectFocusRows(Node node, List<List<Control>> rows)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Control { Visible: false })
			{
				continue;
			}
			if (child is HBoxContainer row)
			{
				List<Control> list = CollectHorizontalFocusGroup(row);
				if (list.Count > 1)
				{
					rows.Add(list);
					continue;
				}
			}
			if (child is Control control2 && IsFocusCandidate(control2))
			{
				int num = 1;
				List<Control> list2 = new List<Control>(num);
				CollectionsMarshal.SetCount(list2, num);
				CollectionsMarshal.AsSpan(list2)[0] = control2;
				rows.Add(list2);
			}
			else
			{
				CollectFocusRows(child, rows);
			}
		}
	}

	private static List<Control> CollectHorizontalFocusGroup(HBoxContainer row)
	{
		List<Control> list = new List<Control>();
		foreach (Node child in row.GetChildren())
		{
			Control control = FindFirstFocusable(child);
			if (control != null)
			{
				list.Add(control);
			}
		}
		return list;
	}

	private static Control? FindFirstFocusable(Node node)
	{
		if (node is Control { Visible: false })
		{
			return null;
		}
		if (node is Control control2 && IsFocusCandidate(control2))
		{
			return control2;
		}
		foreach (Node child in node.GetChildren())
		{
			Control control3 = FindFirstFocusable(child);
			if (control3 != null)
			{
				return control3;
			}
		}
		return null;
	}

	private static bool IsFocusCandidate(Control control)
	{
		if (control.Visible && control.FocusMode == Control.FocusModeEnum.All && !(control is BaseButton { Disabled: not false }))
		{
			if (control is NButton nButton)
			{
				return nButton.IsEnabled;
			}
			return true;
		}
		return false;
	}

	private static Control? ResolveContentEntry(IReadOnlyList<List<Control>> rows)
	{
		foreach (List<Control> row in rows)
		{
			foreach (Control item in row)
			{
				Control control = CznSettingsInputControls.ResolveVisualControl(item);
				if (control.HasMeta("czn_mod_settings_global_category_id") || control.HasMeta("czn_mod_settings_category_id"))
				{
					return ResolvePreferredControl(row, null);
				}
			}
		}
		if (rows.Count != 0)
		{
			return ResolvePreferredControl(rows[0], null);
		}
		return null;
	}

	private static void ApplySidebarNavigation(IReadOnlyList<Control> sidebar, Control? selectedNav, Control? firstContent)
	{
		for (int i = 0; i < sidebar.Count; i++)
		{
			Control control = sidebar[i];
			control.FocusNeighborLeft = control.GetPath();
			control.FocusNeighborTop = sidebar[Math.Max(0, i - 1)].GetPath();
			control.FocusNeighborBottom = sidebar[Math.Min(sidebar.Count - 1, i + 1)].GetPath();
			control.FocusNeighborRight = ((control == selectedNav && firstContent != null) ? firstContent.GetPath() : control.GetPath());
		}
	}

	private static void ApplyContentNavigation(IReadOnlyList<List<Control>> rows, Control? selectedNav)
	{
		for (int i = 0; i < rows.Count; i++)
		{
			List<Control> list = rows[i];
			for (int j = 0; j < list.Count; j++)
			{
				Control control = list[j];
				Control control2 = list[Math.Max(0, j - 1)];
				Control control3 = list[Math.Min(list.Count - 1, j + 1)];
				if (list.Count > 1 && j == 0 && selectedNav != null)
				{
					control.FocusNeighborLeft = selectedNav.GetPath();
				}
				else
				{
					control.FocusNeighborLeft = control2.GetPath();
				}
				control.FocusNeighborRight = control3.GetPath();
				Control control4 = ((i > 0) ? ResolvePreferredControl(rows[i - 1], control) : control);
				Control control5 = ((i < rows.Count - 1) ? ResolvePreferredControl(rows[i + 1], control) : control);
				control.FocusNeighborTop = control4.GetPath();
				control.FocusNeighborBottom = control5.GetPath();
			}
		}
	}

	private static Control ResolvePreferredControl(IReadOnlyList<Control> row, Control? source)
	{
		foreach (Control item in row)
		{
			Control control = CznSettingsInputControls.ResolveVisualControl(item);
			if (control.HasMeta("czn_mod_settings_focus_preferred") && control.GetMeta("czn_mod_settings_focus_preferred").AsBool())
			{
				return item;
			}
			if (control is BaseButton { ButtonPressed: not false })
			{
				return item;
			}
		}
		if (source == null || row.Count == 1)
		{
			return row[0];
		}
		float num = source.GlobalPosition.X + source.Size.X * 0.5f;
		Control result = row[0];
		float num2 = float.PositiveInfinity;
		foreach (Control item2 in row)
		{
			float num3 = Math.Abs(item2.GlobalPosition.X + item2.Size.X * 0.5f - num);
			if (num3 < num2)
			{
				num2 = num3;
				result = item2;
			}
		}
		return result;
	}

	private static string FormatSliderValue(double value, string suffix)
	{
		if (string.IsNullOrEmpty(suffix))
		{
			return $"{value:0}";
		}
		return $"{value:0}{suffix}";
	}

	private static bool TryCreateExtensionSettingsSections(SettingsUiExtensionContext ctx)
	{
		bool flag = false;
		foreach (SharedSettingsExtensionCategory category in SharedSettingsExtensionRegistry.GetCategories())
		{
			for (int i = 0; i < category.Groups.Count; i++)
			{
				SettingsUiExtensionGroup settingsUiExtensionGroup = category.Groups[i];
				string groupPrefix = "CznModExtension_" + BuildExtensionNodeSegment(settingsUiExtensionGroup.ProviderId) + "_";
				flag |= TryCreateSettingsExtensionGroup(ctx, settingsUiExtensionGroup, i == 0, (string segment) => groupPrefix + segment, () =>
				{
					RebuildExtensionSectionsDeferred(ctx);
				}, out string[] sectionNames);
				AssignGlobalCategory(ctx.VBox, category.Id, sectionNames);
			}
		}
		return flag;
	}

	private static bool TryCreateSettingsExtensionGroup(SettingsUiExtensionContext ctx, SettingsUiExtensionGroup group, bool first, Func<string, string> buildSectionName, Action rebuild, out string[] sectionNames)
	{
		string text = buildSectionName("Header");
		List<string> list = new List<string>(group.Controls.Count + 1) { text };
		Action resetAction = ((group.Reset == null) ? null : ((Action)(() =>
		{
			group.Reset();
			rebuild();
		})));
		bool flag = TryCreateGroupHeaderSection(ctx.VBox, ctx.TemplateLabel, text, group.Label(), first, resetAction, ctx.Text);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (SettingsUiExtensionControl control in group.Controls)
		{
			if (!string.IsNullOrWhiteSpace(control.ParentId))
			{
				hashSet.Add(control.ParentId);
			}
		}
		foreach (SettingsUiExtensionControl control2 in group.Controls)
		{
			SettingsControlRowRole rowRole = ((!string.IsNullOrWhiteSpace(control2.ParentId)) ? SettingsControlRowRole.Child : (hashSet.Contains(control2.Id) ? SettingsControlRowRole.Parent : SettingsControlRowRole.Standalone));
			string text2 = buildSectionName(BuildExtensionNodeSegment(control2.Id));
			list.Add(text2);
			flag |= TryCreateSettingsExtensionControl(ctx, control2, text2, rebuild, rowRole);
		}
		sectionNames = list.ToArray();
		return flag;
	}

	private static bool TryCreateSettingsExtensionControl(SettingsUiExtensionContext ctx, SettingsUiExtensionControl control, string sectionName, Action rebuild, SettingsControlRowRole rowRole)
	{
		SettingsUiExtensionToggle settingsUiExtensionToggle = control as SettingsUiExtensionToggle;
		if ((object)settingsUiExtensionToggle == null)
		{
			if (!(control is SettingsUiExtensionSlider settingsUiExtensionSlider))
			{
				SettingsUiExtensionChoice settingsUiExtensionChoice = control as SettingsUiExtensionChoice;
				if ((object)settingsUiExtensionChoice == null)
				{
					SettingsUiExtensionStringChoice settingsUiExtensionStringChoice = control as SettingsUiExtensionStringChoice;
					if ((object)settingsUiExtensionStringChoice == null)
					{
						SettingsUiExtensionText settingsUiExtensionText = control as SettingsUiExtensionText;
						if ((object)settingsUiExtensionText == null)
						{
							return false;
						}
						return TryCreateTextSection(ctx, settingsUiExtensionText, sectionName, rebuild, rowRole);
					}
					return TryCreateStringChoiceSection(ctx, settingsUiExtensionStringChoice, sectionName, rebuild, rowRole);
				}
				List<(int, string)> list = new List<(int, string)>(settingsUiExtensionChoice.Options.Count);
				foreach (SettingsUiExtensionChoiceOption option in settingsUiExtensionChoice.Options)
				{
					list.Add((option.Id, option.Label()));
				}
				return TryCreateDropdownSection(ctx.VBox, ctx.TemplateLabel, sectionName, settingsUiExtensionChoice.Label(), settingsUiExtensionChoice.Description(), list, settingsUiExtensionChoice.Read(), (int value) =>
				{
					settingsUiExtensionChoice.Write(value);
					rebuild();
				}, settingsUiExtensionChoice.Enabled(), rowRole);
			}
			return TryCreateSliderSection(ctx, sectionName, settingsUiExtensionSlider.Label(), settingsUiExtensionSlider.Description(), BuildExtensionNodeSegment(settingsUiExtensionSlider.Id + "_Slider"), settingsUiExtensionSlider.Min, settingsUiExtensionSlider.Max, settingsUiExtensionSlider.Step, settingsUiExtensionSlider.Read(), settingsUiExtensionSlider.Suffix(), settingsUiExtensionSlider.DisplayOffset, settingsUiExtensionSlider.Write, settingsUiExtensionSlider.Enabled(), rowRole);
		}
		return TryCreateToggleSection(ctx, sectionName, settingsUiExtensionToggle.Label(), settingsUiExtensionToggle.Description(), settingsUiExtensionToggle.Read(), (bool value) =>
		{
			settingsUiExtensionToggle.Write(value);
			rebuild();
		}, settingsUiExtensionToggle.Enabled(), rowRole);
	}

	private static bool TryCreateStringChoiceSection(SettingsUiExtensionContext ctx, SettingsUiExtensionStringChoice choice, string sectionName, Action rebuild, SettingsControlRowRole rowRole)
	{
		if (choice.Options.Count == 0)
		{
			return false;
		}
		string b = choice.Read();
		int currentId = 0;
		List<(int, string)> list = new List<(int, string)>(choice.Options.Count);
		for (int i = 0; i < choice.Options.Count; i++)
		{
			SettingsUiExtensionStringChoiceOption settingsUiExtensionStringChoiceOption = choice.Options[i];
			list.Add((i, settingsUiExtensionStringChoiceOption.Label()));
			if (string.Equals(settingsUiExtensionStringChoiceOption.Id, b, StringComparison.Ordinal))
			{
				currentId = i;
			}
		}
		return TryCreateDropdownSection(ctx.VBox, ctx.TemplateLabel, sectionName, choice.Label(), choice.Description(), list, currentId, (int index) =>
		{
			choice.Write(choice.Options[index].Id);
			rebuild();
		}, choice.Enabled(), rowRole);
	}

	// 设计文档 §4.5 方案 B：text 控件行构建器（Godot LineEdit；回车/失焦即写盘并重建面板）。
	private static bool TryCreateTextSection(SettingsUiExtensionContext ctx, SettingsUiExtensionText choice, string sectionName, Action rebuild, SettingsControlRowRole rowRole)
	{
		return TryCreateControlSection(ctx.VBox, ctx.TemplateLabel, sectionName, choice.Label(), choice.Description(), (float rowHeight) =>
		{
			LineEdit lineEdit = new LineEdit
			{
				Name = sectionName + "Input",
				Text = choice.Read() ?? string.Empty,
				CustomMinimumSize = new Vector2(320f, 44f),
				FocusMode = Control.FocusModeEnum.All,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
				Editable = choice.Enabled(),
				Alignment = HorizontalAlignment.Left
			};
			lineEdit.Set("layout_mode", 2);
			lineEdit.AddThemeFontOverride("font", ctx.TemplateLabel.GetThemeFont("normal_font"));
			lineEdit.AddThemeFontSizeOverride("font_size", SettingsFontSize(ctx.TemplateLabel, 0));
			bool commitOnSubmit = true;
			lineEdit.TextSubmitted += (string text) =>
			{
				if (!commitOnSubmit)
				{
					return;
				}
				commitOnSubmit = false;
				choice.Write(text);
				rebuild();
			};
			lineEdit.FocusExited += () =>
			{
				if (!commitOnSubmit)
				{
					return;
				}
				commitOnSubmit = false;
				choice.Write(lineEdit.Text);
				rebuild();
			};
			return lineEdit;
		}, choice.Enabled(), rowRole);
	}

	private static string BuildExtensionNodeSegment(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "Unnamed";
		}
		char[] array = new char[value.Length];
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			array[i] = (char.IsLetterOrDigit(c) ? c : '_');
		}
		return new string(array);
	}

	private static bool TryCreateToggleSection(SettingsUiExtensionContext ctx, string sectionName, string labelText, string descriptionText, bool current, Action<bool> onToggled, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		return TryCreateToggleSection(ctx.VBox, ctx.TemplateLabel, ctx.TickboxVisualScene, sectionName, labelText, descriptionText, current, onToggled, enabled, rowRole);
	}

	private static bool TryCreateSliderSection(SettingsUiExtensionContext ctx, string sectionName, string labelText, string descriptionText, string sliderRootName, double min, double max, double step, double current, string suffix, double displayValueOffset, Action<double> onChanged, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		return TryCreateSliderSection(ctx.VBox, ctx.TemplateLabel, ctx.SliderScene, sectionName, labelText, descriptionText, sliderRootName, min, max, step, current, suffix, displayValueOffset, onChanged, enabled, rowRole);
	}

	private static bool TryCreateTopSpacerSection(VBoxContainer vbox, string sectionName)
	{
		if (vbox.GetNodeOrNull(new NodePath(sectionName)) != null)
		{
			return false;
		}
		Control control = new Control
		{
			Name = sectionName,
			CustomMinimumSize = new Vector2(882f, 18f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		control.Set("layout_mode", 2);
		control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		vbox.AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static bool TryCreateGroupHeaderSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText, bool first, Action? resetAction, ISkinSettingsUiText text, string? actionText = null)
	{
		if (vbox.GetNodeOrNull(new NodePath(sectionName)) != null)
		{
			return false;
		}
		MarginContainer marginContainer = CreateSection(sectionName, (!first) ? 28 : 0, 10, 58f, 0);
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(882f, 46f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panelContainer.Set("layout_mode", 2);
		StyleBoxFlat stylebox = new StyleBoxFlat
		{
			BgColor = ModHeaderBg,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 12f,
			ContentMarginRight = 12f,
			ContentMarginTop = 5f,
			ContentMarginBottom = 5f
		};
		panelContainer.AddThemeStyleboxOverride("panel", stylebox);
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(858f, 36f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 12);
		ColorRect colorRect = new ColorRect
		{
			Color = CreamGold,
			CustomMinimumSize = new Vector2(4f, 28f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		colorRect.Set("layout_mode", 2);
		Label label = CreateTextLabel(templateLabel, labelText, 2, CreamGold);
		label.CustomMinimumSize = new Vector2(522f, 36f);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.VerticalAlignment = VerticalAlignment.Center;
		hBoxContainer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		if (resetAction != null)
		{
			hBoxContainer.AddChild(CreateResetButton(templateLabel, resetAction, text, actionText), forceReadableName: false, Node.InternalMode.Disabled);
		}
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		marginContainer.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		vbox.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static bool TryCreateMiniHeaderSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText)
	{
		if (vbox.GetNodeOrNull(new NodePath(sectionName)) != null)
		{
			return false;
		}
		MarginContainer marginContainer = CreateSection(sectionName, 10, 2, 38f);
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(858f, 32f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panelContainer.Set("layout_mode", 2);
		panelContainer.AddThemeStyleboxOverride("panel", CreateSettingStripStyle(new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.6f), accented: false));
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(838f, 28f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 10);
		ColorRect colorRect = new ColorRect
		{
			Color = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.42f),
			CustomMinimumSize = new Vector2(3f, 20f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		colorRect.Set("layout_mode", 2);
		Label label = CreateTextLabel(templateLabel, labelText, 0, CreamGold);
		label.CustomMinimumSize = new Vector2(812f, 28f);
		label.VerticalAlignment = VerticalAlignment.Center;
		hBoxContainer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		marginContainer.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		vbox.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static bool TryCreateSubHeaderSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText)
	{
		if (vbox.GetNodeOrNull(new NodePath(sectionName)) != null)
		{
			return false;
		}
		MarginContainer marginContainer = CreateSection(sectionName, 18, 8, 62f);
		VBoxContainer vBoxContainer = CreateStack();
		vBoxContainer.AddThemeConstantOverride("separation", 6);
		vBoxContainer.AddChild(CreateSubHeaderDivider(sectionName + "Divider"), forceReadableName: false, Node.InternalMode.Disabled);
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(858f, 38f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panelContainer.Set("layout_mode", 2);
		panelContainer.AddThemeStyleboxOverride("panel", CreateSettingStripStyle(SubHeaderBg, accented: true));
		HBoxContainer hBoxContainer = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(838f, 30f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 10);
		ColorRect colorRect = new ColorRect
		{
			Color = CreamGold,
			CustomMinimumSize = new Vector2(4f, 24f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		colorRect.Set("layout_mode", 2);
		Label label = CreateTextLabel(templateLabel, labelText, 2, CreamGold);
		label.CustomMinimumSize = new Vector2(812f, 30f);
		label.VerticalAlignment = VerticalAlignment.Center;
		hBoxContainer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		marginContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		vbox.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static Button CreateResetButton(RichTextLabel templateLabel, Action resetAction, ISkinSettingsUiText text, string? buttonText = null)
	{
		Button button = CreateHeaderActionButton(templateLabel, buttonText ?? text.ResetDefaults, 124f);
		button.Pressed += resetAction;
		return button;
	}

	private static Button CreateHeaderActionButton(RichTextLabel templateLabel, string text, float width)
	{
		CznSettingsButton cznSettingsButton = new CznSettingsButton();
		cznSettingsButton.Text = text;
		cznSettingsButton.CustomMinimumSize = new Vector2(width, 34f);
		cznSettingsButton.FocusMode = Control.FocusModeEnum.All;
		cznSettingsButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		cznSettingsButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		cznSettingsButton.Set("layout_mode", 2);
		cznSettingsButton.AddThemeFontOverride("font", templateLabel.GetThemeFont("normal_font"));
		cznSettingsButton.AddThemeFontSizeOverride("font_size", SettingsFontSize(templateLabel, -1));
		cznSettingsButton.AddThemeColorOverride("font_color", CreamGold);
		cznSettingsButton.AddThemeColorOverride("font_hover_color", TextColor);
		cznSettingsButton.AddThemeColorOverride("font_pressed_color", TextColor);
		cznSettingsButton.AddThemeStyleboxOverride("normal", CreateHeaderButtonStyle(new Color(0.12f, 0.19f, 0.23f, 0.92f)));
		cznSettingsButton.AddThemeStyleboxOverride("hover", CreateHeaderButtonStyle(new Color(0.18f, 0.29f, 0.34f, 0.96f)));
		cznSettingsButton.AddThemeStyleboxOverride("pressed", CreateHeaderButtonStyle(new Color(0.09f, 0.15f, 0.18f)));
		cznSettingsButton.AddThemeStyleboxOverride("focus", CreateHeaderButtonStyle(new Color(0.18f, 0.29f, 0.34f, 0.96f)));
		return CznSettingsInputControls.AttachNativeOwner(cznSettingsButton);
	}

	private static bool TryCreateSliderSection(VBoxContainer vbox, RichTextLabel templateLabel, PackedScene sliderScene, string sectionName, string labelText, string descriptionText, string sliderRootName, double min, double max, double step, double current, string suffix, double displayValueOffset, Action<double> onChanged, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		Control sliderRoot = null;
		bool flag = TryCreateControlSection(vbox, templateLabel, sectionName, labelText, descriptionText, (float rowHeight) =>
		{
			sliderRoot = sliderScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
			sliderRoot.Name = sliderRootName;
			sliderRoot.Set("layout_mode", 2);
			sliderRoot.CustomMinimumSize = new Vector2(324f, rowHeight);
			sliderRoot.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			return sliderRoot;
		}, enabled, rowRole);
		if (flag && sliderRoot != null)
		{
			GodotScheduler.Deferred(() =>
			{
				WireSliderWhenReady(sliderRoot, min, max, step, current, suffix, displayValueOffset, onChanged, enabled, 8);
			});
		}
		return flag;
	}

	private static bool TryCreateToggleSection(VBoxContainer vbox, RichTextLabel templateLabel, PackedScene? tickboxScene, string sectionName, string labelText, string descriptionText, bool current, Action<bool> onToggled, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		return TryCreateControlSection(vbox, templateLabel, sectionName, labelText, descriptionText, (float rowHeight) =>
		{
			Control control = CreateToggleControl(templateLabel, tickboxScene, sectionName + "Toggle", current, onToggled);
			if (!enabled && control is BaseButton baseButton)
			{
				baseButton.Disabled = true;
			}
			control.TooltipText = descriptionText;
			return WrapControlCenter(control, 64f, rowHeight);
		}, enabled, rowRole);
	}

	private static bool TryCreateActionSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText, string descriptionText, string buttonText, Action onPressed, bool enabled = true)
	{
		return TryCreateControlSection(vbox, templateLabel, sectionName, labelText, descriptionText, (float rowHeight) =>
		{
			Button button = CreateHeaderActionButton(templateLabel, buttonText, 164f);
			button.Disabled = !enabled;
			button.Pressed += onPressed;
			button.TooltipText = descriptionText;
			return WrapControlCenter(button, 184f, rowHeight);
		}, enabled);
	}

	private static bool TryCreateDropdownSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText, string descriptionText, IReadOnlyList<(int Id, string Label)> options, int currentId, Action<int> onSelected, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		return TryCreateControlSection(vbox, templateLabel, sectionName, labelText, descriptionText, (float rowHeight) =>
		{
			OptionButton optionButton = CreateDropdownControl(templateLabel, sectionName + "Dropdown", options, currentId, onSelected);
			optionButton.Disabled = !enabled;
			optionButton.TooltipText = descriptionText;
			return WrapControlCenter(optionButton, 320f, rowHeight);
		}, enabled, rowRole);
	}

	private static bool TryCreateControlSection(VBoxContainer vbox, RichTextLabel templateLabel, string sectionName, string labelText, string descriptionText, Func<float, Control> createControl, bool enabled = true, SettingsControlRowRole rowRole = SettingsControlRowRole.Standalone)
	{
		if (vbox.GetNodeOrNull(new NodePath(sectionName)) != null)
		{
			return false;
		}
		bool flag = !string.IsNullOrWhiteSpace(descriptionText);
		float num = (flag ? 74f : 56f);
		MarginContainer marginContainer = CreateSection(sectionName, 0, 0, num + 1f);
		float num2 = ((rowRole == SettingsControlRowRole.Standalone) ? 858f : 834f);
		VBoxContainer vBoxContainer = CreateStack((rowRole == SettingsControlRowRole.Standalone) ? 882f : num2);
		vBoxContainer.AddChild(CreateRowSeparator(sectionName + "Divider", num2), forceReadableName: false, Node.InternalMode.Disabled);
		HBoxContainer hBoxContainer = CreateRow(num, num2);
		VBoxContainer node = CreateTextStack(templateLabel, labelText, descriptionText, num);
		Control control = createControl(num);
		if (!enabled)
		{
			hBoxContainer.Modulate = DisabledRowModulate;
		}
		if (flag)
		{
			hBoxContainer.TooltipText = descriptionText;
			control.TooltipText = descriptionText;
		}
		hBoxContainer.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(CreateSpacer(), forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		marginContainer.AddChild(WrapControlHierarchy(vBoxContainer, rowRole, num), forceReadableName: false, Node.InternalMode.Disabled);
		vbox.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	private static Control WrapControlHierarchy(VBoxContainer stack, SettingsControlRowRole rowRole, float rowHeight)
	{
		if (rowRole == SettingsControlRowRole.Standalone)
		{
			return stack;
		}
		stack.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (rowRole == SettingsControlRowRole.Parent)
		{
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.CustomMinimumSize = new Vector2(858f, rowHeight + 1f);
			panelContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
			panelContainer.Set("layout_mode", 2);
			panelContainer.AddThemeStyleboxOverride("panel", CreateSettingStripStyle(new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.46f), accented: true));
			panelContainer.AddChild(stack, forceReadableName: false, Node.InternalMode.Disabled);
			return panelContainer;
		}
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(858f, rowHeight + 1f);
		hBoxContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 14);
		hBoxContainer.AddChild(new ColorRect
		{
			Name = "CznModSettingsChildGuide",
			Color = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.38f),
			CustomMinimumSize = new Vector2(3f, rowHeight + 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		}, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(stack, forceReadableName: false, Node.InternalMode.Disabled);
		return hBoxContainer;
	}

	private static OptionButton CreateDropdownControl(RichTextLabel templateLabel, string name, IReadOnlyList<(int Id, string Label)> options, int currentId, Action<int> onSelected)
	{
		OptionButton dropdown = new CznSettingsOptionButton
		{
			Name = name,
			CustomMinimumSize = new Vector2(320f, 44f),
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};
		dropdown.Set("layout_mode", 2);
		dropdown.AddThemeFontOverride("font", templateLabel.GetThemeFont("normal_font"));
		dropdown.AddThemeFontSizeOverride("font_size", SettingsFontSize(templateLabel, -1));
		int idx = 0;
		for (int i = 0; i < options.Count; i++)
		{
			var (num, label) = options[i];
			dropdown.AddItem(label, num);
			if (num == currentId)
			{
				idx = i;
			}
		}
		dropdown.Select(idx);
		dropdown.ItemSelected += (long index) =>
		{
			int itemId = dropdown.GetItemId((int)index);
			onSelected(itemId);
		};
		return CznSettingsInputControls.AttachNativeOwner(dropdown);
	}

	private static MarginContainer CreateSection(string name, int top, int bottom, float minHeight, int horizontalMargin = 12)
	{
		MarginContainer marginContainer = new MarginContainer();
		marginContainer.Name = name;
		marginContainer.CustomMinimumSize = new Vector2(882f, minHeight);
		marginContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		marginContainer.Set("layout_mode", 2);
		marginContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		marginContainer.AddThemeConstantOverride("margin_left", horizontalMargin);
		marginContainer.AddThemeConstantOverride("margin_top", top);
		marginContainer.AddThemeConstantOverride("margin_right", horizontalMargin);
		marginContainer.AddThemeConstantOverride("margin_bottom", bottom);
		return marginContainer;
	}

	private static VBoxContainer CreateStack(float width = 882f)
	{
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.CustomMinimumSize = new Vector2(width, 0f);
		vBoxContainer.Set("layout_mode", 2);
		vBoxContainer.AddThemeConstantOverride("separation", 0);
		return vBoxContainer;
	}

	private static HBoxContainer CreateRow(float height, float width = 858f)
	{
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.CustomMinimumSize = new Vector2(width, height);
		hBoxContainer.Set("layout_mode", 2);
		hBoxContainer.AddThemeConstantOverride("separation", 18);
		return hBoxContainer;
	}

	private static VBoxContainer CreateTextStack(RichTextLabel templateLabel, string labelText, string descriptionText, float rowHeight)
	{
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(330f, rowHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vBoxContainer.Set("layout_mode", 2);
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill;
		vBoxContainer.AddThemeConstantOverride("separation", 1);
		if (string.IsNullOrWhiteSpace(descriptionText))
		{
			Label label = CreateTextLabel(templateLabel, labelText, 2, TextColor);
			label.CustomMinimumSize = new Vector2(330f, rowHeight);
			label.VerticalAlignment = VerticalAlignment.Center;
			vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
			return vBoxContainer;
		}
		Label label2 = CreateTextLabel(templateLabel, labelText, 2, TextColor);
		label2.CustomMinimumSize = new Vector2(330f, 32f);
		label2.VerticalAlignment = VerticalAlignment.Bottom;
		vBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		Label label3 = CreateTextLabel(templateLabel, descriptionText, -2, DimText);
		label3.CustomMinimumSize = new Vector2(330f, Math.Max(30f, rowHeight - 34f));
		label3.VerticalAlignment = VerticalAlignment.Top;
		label3.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(label3, forceReadableName: false, Node.InternalMode.Disabled);
		return vBoxContainer;
	}

	private static Control CreateToggleControl(RichTextLabel templateLabel, PackedScene? tickboxScene, string name, bool current, Action<bool> onToggled)
	{
		if (tickboxScene != null)
		{
			try
			{
				CznSettingsButton cznSettingsButton = new CznSettingsButton();
				cznSettingsButton.Name = name;
				cznSettingsButton.ToggleMode = true;
				cznSettingsButton.ButtonPressed = current;
				cznSettingsButton.Text = string.Empty;
				cznSettingsButton.Flat = true;
				cznSettingsButton.FocusMode = Control.FocusModeEnum.All;
				cznSettingsButton.CustomMinimumSize = new Vector2(64f, 64f);
				cznSettingsButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
				cznSettingsButton.Set("layout_mode", 2);
				AddTransparentButtonStyles(cznSettingsButton);
				Control visual = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
				visual.Name = "TickboxVisual";
				visual.CustomMinimumSize = new Vector2(64f, 64f);
				visual.MouseFilter = Control.MouseFilterEnum.Ignore;
				UpdateTickboxVisualState(visual, current);
				CenterContainer centerContainer = new CenterContainer
				{
					CustomMinimumSize = new Vector2(64f, 64f),
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				centerContainer.Set("layout_mode", 2);
				centerContainer.AddChild(visual, forceReadableName: false, Node.InternalMode.Disabled);
				cznSettingsButton.AddChild(centerContainer, forceReadableName: false, Node.InternalMode.Disabled);
				cznSettingsButton.Toggled += (bool pressed) =>
				{
					UpdateTickboxVisualState(visual, pressed);
					onToggled(pressed);
				};
				return CznSettingsInputControls.AttachNativeOwner(cznSettingsButton);
			}
			catch (Exception ex)
			{
				SkinLog.Warn("Settings tickbox visual unavailable: " + SkinLog.FormatException(ex));
			}
		}
		CznSettingsCheckButton cznSettingsCheckButton = new CznSettingsCheckButton();
		cznSettingsCheckButton.Name = name;
		cznSettingsCheckButton.ButtonPressed = current;
		cznSettingsCheckButton.Text = string.Empty;
		cznSettingsCheckButton.CustomMinimumSize = new Vector2(58f, 44f);
		cznSettingsCheckButton.FocusMode = Control.FocusModeEnum.All;
		cznSettingsCheckButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		cznSettingsCheckButton.AddThemeFontOverride("font", templateLabel.GetThemeFont("normal_font"));
		cznSettingsCheckButton.AddThemeFontSizeOverride("font_size", SettingsFontSize(templateLabel, 0));
		cznSettingsCheckButton.Toggled += (bool pressed) =>
		{
			onToggled(pressed);
		};
		return CznSettingsInputControls.AttachNativeOwner(cznSettingsCheckButton);
	}

	private static CenterContainer WrapControlCenter(Control control, float width, float height)
	{
		CenterContainer centerContainer = new CenterContainer();
		centerContainer.CustomMinimumSize = new Vector2(width, height);
		centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		centerContainer.Set("layout_mode", 2);
		centerContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		centerContainer.AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
		return centerContainer;
	}

	private static Control CreateSpacer()
	{
		Control control = new Control();
		control.MouseFilter = Control.MouseFilterEnum.Ignore;
		control.Set("layout_mode", 2);
		control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return control;
	}

	private static HSeparator CreateRowSeparator(string name, float width = 858f)
	{
		HSeparator hSeparator = new HSeparator();
		hSeparator.Name = name;
		hSeparator.CustomMinimumSize = new Vector2(width, 1f);
		hSeparator.MouseFilter = Control.MouseFilterEnum.Ignore;
		hSeparator.Set("layout_mode", 2);
		hSeparator.AddThemeConstantOverride("separation", 1);
		hSeparator.AddThemeStyleboxOverride(stylebox: new StyleBoxFlat
		{
			BgColor = RowSeparatorColor,
			ContentMarginTop = 0f,
			ContentMarginBottom = 0f
		}, name: "separator");
		return hSeparator;
	}

	private static ColorRect CreateSubHeaderDivider(string name)
	{
		ColorRect colorRect = new ColorRect();
		colorRect.Name = name;
		colorRect.Color = SubHeaderLineColor;
		colorRect.CustomMinimumSize = new Vector2(858f, 2f);
		colorRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		colorRect.Set("layout_mode", 2);
		return colorRect;
	}

	private static Label CreateTextLabel(RichTextLabel templateLabel, string text, int fontDelta, Color color)
	{
		Label label = new Label();
		label.Text = text;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.Theme = templateLabel.Theme;
		label.Set("layout_mode", 2);
		label.AddThemeFontOverride("font", templateLabel.GetThemeFont("normal_font"));
		label.AddThemeFontSizeOverride("font_size", SettingsFontSize(templateLabel, fontDelta));
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static int SettingsFontSize(RichTextLabel templateLabel, int fontDelta)
	{
		return Math.Clamp(templateLabel.GetThemeFontSize("normal_font_size") + fontDelta, 14, 28);
	}

	private static void WireSliderWhenReady(Control sliderRoot, double min, double max, double step, double current, string suffix, double displayValueOffset, Action<double> onChanged, bool enabled, int retries)
	{
		if (!GodotNodeUtil.IsAlive(sliderRoot))
		{
			return;
		}
		NSlider nodeOrNull = sliderRoot.GetNodeOrNull<NSlider>(new NodePath("Slider"));
		MegaLabel valueLabel = sliderRoot.GetNodeOrNull<MegaLabel>(new NodePath("SliderValue"));
		if (nodeOrNull == null || valueLabel == null)
		{
			if (retries > 0)
			{
				GodotScheduler.Deferred(() =>
				{
					WireSliderWhenReady(sliderRoot, min, max, step, current, suffix, displayValueOffset, onChanged, enabled, retries - 1);
				});
			}
			return;
		}
		nodeOrNull.MouseFilter = (Control.MouseFilterEnum)(enabled ? 0 : 2);
		nodeOrNull.FocusMode = (Control.FocusModeEnum)(enabled ? 2 : 0);
		nodeOrNull.MinValue = min;
		nodeOrNull.MaxValue = max;
		nodeOrNull.Step = step;
		nodeOrNull.SetValueWithoutAnimation(current);
		valueLabel.SetTextAutoSize(FormatSliderValue(current + displayValueOffset, suffix));
		if (!nodeOrNull.HasMeta("czn_mod_slider_bound"))
		{
			nodeOrNull.Connect(global::Godot.Range.SignalName.ValueChanged, Callable.From((double value) =>
			{
				double num = Math.Round(value / step) * step + displayValueOffset;
				valueLabel.SetTextAutoSize(FormatSliderValue(num, suffix));
				onChanged(num);
			}));
			nodeOrNull.SetMeta("czn_mod_slider_bound", true);
		}
	}

	private static SettingsUiShell EnsureSettingsShell(NSettingsPanel panel, VBoxContainer panelVBox)
	{
		HBoxContainer hBoxContainer = panelVBox.GetNodeOrNull<HBoxContainer>(new NodePath("CznModSettingsShell"));
		if (hBoxContainer != null && !IsCurrentSettingsShell(hBoxContainer))
		{
			panelVBox.RemoveChild(hBoxContainer);
			hBoxContainer.QueueFree();
			hBoxContainer = null;
		}
		if (hBoxContainer == null)
		{
			GodotNodeUtil.ClearChildren(panelVBox);
			panelVBox.CustomMinimumSize = new Vector2(1012f, 0f);
			panelVBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			panelVBox.AddThemeConstantOverride("separation", 0);
			hBoxContainer = new HBoxContainer
			{
				Name = "CznModSettingsShell",
				CustomMinimumSize = new Vector2(1012f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			hBoxContainer.Set("layout_mode", 2);
			hBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			hBoxContainer.AddThemeConstantOverride("separation", 12);
			VBoxContainer node = CreateSettingsSidebar();
			VBoxContainer node2 = CreateSettingsContentHost();
			hBoxContainer.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
			hBoxContainer.AddChild(node2, forceReadableName: false, Node.InternalMode.Disabled);
			panelVBox.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		}
		hBoxContainer.SetMeta("czn_mod_settings_shell_version", 7);
		VBoxContainer vBoxContainer = hBoxContainer.GetNodeOrNull<VBoxContainer>(new NodePath("CznModSettingsSidebar"));
		VBoxContainer vBoxContainer2 = hBoxContainer.GetNodeOrNull<VBoxContainer>(new NodePath("CznModSettingsContentHost"));
		if (vBoxContainer == null)
		{
			vBoxContainer = CreateSettingsSidebar();
			hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
			hBoxContainer.MoveChild(vBoxContainer, 0);
		}
		if (vBoxContainer2 == null)
		{
			vBoxContainer2 = CreateSettingsContentHost();
			hBoxContainer.AddChild(vBoxContainer2, forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (!panel.HasMeta("czn_mod_settings_selected_page"))
		{
			panel.SetMeta("czn_mod_settings_selected_page", "global");
		}
		if (!panel.HasMeta("czn_mod_settings_selected_global_category"))
		{
			panel.SetMeta("czn_mod_settings_selected_global_category", "general");
		}
		return new SettingsUiShell(panelVBox, hBoxContainer, vBoxContainer, vBoxContainer2);
	}

	private static bool IsCurrentSettingsShell(HBoxContainer shell)
	{
		if (!shell.HasMeta("czn_mod_settings_shell_version"))
		{
			return false;
		}
		Variant meta = shell.GetMeta("czn_mod_settings_shell_version");
		if (meta.VariantType == Variant.Type.Int)
		{
			return meta.AsInt32() >= 7;
		}
		return false;
	}

	private static VBoxContainer CreateSettingsSidebar()
	{
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.Name = "CznModSettingsSidebar";
		vBoxContainer.CustomMinimumSize = new Vector2(118f, 0f);
		vBoxContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		vBoxContainer.Set("layout_mode", 2);
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		vBoxContainer.AddThemeConstantOverride("separation", 10);
		return vBoxContainer;
	}

	private static VBoxContainer CreateSettingsContentHost()
	{
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.Name = "CznModSettingsContentHost";
		vBoxContainer.CustomMinimumSize = new Vector2(882f, 0f);
		vBoxContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		vBoxContainer.Set("layout_mode", 2);
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		vBoxContainer.AddThemeConstantOverride("separation", 0);
		return vBoxContainer;
	}

	private static VBoxContainer EnsureSettingsPage(VBoxContainer contentHost, string pageName, string pageId)
	{
		VBoxContainer nodeOrNull = contentHost.GetNodeOrNull<VBoxContainer>(new NodePath(pageName));
		if (nodeOrNull != null)
		{
			return nodeOrNull;
		}
		nodeOrNull = new VBoxContainer
		{
			Name = pageName,
			CustomMinimumSize = new Vector2(882f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		nodeOrNull.Set("layout_mode", 2);
		nodeOrNull.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		nodeOrNull.AddThemeConstantOverride("separation", 0);
		nodeOrNull.SetMeta("czn_mod_settings_page_id", pageId);
		contentHost.AddChild(nodeOrNull, forceReadableName: false, Node.InternalMode.Disabled);
		return nodeOrNull;
	}

	private static bool EnsureGlobalNavigationButton(NSettingsPanel panel, VBoxContainer sidebar, RichTextLabel templateLabel, string globalSettingsIconPath, ISkinSettingsUiText text)
	{
		if (sidebar.GetNodeOrNull(new NodePath("CznModSettingsNav_Global")) != null)
		{
			return false;
		}
		Button button = CreateSettingsNavButton(templateLabel, "CznModSettingsNav_Global", "global", BuildNavigationTooltip(text.GlobalSettingsHeader, text.GlobalSettingsNavDescription), "ALL", globalSettingsIconPath, text.GlobalSettingsNavLabel, preferRawImage: true);
		button.Pressed += () =>
		{
			SetSelectedPage(panel, "global");
		};
		sidebar.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		sidebar.MoveChild(button, 0);
		return true;
	}

	private static Button CreateSettingsNavButton(RichTextLabel templateLabel, string name, string pageId, string tooltip, string fallbackText, string avatarPath, string displayText, bool preferRawImage = false)
	{
		CznSettingsButton cznSettingsButton = new CznSettingsButton();
		cznSettingsButton.Name = name;
		cznSettingsButton.ToggleMode = false;
		cznSettingsButton.Text = string.Empty;
		cznSettingsButton.TooltipText = tooltip;
		cznSettingsButton.FocusMode = Control.FocusModeEnum.All;
		cznSettingsButton.CustomMinimumSize = new Vector2(96f, 96f);
		cznSettingsButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		cznSettingsButton.Set("layout_mode", 2);
		cznSettingsButton.SetMeta("czn_mod_settings_page_id", pageId);
		ApplyNavButtonStyle(cznSettingsButton, selected: false);
		CenterContainer centerContainer = new CenterContainer
		{
			CustomMinimumSize = new Vector2(96f, 96f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		centerContainer.Set("layout_mode", 2);
		Control icon = CreateNavIcon(templateLabel, fallbackText, avatarPath, preferRawImage);
		VBoxContainer vBoxContainer = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(78f, 78f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		vBoxContainer.Set("layout_mode", 2);
		vBoxContainer.AddThemeConstantOverride("separation", 4);
		vBoxContainer.AddChild(CreateNavIconHost(icon), forceReadableName: false, Node.InternalMode.Disabled);
		if (ShouldShowNavLabel(fallbackText, avatarPath, displayText))
		{
			Label label = CreateTextLabel(templateLabel, displayText, -4, CreamGold);
			label.CustomMinimumSize = new Vector2(74f, 22f);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		}
		centerContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		cznSettingsButton.AddChild(centerContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return CznSettingsInputControls.AttachNativeOwner(cznSettingsButton);
	}

	private static CenterContainer CreateNavIconHost(Control icon)
	{
		CenterContainer centerContainer = new CenterContainer();
		centerContainer.CustomMinimumSize = new Vector2(74f, 48f);
		centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		centerContainer.Set("layout_mode", 2);
		centerContainer.AddChild(icon, forceReadableName: false, Node.InternalMode.Disabled);
		return centerContainer;
	}

	private static bool ShouldShowNavLabel(string fallbackText, string avatarPath, string displayText)
	{
		if (string.IsNullOrWhiteSpace(displayText))
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(avatarPath))
		{
			return !string.Equals(fallbackText, displayText, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static Control CreateNavIcon(RichTextLabel templateLabel, string fallbackText, string avatarPath, bool preferRawImage)
	{
		Texture2D texture2D = LoadNavTexture(avatarPath, preferRawImage);
		if (texture2D != null)
		{
			TextureRect textureRect = new TextureRect();
			textureRect.Texture = texture2D;
			textureRect.CustomMinimumSize = new Vector2(48f, 48f);
			textureRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			textureRect.Set("layout_mode", 2);
			return textureRect;
		}
		Label label = CreateTextLabel(templateLabel, fallbackText, 2, CreamGold);
		label.CustomMinimumSize = new Vector2(48f, 48f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		return label;
	}

	private static Texture2D? LoadNavTexture(string texturePath, bool preferRawImage)
	{
		if (string.IsNullOrWhiteSpace(texturePath))
		{
			return null;
		}
		if (preferRawImage)
		{
			Texture2D texture2D = LoadRawNavTexture(texturePath);
			if (texture2D != null)
			{
				return texture2D;
			}
		}
		try
		{
			Texture2D texture2D2 = ResourceLoader.Load<Texture2D>(texturePath, null, ResourceLoader.CacheMode.Reuse);
			if (texture2D2 != null)
			{
				return texture2D2;
			}
		}
		catch
		{
		}
		if (!preferRawImage)
		{
			return LoadRawNavTexture(texturePath);
		}
		return null;
	}

	private static Texture2D? LoadRawNavTexture(string texturePath)
	{
		try
		{
			if (!global::Godot.FileAccess.FileExists(texturePath))
			{
				return null;
			}
			Image image = Image.LoadFromFile(texturePath);
			return (image == null || image.IsEmpty()) ? null : ImageTexture.CreateFromImage(image);
		}
		catch
		{
			return null;
		}
	}

	private static string BuildNavigationTooltip(string title, string description)
	{
		if (!string.IsNullOrWhiteSpace(description))
		{
			return title + "\n" + description;
		}
		return title;
	}

	private static string BuildNavInitials(string text)
	{
		string text2 = text.Trim();
		if (!string.IsNullOrEmpty(text2))
		{
			return text2.Substring(0, 1).ToUpperInvariant();
		}
		return "?";
	}

	private static void SetSelectedPage(NSettingsPanel panel, string pageId)
	{
		panel.SetMeta("czn_mod_settings_selected_page", pageId);
		RefreshSettingsShellSelection(panel);
		RefreshGlobalCategorySelection(panel);
		RefreshPanelLayout(panel);
		RefreshFocusNeighbors(panel);
	}

	private static void SetSelectedGlobalCategory(NSettingsPanel panel, string categoryId)
	{
		panel.SetMeta("czn_mod_settings_selected_global_category", categoryId);
		RefreshGlobalCategorySelection(panel);
		RefreshPanelLayout(panel);
		RefreshFocusNeighbors(panel);
	}

	private static string GetSelectedPage(NSettingsPanel panel)
	{
		if (!panel.HasMeta("czn_mod_settings_selected_page"))
		{
			return "global";
		}
		string text = panel.GetMeta("czn_mod_settings_selected_page").ToString();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "global";
	}

	private static void RefreshSettingsShellSelection(NSettingsPanel panel)
	{
		if (!TryGetSettingsShell(panel, out SettingsUiShell shell))
		{
			return;
		}
		string text = GetSelectedPage(panel);
		bool flag = false;
		foreach (Node child in shell.ContentHost.GetChildren())
		{
			if (child is Control control && child.HasMeta("czn_mod_settings_page_id"))
			{
				bool flag2 = (control.Visible = string.Equals(child.GetMeta("czn_mod_settings_page_id").ToString(), text, StringComparison.Ordinal));
				flag |= flag2;
			}
		}
		if (!flag)
		{
			text = "global";
			panel.SetMeta("czn_mod_settings_selected_page", text);
			foreach (Node child2 in shell.ContentHost.GetChildren())
			{
				if (child2 is Control control2 && child2.HasMeta("czn_mod_settings_page_id"))
				{
					control2.Visible = string.Equals(child2.GetMeta("czn_mod_settings_page_id").ToString(), text, StringComparison.Ordinal);
				}
			}
		}
		foreach (Node child3 in shell.Sidebar.GetChildren())
		{
			if (child3 is Button button && child3.HasMeta("czn_mod_settings_page_id"))
			{
				bool selected = (button.ButtonPressed = string.Equals(child3.GetMeta("czn_mod_settings_page_id").ToString(), text, StringComparison.Ordinal));
				ApplyNavButtonStyle(button, selected);
			}
		}
	}

	private static void RefreshGlobalCategorySelection(NSettingsPanel panel)
	{
		if (!TryGetSettingsShell(panel, out SettingsUiShell shell))
		{
			return;
		}
		string selectedCategory = ResolveSelectedGlobalCategory(panel, shell);
		foreach (Node child in shell.ContentHost.GetChildren())
		{
			if (child is Control control && control.HasMeta("czn_mod_settings_page_id") && string.Equals(control.GetMeta("czn_mod_settings_page_id").ToString(), "global", StringComparison.Ordinal))
			{
				RefreshGlobalCategoryNode(control, selectedCategory);
			}
		}
	}

	private static void RefreshGlobalCategoryNode(Node node, string selectedCategory)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Button button && child.HasMeta("czn_mod_settings_global_category_id"))
			{
				bool flag = (button.ButtonPressed = string.Equals(child.GetMeta("czn_mod_settings_global_category_id").ToString(), selectedCategory, StringComparison.Ordinal));
				button.SetMeta("czn_mod_settings_focus_preferred", flag);
				ApplyCategoryButtonStyle(button, flag);
			}
			if (child is Control control && child.HasMeta("czn_mod_settings_global_category"))
			{
				control.Visible = string.Equals(child.GetMeta("czn_mod_settings_global_category").ToString(), selectedCategory, StringComparison.Ordinal);
			}
			RefreshGlobalCategoryNode(child, selectedCategory);
		}
	}

	private static string ResolveSelectedGlobalCategory(NSettingsPanel panel, SettingsUiShell shell)
	{
		string selectedGlobalCategory = GetSelectedGlobalCategory(panel);
		List<string> list = new List<string>();
		foreach (Node child in shell.ContentHost.GetChildren())
		{
			if (child is Control control && control.HasMeta("czn_mod_settings_page_id") && string.Equals(control.GetMeta("czn_mod_settings_page_id").ToString(), "global", StringComparison.Ordinal))
			{
				CollectGlobalCategoryIds(control, list);
			}
		}
		if (list.Count == 0 || list.Contains(selectedGlobalCategory))
		{
			return selectedGlobalCategory;
		}
		panel.SetMeta("czn_mod_settings_selected_global_category", list[0]);
		return list[0];
	}

	private static void CollectGlobalCategoryIds(Node node, List<string> ids)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Button && child.HasMeta("czn_mod_settings_global_category_id"))
			{
				string item = child.GetMeta("czn_mod_settings_global_category_id").ToString();
				if (!ids.Contains(item))
				{
					ids.Add(item);
				}
			}
			CollectGlobalCategoryIds(child, ids);
		}
	}

	private static string GetSelectedGlobalCategory(NSettingsPanel panel)
	{
		if (!panel.HasMeta("czn_mod_settings_selected_global_category"))
		{
			panel.SetMeta("czn_mod_settings_selected_global_category", "general");
			return "general";
		}
		string text = panel.GetMeta("czn_mod_settings_selected_global_category").ToString();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "general";
	}

	private static bool TryGetSettingsShell(NSettingsPanel panel, out SettingsUiShell shell)
	{
		shell = null;
		VBoxContainer nodeOrNull = panel.GetNodeOrNull<VBoxContainer>(new NodePath("VBoxContainer"));
		HBoxContainer hBoxContainer = nodeOrNull?.GetNodeOrNull<HBoxContainer>(new NodePath("CznModSettingsShell"));
		VBoxContainer vBoxContainer = hBoxContainer?.GetNodeOrNull<VBoxContainer>(new NodePath("CznModSettingsSidebar"));
		VBoxContainer vBoxContainer2 = hBoxContainer?.GetNodeOrNull<VBoxContainer>(new NodePath("CznModSettingsContentHost"));
		if (nodeOrNull == null || hBoxContainer == null || vBoxContainer == null || vBoxContainer2 == null)
		{
			return false;
		}
		shell = new SettingsUiShell(nodeOrNull, hBoxContainer, vBoxContainer, vBoxContainer2);
		return true;
	}

	private static StyleBoxFlat CreateTransparentStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0f),
			ContentMarginLeft = 0f,
			ContentMarginRight = 0f,
			ContentMarginTop = 0f,
			ContentMarginBottom = 0f
		};
	}

	private static void ApplyNavButtonStyle(Button button, bool selected)
	{
		button.AddThemeStyleboxOverride("normal", CreateNavButtonStyle(selected ? NavSelectedBg : NavIdleBg, selected));
		button.AddThemeStyleboxOverride("hover", CreateNavButtonStyle(selected ? NavSelectedBg : NavHoverBg, accented: true));
		button.AddThemeStyleboxOverride("pressed", CreateNavButtonStyle(NavSelectedBg, accented: true));
		button.AddThemeStyleboxOverride("focus", CreateNavButtonStyle(NavHoverBg, accented: true));
		button.AddThemeStyleboxOverride("disabled", CreateNavButtonStyle(NavIdleBg, accented: false));
	}

	private static void ApplyCategoryButtonStyle(Button button, bool selected)
	{
		Color bgColor = new Color(ModHeaderBg.R, ModHeaderBg.G, ModHeaderBg.B, 0.92f);
		Color bgColor2 = new Color(ModHeaderBg.R, ModHeaderBg.G, ModHeaderBg.B, 0.96f);
		Color bgColor3 = new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.22f);
		Color bgColor4 = new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.34f);
		Color bgColor5 = new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.3f);
		Color borderColor = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.92f);
		Color borderColor2 = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.04f);
		Color borderColor3 = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.16f);
		Color borderColor4 = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.18f);
		button.AddThemeColorOverride("font_color", selected ? CreamGold : DimText);
		button.AddThemeColorOverride("font_hover_color", selected ? CreamGold : TextColor);
		button.AddThemeColorOverride("font_pressed_color", CreamGold);
		button.AddThemeColorOverride("font_focus_color", selected ? CreamGold : TextColor);
		button.AddThemeColorOverride("font_disabled_color", DimText);
		button.AddThemeStyleboxOverride("normal", selected ? CreateCategoryButtonStyle(bgColor, borderColor, 4, 1, 1, 3) : CreateCategoryButtonStyle(bgColor3, borderColor2, 0, 0, 0, 1));
		button.AddThemeStyleboxOverride("hover", selected ? CreateCategoryButtonStyle(bgColor2, borderColor, 4, 1, 1, 3) : CreateCategoryButtonStyle(bgColor4, borderColor3, 0, 0, 0, 1));
		button.AddThemeStyleboxOverride("pressed", CreateCategoryButtonStyle(bgColor, borderColor, 4, 1, 1, 3));
		button.AddThemeStyleboxOverride("focus", selected ? CreateCategoryButtonStyle(bgColor, borderColor, 4, 1, 1, 3) : CreateCategoryButtonStyle(bgColor5, borderColor4, 0, 0, 0, 1));
		button.AddThemeStyleboxOverride("disabled", CreateCategoryButtonStyle(new Color(SubHeaderBg.R, SubHeaderBg.G, SubHeaderBg.B, 0.5f), borderColor2, 0, 0, 0, 1));
	}

	private static StyleBoxFlat CreateNavButtonStyle(Color bgColor, bool accented)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = (accented ? CreamGold : new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.24f)),
			BorderWidthLeft = ((!accented) ? 1 : 2),
			BorderWidthTop = ((!accented) ? 1 : 2),
			BorderWidthRight = ((!accented) ? 1 : 2),
			BorderWidthBottom = ((!accented) ? 1 : 2),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 8f,
			ContentMarginRight = 8f,
			ContentMarginTop = 8f,
			ContentMarginBottom = 8f
		};
	}

	private static StyleBoxFlat CreateCategoryButtonStyle(Color bgColor, Color borderColor, int borderWidthLeft, int borderWidthTop, int borderWidthRight, int borderWidthBottom)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = borderColor,
			BorderWidthLeft = borderWidthLeft,
			BorderWidthTop = borderWidthTop,
			BorderWidthRight = borderWidthRight,
			BorderWidthBottom = borderWidthBottom,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			ContentMarginLeft = 8f,
			ContentMarginRight = 8f,
			ContentMarginTop = 2f,
			ContentMarginBottom = 2f
		};
	}

	private static StyleBoxFlat CreateStatusHeaderStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = StatusHeaderBg,
			BorderColor = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginLeft = 12f,
			ContentMarginRight = 12f,
			ContentMarginTop = 6f,
			ContentMarginBottom = 6f
		};
	}

	private static StyleBoxFlat CreateSettingStripStyle(Color bgColor, bool accented)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = new Color(CreamGold.R, CreamGold.G, CreamGold.B, accented ? 0.28f : 0.14f),
			BorderWidthLeft = (accented ? 1 : 0),
			BorderWidthTop = 0,
			BorderWidthRight = 0,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = (accented ? 3f : 2f),
			ContentMarginBottom = (accented ? 3f : 2f)
		};
	}

	private static StyleBoxFlat CreateHeaderButtonStyle(Color bgColor)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = new Color(CreamGold.R, CreamGold.G, CreamGold.B, 0.55f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			ContentMarginLeft = 10f,
			ContentMarginRight = 10f,
			ContentMarginTop = 2f,
			ContentMarginBottom = 2f
		};
	}
}
