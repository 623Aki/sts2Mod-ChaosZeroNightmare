using System;
using System.Collections.Generic;
using System.Linq;
using CznCore.Godot;
using CznCore.Settings;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CznCore.Presentation;

internal sealed class CharacterSelectPresentationRuntime
{
	private const string LoadedSceneMeta = "czn_character_select_loaded_scene";

	private const string LoadedSceneInstanceMeta = "czn_character_select_loaded_scene_instance";

	private const string NativeHostOwnerMeta = "czn_character_select_native_host_owner";

	private const string NativeHostPositionMeta = "czn_character_select_native_host_position";

	private const string NativeHostSizeMeta = "czn_character_select_native_host_size";

	private const string NativeHostScaleMeta = "czn_character_select_native_host_scale";

	private const string NativeHostPivotMeta = "czn_character_select_native_host_pivot";

	private const string ToolbarPanelNodeNameSuffix = "CharacterSelectOptionsPanel";

	private const string ToolbarButtonsRootNodeName = "CharSelectButtons";

	private readonly CharacterSkinProfile _profile;

	private readonly SkinCharacterSettings _settings;

	private readonly CharacterSkinVoicePlayerRuntime? _voicePlayer;

	private readonly ILocalizedText? _switchBackgroundText;

	private readonly SkinSettingsUiTextProfile? _settingsText;

	private readonly Func<Node, string, bool>? _tryPrepareLoopSpine;

	private readonly Func<Player?>? _voiceOwner;

	private readonly Func<IReadOnlyList<CharacterSelectMenuSectionDescriptor>>? _additionalMenuSections;

	private CharacterSelectBackgroundVariant _backgroundVariant;

	private string ToolbarPanelNodeName => _profile.RuntimeNodeName("CharacterSelectOptionsPanel");

	public CharacterSelectPresentationRuntime(CharacterSkinProfile profile, SkinCharacterSettings settings, CharacterSkinVoicePlayerRuntime? voicePlayer = null, CharacterSelectPresentationRuntimeOptions? options = null)
	{
		_profile = profile ?? throw new ArgumentNullException("profile");
		_settings = settings ?? throw new ArgumentNullException("settings");
		_voicePlayer = voicePlayer;
		_switchBackgroundText = options?.SwitchBackgroundText;
		_settingsText = options?.SettingsText;
		_tryPrepareLoopSpine = options?.TryPrepareLoopSpine;
		_voiceOwner = options?.VoiceOwner;
		_additionalMenuSections = options?.AdditionalMenuSections;
	}

	public void ApplyScreenSelection(NCharacterSelectScreen screen, NCharacterSelectButton button, CharacterModel characterModel)
	{
		SkinPatchGuard.Run("Character select background patch", () =>
		{
			CharacterSelectPresentationProfile characterSelectPresentation = _profile.CharacterSelectPresentation;
			RemoveLegacyCharacterSelectControls(screen);
			Control nodeOrNull = screen.GetNodeOrNull<Control>(characterSelectPresentation.OriginalBgNodeName);
			Control control = screen.GetNodeOrNull<Control>(characterSelectPresentation.CustomBgNodeName);
			if (button.IsLocked || !_profile.AppliesTo(characterModel))
			{
				CharacterSelectSettingsMenu.RemoveOwned(screen, _profile.ProfileId);
				if (control != null)
				{
					SetVisible(control, visible: false);
					GodotNodeUtil.ClearChildren(control);
					RemoveLoadedSceneMeta(control);
				}
				if (nodeOrNull != null && !AnyOtherCustomBgVisible(screen, characterSelectPresentation.CustomBgNodeName))
				{
					RestoreNativeCharacterSelectHost(nodeOrNull);
					SetVisible(nodeOrNull, visible: true);
				}
			}
			else
			{
				SetCharacterSelectBackgroundVariant(characterSelectPresentation.NormalizeVariant(_settings.CharacterSelectBackgroundVariant), persist: false);
				if (control == null)
				{
					control = CreateCustomBgContainer(screen, nodeOrNull, characterSelectPresentation.CustomBgNodeName);
				}
				ApplyCharacterSelectBackgroundMode(control, nodeOrNull, characterModel, "character_select");
				EnsureCharacterSelectMenu(screen, control, nodeOrNull, characterModel);
			}
		});
	}

