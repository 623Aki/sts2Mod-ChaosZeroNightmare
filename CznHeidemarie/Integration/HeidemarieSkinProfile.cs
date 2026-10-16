using System;
using System.Collections.Generic;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using Godot;

namespace CznHeidemarie.Integration;

/// <summary>
/// The single CznCore profile for Heidemarie.
/// Keeping this profile complete is important: the bootstrap owns the shared
/// character-select, preload, lifecycle, and compatibility patch groups.
/// </summary>
internal static class HeidemarieSkinProfile
{
	internal const string BodySkeletonDataPath = Heidemarie.BodySkeletonDataPath;
	internal const string BodyMaterialPath = "res://CznHeidemarie/materials/heidemarie_spine_premultiplied.tres";
	internal const string BattleReadyScenePath = "res://scenes/heidemarie_battle_ready.tscn";

	internal static readonly CharacterSkinProfile Profile = BuildProfile();

	internal static readonly RunStartPreloadProfile Preload = new(
		Profile,
		"Heidemarie",
		BuildPreloadItems);

	private static CharacterSkinProfile BuildProfile()
	{
		CombatPresentationProfileTables presentation = new(
			new Dictionary<string, CombatPresentationState>(),
			new Dictionary<CombatPresentationCardRoute, CombatPresentationRoutePlan>());

		CharacterSkinCombatBodyProfile body = new(
			IdleAnimation: "idle",
			ReadyLoopAnimation: "b_idle",
			ReadyExitAnimation: "b_idle_to_idle",
			EntrySequence: new[] { "enter_ready", "enter_play", "enter_end" },
			EntryRunInOffsetX: 0f,
			EntryRunInDurationSeconds: 0f,
			EntryRunInSegmentOffsets: Array.Empty<Vector2>(),
			VictoryStartAnimations: new[] { "victory_ready", "victory" },
			VictoryLoopAnimation: "victory",
			DeathSequence: new[] { "death_ready", "death" },
			PostActionReadyDwellSeconds: 0f,
			ReadyExitGraceSeconds: 0.1f);

		CharacterSkinFeatureSupportProfile features = new(
			CombatBodyScalePatch: false,
			CombatPositionOffsetPatch: false,
			CombatVoiceHooks: true,
			CombatEntryAnimation: true,
			CombatIdleEffects: false,
			BattleReadyOverlay: true,
			DeathCgOverlay: true,
			MeleeApproachMovement: false,
			OfficialAttackTiming: true);

		CharacterSelectPresentationProfile characterSelect = new(
			LiveNodeName: "AnimatedBg",
			OriginalBgNodeName: "OriginalBg",
			CustomBgNodeName: "VariantHost",
			ToggleButtonNodeName: "CharacterSelectSettings",
			StaticScenePath: "res://CznHeidemarie/scenes/screens/char_select/heidemarie_static_portrait_bg.tscn",
			LiveScenePath: "res://CznHeidemarie/scenes/screens/char_select/heidemarie_live_lobby_bg.tscn",
			LoopAnimationName: "lobby",
			ToggleButtonMinimumSize: new Vector2(0f, 0f),
			ToggleButtonPosition: Vector2.Zero,
			BackgroundOptions: new CharacterSelectBackgroundPresentationProfile[]
			{
				new(CharacterSelectBackgroundVariant.StaticPortrait, "res://CznHeidemarie/scenes/screens/char_select/heidemarie_static_portrait_bg.tscn"),
				new CharacterSelectBackgroundPresentationProfile(CharacterSelectBackgroundVariant.TeamJoinPortrait, "res://CznHeidemarie/scenes/screens/char_select/heidemarie_dynamic_portrait_bg.tscn"),
				new CharacterSelectBackgroundPresentationProfile(CharacterSelectBackgroundVariant.LiveLobby, "res://CznHeidemarie/scenes/screens/char_select/heidemarie_live_lobby_bg.tscn", ForceLoopAnimation: true)
			},
			CombatPresentationIconPath: "res://CznHeidemarie/images/character_select/icon_large_battle.png",
			CharacterBackgroundIconPath: "res://CznHeidemarie/images/character_select/icon_large_lobby.png",
			SettingsAvatarPath: "res://images/ui/top_panel/character_icon_heidemarie.png",
			MapMarkerIconPath: "res://images/packed/map/icons/map_marker_heidemarie.png");

		return new CharacterSkinProfile(
			ProfileId: HeidemarieSettings.CurrentCharacterId,
			LogPrefix: "Heidemarie",
			RuntimeNodeNamePrefix: "Heidemarie",
			TargetCharacterType: typeof(Heidemarie),
			BodyTexturePath: "",
			BodySkeletonDataPath: BodySkeletonDataPath,
			CombatSpriteNodeName: "Visuals",
			BodySpineScaleMultiplier: 1.247f,
			CombatPositionOffset: Vector2.Zero,
			CombatBody: body,
			FormVfx: new CharacterSkinFormVfxProfile("", "", ""),
			PremultipliedAlphaMaterialPath: BodyMaterialPath,
			CombatIdleEffectsScenePath: "",
			CombatEffects: EmptyCombatEffects(),
			Features: features,
			SettingsText: new CharacterSkinSettingsTextProfile(
				HeidemarieText.SettingsHeader,
				HeidemarieText.CharacterType,
				SettingsLocalization.Text("heidemarie_effects", "战斗特效"),
				HeidemarieText.CharacterType),
			CardPresentation: new CombatCardPresentationProfile(
				Array.Empty<CombatCardPresentationOverride>(),
				new Dictionary<CombatPresentationCardRoute, IReadOnlyList<string>>(),
				Array.Empty<string>(),
				Array.Empty<string>()),
			CombatPresentation: new HeidemarieCombatPresentationProfile(presentation),
			BattleReady: new BattleReadyOverlayProfile(
				"HeidemarieBattleReady",
				BattleReadyScenePath,
				"res://CznHeidemarie/animations/model/30093_skel_data.tres",
				new BattleReadyOverlayAnimations("", "b_idle", "", "attack_play1", "buff_play"),
				0.15f,
				0.1f,
				-720f,
				0.35f,
				null,
				new Dictionary<CombatPresentationCardRoute, string>
				{
					[CombatPresentationCardRoute.Attack] = "attack_play1",
					[CombatPresentationCardRoute.Skill] = "buff_play"
				}),
			CharacterSelect: new CharacterSelectIdentityProfile(
				"HEIDEMARIE.title",
				"HEIDEMARIE.description",
				new Dictionary<CharacterSelectBackgroundVariant, string>()),
			CharacterSelectPresentation: characterSelect,
			RestSite: new RestSitePresentationProfile("camping", new[] { "b_idle", "idle" }, "", 0, Vector2.Zero, "heidemarie_rest_body_base_position", Array.Empty<RestSiteCampingLayerProfile>()),
			Defeat: new DefeatPresentationProfile(
				"res://CznHeidemarie/images/defeat/collapse_30093_01.png",
				"res://CznHeidemarie/audio/sfx/se_30093_collapse.ogg",
				0f, 0.55f, 0.3f, 0f, 0f, 0.15f, 1.2f, 0.9f, 1f, Vector2.Zero, Vector2.Zero, 0f, 0f, 250,
				Array.Empty<DefeatEffectLayerProfile>(), Array.Empty<DefeatSfxCue>(),
				Array.Empty<DefeatCgMotionLayerProfile>(), 1f, "res://CznHeidemarie/images/defeat/collapse_30093_02.png"),
			Voice: new CharacterVoiceProfile(
				new[] { "res://CznHeidemarie/audio/voice/vo_30093_stage_enter_01.ogg", "res://CznHeidemarie/audio/voice/vo_30093_idle_01.ogg" },
				"res://CznHeidemarie/audio/voice/vo_30093_stage_enter_01.ogg",
				"res://CznHeidemarie/audio/voice/vo_30093_stage_success_01.ogg",
				"res://CznHeidemarie/audio/voice/vo_30093_hit_01.ogg",
				"res://CznHeidemarie/audio/voice/vo_30093_lose_01.ogg"),
			GlobalSettingsIconPath: "res://images/ui/top_panel/character_icon_heidemarie.png");
	}