	public void ApplyMultiplayerLoadBackground(NMultiplayerLoadGameScreen screen)
	{
		SkinPatchGuard.Run("Multiplayer load character select background patch", () =>
		{
			if (TryGetMultiplayerLoadLocalCharacter(screen, out CharacterModel characterModel) && _profile.AppliesTo(characterModel))
			{
				if (!(AccessTools.Field(typeof(NMultiplayerLoadGameScreen), "_bgContainer")?.GetValue(screen) is Control control))
				{
					SkinLog.Warn("Multiplayer load background container missing.");
				}
				else
				{
					CharacterSelectPresentationProfile characterSelectPresentation = _profile.CharacterSelectPresentation;
					SetCharacterSelectBackgroundVariant(characterSelectPresentation.NormalizeVariant(_settings.CharacterSelectBackgroundVariant), persist: false);
					CharacterSelectBackgroundPresentationProfile backgroundOptionOrDefault = characterSelectPresentation.GetBackgroundOptionOrDefault(_backgroundVariant);
					string scenePathForVariant = GetScenePathForVariant(characterModel, backgroundOptionOrDefault);
					PrepareNativeCharacterSelectHost(control);
					Node node = LoadSceneIntoContainer(control, scenePathForVariant, "multiplayer_load");
					if (node != null && backgroundOptionOrDefault.ForceLoopAnimation)
					{
						ForceLoopAnimationRecursive(node, "multiplayer_load");
					}
				}
			}
		});
	}

	public void ApplyRandomCharacterResolved(NCharacterSelectScreen screen, CharacterModel characterModel)
	{
		SkinPatchGuard.Run("Random character select background resolution patch", () =>
		{
			if (_profile.AppliesTo(characterModel))
			{
				CharacterSelectPresentationProfile characterSelectPresentation = _profile.CharacterSelectPresentation;
				Control nodeOrNull = screen.GetNodeOrNull<Control>(characterSelectPresentation.OriginalBgNodeName);
				Control control = screen.GetNodeOrNull<Control>(characterSelectPresentation.CustomBgNodeName);
				if (control == null)
				{
					control = CreateCustomBgContainer(screen, nodeOrNull, characterSelectPresentation.CustomBgNodeName);
				}
				SetCharacterSelectBackgroundVariant(characterSelectPresentation.NormalizeVariant(_settings.CharacterSelectBackgroundVariant), persist: false);
				ApplyCharacterSelectBackgroundMode(control, nodeOrNull, characterModel, "character_select_random_resolution");
				EnsureCharacterSelectMenu(screen, control, nodeOrNull, characterModel);
			}
		});
	}

	public void PlaySelectedVoice(NCharacterSelectButton button)
	{
		SkinPatchGuard.Run("Character select voice", () =>
		{
			if (_profile.AppliesTo(button.Character))
			{
				SetCharacterSelectBackgroundVariant(_profile.CharacterSelectPresentation.NormalizeVariant(_settings.CharacterSelectBackgroundVariant), persist: false);
				TryPlayCharacterSelectVoice(_backgroundVariant, "character_selected");
			}
			else
			{
				_voicePlayer?.Stop();
			}
		});
	}

	public void ApplyCombatPresentationModeDropdown(NCharacterSelectScreen screen, NCharacterSelectButton button, CharacterModel characterModel)
	{
		SkinPatchGuard.Run("Character select combat presentation mode", () =>
		{
			RemoveLegacyCharacterSelectControls(screen);
			if (button.IsLocked || !_profile.AppliesTo(characterModel))
			{
				CharacterSelectSettingsMenu.RemoveOwned(screen, _profile.ProfileId);
			}
			else
			{
				CharacterSelectPresentationProfile characterSelectPresentation = _profile.CharacterSelectPresentation;
				Control nodeOrNull = screen.GetNodeOrNull<Control>(characterSelectPresentation.OriginalBgNodeName);
				Control control = screen.GetNodeOrNull<Control>(characterSelectPresentation.CustomBgNodeName);
				if (control == null)
				{
					control = CreateCustomBgContainer(screen, nodeOrNull, characterSelectPresentation.CustomBgNodeName);
				}
				EnsureCharacterSelectMenu(screen, control, nodeOrNull, characterModel);
			}
		});
	}

	private Control CreateCustomBgContainer(NCharacterSelectScreen screen, Control? originalBg, string nodeName)
	{
		Control control = new Control
		{
			Name = nodeName
		};
		control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		if (originalBg != null)
		{
			originalBg.GetParent().AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
			originalBg.GetParent().MoveChild(control, originalBg.GetIndex());
		}
		else
		{
			screen.AddChild(control, forceReadableName: false, Node.InternalMode.Disabled);
			screen.MoveChild(control, 0);
		}
		return control;
	}

	private void EnsureCharacterSelectMenu(NCharacterSelectScreen screen, Control customBg, Control? originalBg, CharacterModel characterModel)
	{
		CharacterSelectPresentationProfile presentation = _profile.CharacterSelectPresentation;
		List<CharacterSelectMenuItemDescriptor> items = new List<CharacterSelectMenuItemDescriptor>
		{
			new CharacterSelectMenuActionDescriptor("combat_presentation", presentation.CombatPresentationIconPath, GetCombatPresentationTitle, () => GetCombatPresentationModeStateLabel(_settings.CombatPresentationMode), GetCombatPresentationModeDescription, () =>
			{
				SetCombatPresentationModeForRuntime(NextCombatPresentationMode(_settings.CombatPresentationMode), persist: true);
			}),
			new CharacterSelectMenuActionDescriptor("character_background", presentation.CharacterBackgroundIconPath, GetCharacterBackgroundTitle, () => GetCharacterSelectBackgroundLabel(_backgroundVariant), () => _switchBackgroundText?.Current ?? "Switch Character Background", () =>
			{
				SetCharacterSelectBackgroundVariant(presentation.NextVariant(_backgroundVariant), persist: true);
				ApplyCharacterSelectBackgroundMode(customBg, originalBg, characterModel, "background_toggle");
				TryPlayCharacterSelectVoice(_backgroundVariant, "background_toggle");
			}, Accent: true),
			new CharacterSelectMenuToggleDescriptor("combat_effects", () => _profile.SettingsText.EffectsLabel.Current, () => _settingsText?.EffectsEnabledDescription.Current ?? "Enable or disable this character's combat effects.", () => _settings.EffectsEnabled, (bool enabled) =>
			{
				_settings.SetEffectsEnabled(enabled, persist: true);
			}),
			new CharacterSelectMenuToggleDescriptor("character_voice", () => _profile.SettingsText.VoiceLabel.Current, GetCharacterVoiceDescription, () => _settings.VoiceEnabled, (bool enabled) =>
			{
				_settings.SetVoiceEnabled(enabled, persist: true);
				if (!enabled)
				{
					_voicePlayer?.Stop();
				}
			})
		};
		List<CharacterSelectMenuSectionDescriptor> list = new List<CharacterSelectMenuSectionDescriptor>
		{
			new CharacterSelectMenuSectionDescriptor("character_screen", () => _settingsText?.VisualSettingsHeader.Current ?? "Character Screen", items)
		};
		IReadOnlyList<CharacterSelectMenuSectionDescriptor> readOnlyList = _additionalMenuSections?.Invoke();
		if (readOnlyList != null && readOnlyList.Count > 0)
		{
			list.AddRange(readOnlyList);
		}
		CharacterSelectSettingsMenu.Attach(screen, _profile.ProfileId, _profile.SettingsText.SettingsHeader.Current, list);
	}