	private static CharacterSkinCombatEffectsProfile EmptyCombatEffects() => new(
		Vector2.One, Vector2.Zero, Colors.White, Colors.White, "", "", "", Vector2I.Zero, Vector2.Zero,
		new CombatEffectMaterialPolicyProfile(Colors.White, 1f, 0f, 1f, Colors.White, 1f, 0f, 1f, 1f,
			new CombatEffectViewportMaterialProfile(1f, 1f, 1f, 1f),
			new CombatEffectViewportMaterialProfile(1f, 1f, 1f, 1f)));

	private static IReadOnlyList<RunStartPreloadItem> BuildPreloadItems()
	{
		RunStartPreloadItemBuilder items = new();
		items.AddResource(BodySkeletonDataPath, "Heidemarie body skeleton");
		items.AddResource(BodyMaterialPath, "Heidemarie body material");
		items.AddResource(BattleReadyScenePath, "Heidemarie battle-ready scene");
		items.AddResource("res://CznHeidemarie/cinematic/heidemarie_ux.json", "Heidemarie UX cinematic");
		items.AddResource("res://CznHeidemarie/cinematic/heidemarie_ug.json", "Heidemarie UG cinematic");
		return items.ToArray();
	}
}

/// <summary>
/// Adapts the table container used by the Heidemarie-specific presentation code
/// to the resolver contract consumed by the shared CznCore runtimes.
/// </summary>
internal sealed class HeidemarieCombatPresentationProfile : ICombatPresentationProfile
{
	private readonly CombatPresentationProfileTables _tables;