	private void RemoveLegacyCharacterSelectControls(NCharacterSelectScreen screen)
	{
		string text = _profile.RuntimeNodeName("CardPlayPresentationBtn");
		string toggleButtonNodeName = _profile.CharacterSelectPresentation.ToggleButtonNodeName;
		string[] array = new string[6]
		{
			ToolbarPanelNodeName,
			"CharSelectButtons/" + ToolbarPanelNodeName,
			text,
			toggleButtonNodeName,
			"CharSelectButtons/" + text,
			"CharSelectButtons/" + toggleButtonNodeName
		};
		foreach (string text2 in array)
		{
			Node nodeOrNull = screen.GetNodeOrNull<Node>(text2);
			if (nodeOrNull != null)
			{
				nodeOrNull.GetParent()?.RemoveChild(nodeOrNull);
				nodeOrNull.QueueFree();
			}
		}
	}

	private string GetCombatPresentationModeDescription()
	{
		return _settingsText?.CombatPresentationModeDescription.Current ?? "Adjusts how combat presentation is played.";
	}

	private string GetCombatPresentationTitle()
	{
		return _settingsText?.CharacterSelectCombatToolbarTitle.Current ?? "Combat Style";
	}

	private string GetCharacterBackgroundTitle()
	{
		return _settingsText?.CharacterSelectBackgroundToolbarTitle.Current ?? "Character BG";
	}

	private string GetCombatPresentationModeStateLabel(CombatPresentationMode mode)
	{
		return mode switch
		{
			CombatPresentationMode.Fast => _settingsText?.CombatPresentationFast.Current ?? "Fast", 
			CombatPresentationMode.Full => _settingsText?.CombatPresentationFull.Current ?? "Full", 
			_ => _settingsText?.CombatPresentationCustom.Current ?? "Custom", 
		};
	}

	private string GetCharacterSelectBackgroundLabel(CharacterSelectBackgroundVariant variant)
	{
		return variant switch
		{
			CharacterSelectBackgroundVariant.TeamJoinPortrait => _settingsText?.CharacterSelectBackgroundTeamJoinPortrait.Current ?? "Animated Portrait", 
			CharacterSelectBackgroundVariant.LiveLobby => _settingsText?.CharacterSelectBackgroundLiveLobby.Current ?? "Character Lobby", 
			_ => _settingsText?.CharacterSelectBackgroundStaticPortrait.Current ?? "Portrait", 
		};
	}

	private static string GetCharacterVoiceDescription()
	{
		string text = TranslationServer.GetLocale().ToLowerInvariant();
		if (text.StartsWith("zh_tw", StringComparison.Ordinal) || text.StartsWith("zht", StringComparison.Ordinal))
		{
			return "開啟或關閉此角色的角色語音。";
		}
		if (text.StartsWith("zh", StringComparison.Ordinal) || text.StartsWith("zhs", StringComparison.Ordinal))
		{
			return "开启或关闭此角色的角色语音。";
		}
		if (text.StartsWith("ja", StringComparison.Ordinal) || text.StartsWith("jpn", StringComparison.Ordinal))
		{
			return "このキャラクターのボイスをオン／オフします。";
		}
		if (text.StartsWith("ko", StringComparison.Ordinal) || text.StartsWith("kor", StringComparison.Ordinal))
		{
			return "이 캐릭터의 음성을 켜거나 끕니다.";
		}
		return "Enable or disable this character's voice.";
	}

	private static CombatPresentationMode NextCombatPresentationMode(CombatPresentationMode mode)
	{
		return mode switch
		{
			CombatPresentationMode.Fast => CombatPresentationMode.Full, 
			CombatPresentationMode.Full => CombatPresentationMode.Custom, 
			_ => CombatPresentationMode.Fast, 
		};
	}