	internal HeidemarieCombatPresentationProfile(CombatPresentationProfileTables tables)
	{
		_tables = tables;
	}

	public CombatPresentationCommonStateIds CommonStateIds { get; } =
		new("card_ready", "restore_idle", "hit");

	public bool TryGetState(string stateId, out CombatPresentationState state) =>
		_tables.TryGetState(stateId, out state);

	public bool TryGetRoutePlan(CombatPresentationCardRoute route, out CombatPresentationRoutePlan plan) =>
		_tables.TryGetRoutePlan(route, out plan);

	public bool TryGetRoutePlan(CombatPresentationCardContext context, out CombatPresentationRoutePlan plan) =>
		_tables.TryGetRoutePlan(context.Route, out plan);

	public bool TryGetAttackPlan(CombatPresentationCardContext context, out CombatPresentationAttackPlan plan) =>
		_tables.TryGetAttackPlan(context.Route, out plan);

	public bool TryGetBodyAnimationTiming(string animationName, out CombatPresentationAnimationTiming timing) =>
		_tables.TryGetBodyAnimationTiming(animationName, out timing);

	public bool TryGetPersistentEffectBindings(string animationName, out IReadOnlyList<CombatPresentationPersistentEffectBinding> effects) =>
		_tables.TryGetPersistentEffectBindings(animationName, out effects);

	public bool TryGetTransientEffects(string animationName, out IReadOnlyList<CombatPresentationEffectCue> effects) =>
		_tables.TryGetTransientEffects(animationName, out effects);

	public bool TryGetSfxCues(string animationName, out IReadOnlyList<CombatPresentationSfxCue> sfxCues) =>
		_tables.TryGetSfxCues(animationName, out sfxCues);

	public IEnumerable<CombatPresentationEffectCue> EnumerateTransientEffectCues() =>
		_tables.EnumerateTransientEffectCues();

	public IEnumerable<CombatPresentationSfxCue> EnumerateSfxCues() =>
		_tables.EnumerateSfxCues();
}