	private void SetCombatPresentationModeForRuntime(CombatPresentationMode mode, bool persist)
	{
		_settings.SetCombatPresentationMode(mode, persist);
	}

	private void ApplyCharacterSelectBackgroundMode(Control customBg, Control? originalBg, CharacterModel characterModel, string source)
	{
		CharacterSelectBackgroundPresentationProfile backgroundOptionOrDefault = _profile.CharacterSelectPresentation.GetBackgroundOptionOrDefault(_backgroundVariant);
		SetCharacterSelectBackgroundVariant(backgroundOptionOrDefault.Variant, persist: false);
		if (!string.IsNullOrWhiteSpace(backgroundOptionOrDefault.ScenePath))
		{
			Control control = originalBg ?? customBg;
			bool flag = control == originalBg;
			if (flag)
			{
				PrepareNativeCharacterSelectHost(control);
			}
			SetVisible(originalBg, visible: true);
			SetVisible(customBg, !flag);
			Node node = LoadSceneIntoContainer(control, backgroundOptionOrDefault.ScenePath, source);
			if (node != null && backgroundOptionOrDefault.ForceLoopAnimation)
			{
				ForceLoopAnimationRecursive(node, source);
			}
			return;
		}
		SetVisible(customBg, visible: false);
		SetVisible(originalBg, visible: true);
		if (originalBg != null)
		{
			RestoreNativeCharacterSelectHost(originalBg);
			if (backgroundOptionOrDefault.ForceLoopAnimation)
			{
				ForceLoopAnimationRecursive(originalBg, source);
			}
			return;
		}
		SetVisible(customBg, visible: true);
		Node node2 = LoadSceneIntoContainer(customBg, characterModel.CharacterSelectBg, source);
		if (node2 != null && backgroundOptionOrDefault.ForceLoopAnimation)
		{
			ForceLoopAnimationRecursive(node2, source);
		}
	}

	private Node? LoadSceneIntoContainer(Control bgContainer, string scenePath, string source)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			SkinLog.Warn("Character select background scene path is empty: source=" + source);
			return null;
		}
		if (bgContainer.GetChildCount() > 0 && bgContainer.HasMeta("czn_character_select_loaded_scene") && bgContainer.GetMeta("czn_character_select_loaded_scene").AsString() == scenePath && bgContainer.GetChild(0).HasMeta("czn_character_select_loaded_scene_instance") && bgContainer.GetChild(0).GetMeta("czn_character_select_loaded_scene_instance").AsString() == scenePath)
		{
			return bgContainer.GetChild(0);
		}
		GodotNodeUtil.ClearChildren(bgContainer);
		RemoveLoadedSceneMeta(bgContainer);
		PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath, null, ResourceLoader.CacheMode.Reuse);
		if (packedScene == null)
		{
			SkinLog.Warn("Character select background scene missing: source=" + source + " scene=" + scenePath);
			return null;
		}
		Node node = packedScene.Instantiate(PackedScene.GenEditState.Disabled);
		node.SetMeta("czn_character_select_loaded_scene_instance", Variant.From(in scenePath));
		bgContainer.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		bgContainer.SetMeta("czn_character_select_loaded_scene", Variant.From(in scenePath));
		SkinLog.Info("Character select background loaded: source=" + source + " scene=" + scenePath + " variant=" + _backgroundVariant);
		return node;
	}

	private void PrepareNativeCharacterSelectHost(Control nativeHost)
	{
		StoreNativeCharacterSelectHost(nativeHost);
		nativeHost.SetMeta("czn_character_select_native_host_owner", Variant.From<string>(_profile.ProfileId));
		nativeHost.Position = Vector2.Zero;
		nativeHost.Size = nativeHost.GetViewportRect().Size;
		nativeHost.Scale = Vector2.One;
		nativeHost.PivotOffset = Vector2.Zero;
	}

	private static void StoreNativeCharacterSelectHost(Control nativeHost)
	{
		if (!nativeHost.HasMeta("czn_character_select_native_host_position"))
		{
			nativeHost.SetMeta("czn_character_select_native_host_position", Variant.From<Vector2>(nativeHost.Position));
			nativeHost.SetMeta("czn_character_select_native_host_size", Variant.From<Vector2>(nativeHost.Size));
			nativeHost.SetMeta("czn_character_select_native_host_scale", Variant.From<Vector2>(nativeHost.Scale));
			nativeHost.SetMeta("czn_character_select_native_host_pivot", Variant.From<Vector2>(nativeHost.PivotOffset));
		}
	}

	private void RestoreNativeCharacterSelectHost(Control nativeHost)
	{
		if (!nativeHost.HasMeta("czn_character_select_native_host_owner"))
		{
			return;
		}
		string text = nativeHost.GetMeta("czn_character_select_native_host_owner").AsString();
		if (string.IsNullOrEmpty(text) || string.Equals(text, _profile.ProfileId, StringComparison.Ordinal))
		{
			if (nativeHost.HasMeta("czn_character_select_native_host_position"))
			{
				nativeHost.Position = nativeHost.GetMeta("czn_character_select_native_host_position").AsVector2();
			}
			if (nativeHost.HasMeta("czn_character_select_native_host_size"))
			{
				nativeHost.Size = nativeHost.GetMeta("czn_character_select_native_host_size").AsVector2();
			}
			if (nativeHost.HasMeta("czn_character_select_native_host_scale"))
			{
				nativeHost.Scale = nativeHost.GetMeta("czn_character_select_native_host_scale").AsVector2();
			}
			if (nativeHost.HasMeta("czn_character_select_native_host_pivot"))
			{
				nativeHost.PivotOffset = nativeHost.GetMeta("czn_character_select_native_host_pivot").AsVector2();
			}
			nativeHost.RemoveMeta("czn_character_select_native_host_owner");
			RemoveNativeHostTransformMeta(nativeHost);
			RemoveLoadedSceneMeta(nativeHost);
		}
	}

	private static void RemoveNativeHostTransformMeta(Control nativeHost)
	{
		if (nativeHost.HasMeta("czn_character_select_native_host_position"))
		{
			nativeHost.RemoveMeta("czn_character_select_native_host_position");
		}
		if (nativeHost.HasMeta("czn_character_select_native_host_size"))
		{
			nativeHost.RemoveMeta("czn_character_select_native_host_size");
		}
		if (nativeHost.HasMeta("czn_character_select_native_host_scale"))
		{
			nativeHost.RemoveMeta("czn_character_select_native_host_scale");
		}
		if (nativeHost.HasMeta("czn_character_select_native_host_pivot"))
		{
			nativeHost.RemoveMeta("czn_character_select_native_host_pivot");
		}
	}

	private void ForceLoopAnimationRecursive(Node root, string source)
	{
		CharacterSelectPresentationProfile characterSelectPresentation = _profile.CharacterSelectPresentation;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		Stack<Node> stack = new Stack<Node>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			Node node = stack.Pop();
			if (node is CanvasItem { Visible: false })
			{
				continue;
			}
			foreach (Node child in node.GetChildren())
			{
				stack.Push(child);
			}
			if (node.GetClass() != "SpineSprite")
			{
				continue;
			}
			try
			{
				MegaSprite megaSprite = new MegaSprite(node);
				num++;
				Func<Node, string, bool>? tryPrepareLoopSpine = _tryPrepareLoopSpine;
				if (tryPrepareLoopSpine != null && tryPrepareLoopSpine(node, source))
				{
					num5++;
				}
				if (megaSprite.HasAnimation(characterSelectPresentation.LoopAnimationName))
				{
					SpineAnimationCompat.SetAnimation(megaSprite.GetAnimationState(), characterSelectPresentation.LoopAnimationName);
					num2++;
					Func<Node, string, bool>? tryPrepareLoopSpine2 = _tryPrepareLoopSpine;
					if (tryPrepareLoopSpine2 != null && tryPrepareLoopSpine2(node, source))
					{
						num5++;
					}
				}
				else
				{
					num3++;
				}
			}
			catch (Exception ex)
			{
				num4++;
				SkinLog.Warn("Character select SpineSprite loop failed: source=" + source + " " + SkinLog.FormatException(ex));
			}
		}
		SkinLog.Info("Character select bg loop forced: source=" + source + " found=" + num + " started=" + num2 + " missing=" + num3 + " failed=" + num4 + " prepared=" + num5);
	}

	private void TryPlayCharacterSelectVoice(CharacterSelectBackgroundVariant backgroundVariant, string source)
	{
		CharacterSkinVoicePlayerRuntime voicePlayer = _voicePlayer;
		if (voicePlayer == null)
		{
			return;
		}
		voicePlayer.Stop();
		if (_profile.CharacterSelect.TryGetVoicePath(backgroundVariant, out string voicePath))
		{
			if (voicePlayer.PlayFor(_voiceOwner?.Invoke(), voicePath))
			{
				SkinLog.Info("Played character select voice: source=" + source + " background=" + backgroundVariant.ToString() + " path=" + voicePath);
			}
			voicePlayer.Prewarm(voicePath);
		}
	}

	private void SetCharacterSelectBackgroundVariant(CharacterSelectBackgroundVariant variant, bool persist)
	{
		_backgroundVariant = _profile.CharacterSelectPresentation.NormalizeVariant(variant);
		_settings.SetCharacterSelectBackgroundVariant(_backgroundVariant, persist);
	}

	private string GetScenePathForVariant(CharacterModel characterModel, CharacterSelectBackgroundVariant variant)
	{
		return GetScenePathForVariant(characterModel, _profile.CharacterSelectPresentation.GetBackgroundOptionOrDefault(variant));
	}

	private static string GetScenePathForVariant(CharacterModel characterModel, CharacterSelectBackgroundPresentationProfile option)
	{
		if (!string.IsNullOrWhiteSpace(option.ScenePath))
		{
			return option.ScenePath;
		}
		return characterModel.CharacterSelectBg;
	}

	private static void SetVisible(CanvasItem? item, bool visible)
	{
		if (item != null)
		{
			item.Visible = visible;
		}
	}

	private static bool AnyOtherCustomBgVisible(NCharacterSelectScreen screen, string activeCustomBgNodeName)
	{
		foreach (Node child in screen.GetChildren())
		{
			if (child is Control control && child.Name.ToString().EndsWith("CustomBg", StringComparison.Ordinal) && child.Name != (StringName)activeCustomBgNodeName && control.Visible)
			{
				return true;
			}
		}
		return false;
	}

	private static void RemoveLoadedSceneMeta(Node node)
	{
		if (node.HasMeta("czn_character_select_loaded_scene"))
		{
			node.RemoveMeta("czn_character_select_loaded_scene");
		}
	}

	private static bool TryGetMultiplayerLoadLocalCharacter(NMultiplayerLoadGameScreen screen, out CharacterModel characterModel)
	{
		characterModel = null;
		LoadRunLobby lobby = AccessTools.Field(typeof(NMultiplayerLoadGameScreen), "_runLobby")?.GetValue(screen) as LoadRunLobby;
		if (lobby == null)
		{
			return false;
		}
		SerializablePlayer serializablePlayer = lobby.Run.Players.FirstOrDefault((SerializablePlayer player) => player.NetId == lobby.NetService.NetId);
		if (serializablePlayer?.CharacterId == null)
		{
			return false;
		}
		characterModel = ModelDb.GetById<CharacterModel>(serializablePlayer.CharacterId);
		return true;
	}
}
