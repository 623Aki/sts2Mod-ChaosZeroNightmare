extends Node
## 角色「古代牌」（全幅立绘）的动态卡面渲染器。
##
## 古代牌的卡面由 NCard 交给 %AncientPortrait 显示。这里按「卡面档案」（PROFILES）
## 把 CZN 的 Spine 骨架渲染进 SubViewport，再把 viewport 的实时贴图塞给那张卡的
## TextureRect，卡面就动起来了 —— 做法与参考 mod 的 `hew_extreme_art.gd`
## （「超究武神霸斩」）一致。
##
## 一个「档案」= 一组**共用同一套变换**的骨架层。它们通常是同一张 cut-in 的
## 分层切片，**必须共用 position / scale**，否则层与层之间会错位。
## 每个档案一份独立的 SubViewport（不同档案的骨架毫不相关，不能混在一个 viewport 里）。
##
## 画布取 600x844 = 2 倍卡面（300x422）。古代牌的立绘窗口比例实测是 600:847
## （`mask_region`，见 card_canvas_group_mask_material.tres），与卡面比例一致，
## 所以只要画布按卡面比例、骨架再按「cover」铺满，就不会被拉伸、也不会留边。

## 画布尺寸 = 2 倍卡面。
const VIEWPORT_SIZE := Vector2i(600, 844)

## 卡面档案表：key = 档案 id（由 Registration/HeidemarieAnimatedCardArt.cs 按卡牌类型给出）。
##
## 每个档案：
##   - `bounds`：整组骨架的 setup 包围盒（Spine 坐标），用于算「cover」比例与居中；
##   - `layers[]`：**后 → 前**，每层
##       - `skel`       SpineSkeletonDataResource 的 res:// 路径；
##       - `animations` 播放序列：前几段接续播，**最后一段循环**（如 ["intro", "loop"]）；
##       - `z`          层序（同一档案内互比）。
var PROFILES := {
	# 「解放极光」：素材区 card/unique_30093_06 的单骨架全幅立绘。
	# bounds 是 CZN 卡面骨架的通用模板值（各卡共用），沿用既有实现。
	"liberated_aurora": {
		"bounds": Rect2(-198.96, -327.41, 418.8201, 654.8001),
		"layers": [
			{
				"skel": "res://CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora_skel_data.tres",
				"animations": ["animation"],
				"z": 1,
			},
		],
	},

	# 「终焉」：素材区 card/unique_30093_01 的单骨架全幅立绘（参考「解放极光」）。
	# bounds 暂沿用「解放极光」的通用模板值（同角色 card 骨架），若实机偏位再用 CZN_Viewer 采样修正。
	# 动画名沿用 card 骨架惯例 "animation"（若该骨架轨道名不同，卡面会停在首帧，需回此改正）。
	"terminus": {
		"bounds": Rect2(-198.96, -327.41, 418.8201, 654.8001),
		"layers": [
			{
				"skel": "res://CznHeidemarie/animations/cards/heidemarie_card_terminus_skel_data.tres",
				"animations": ["animation"],
				"z": 1,
			},
		],
	},

	# 「剑之乐园」：官方「破军(ux)全屏演出」的三张插画切片，组装成一张卡面。
	#   后 → 前：illust_3（花2 + 石柱1~4）、illust_2（人物）、illust_1（花1 + 光晕）。
	# 层序与选人界面「立绘」档一致（scenes/screens/char_select/heidemarie_static_portrait_bg.tscn）。
	# bounds = 三层包围盒并集（CZN_Viewer headless 采样值，见「动画与特效清单」§4.1）；
	# 该采样比实机绘制范围偏小约 1.3 倍，用 cover 铺满后四边本就会被卡面裁掉，属预期。
	# 若实机看着偏大 / 偏小，只调这里的 bounds 即可（三层共用，不会失配）。
	"sword_paradise": {
		"bounds": Rect2(-535.09, -481.40, 1051.15, 968.55),
		"layers": [
			{
				"skel": "res://CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_3_skel_data.tres",
				"animations": ["intro", "loop"],
				"z": 1,
			},
			{
				"skel": "res://CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_2_skel_data.tres",
				"animations": ["intro", "loop"],
				"z": 2,
			},
			{
				"skel": "res://CznHeidemarie/animations/effects/heidemarie_30093_ux_illust_1_skel_data.tres",
				"animations": ["intro", "loop"],
				"z": 3,
			},
		],
	},
}

## 当前把卡面挂在哪些 TextureRect 上。
var _targets: Array[TextureRect] = []

## TextureRect → 档案 id。
var _target_profiles: Dictionary = {}

## 档案 id → { "viewport": SubViewport, "bodies": Array[SpineSprite], "anims": Array, "running": bool }。
var _surfaces: Dictionary = {}


func _ready() -> void:
	# 档案按需创建（第一次有该档案的卡被显示时才建 SubViewport）——
	# 预建全部档案会把三张 ux 插画骨架一起读进内存，而只有对应卡出现时才用得上。
	set_process(false)


## 把本渲染器挂到某张卡的 %AncientPortrait 上（同时把贴图换成实时画面）。
func register_portrait(portrait: TextureRect, profile_id: String) -> void:
	var surface: Dictionary = _ensure_surface(profile_id)
	if surface.is_empty():
		push_warning("heidemarie_card_art: 档案 '%s' 没有可用图层，保持静态兜底图" % profile_id)
		return

	if not _targets.has(portrait):
		_targets.append(portrait)
	_target_profiles[portrait] = profile_id
	portrait.texture = surface["viewport"].get_texture()
	set_process(true)
	# 立刻启播，不等下一帧 _process：本渲染器可能是在战斗中途（甚至暂停态）被创建的，
	# 那时 _process 未必马上跑；若只靠 _process 开 SubViewport，卡面会先停在空白/首帧上。
	_update_running()


## 摘掉某张卡。只在贴图确实还是本渲染器那张时才清空
## —— NCard.UpdatePortrait 可能已经把它换成别的牌的了。
func unregister_portrait(portrait: TextureRect) -> void:
	if _targets.has(portrait):
		_targets.erase(portrait)
	var profile_id: String = _target_profiles.get(portrait, "")
	_target_profiles.erase(portrait)

	if not profile_id.is_empty() and is_instance_valid(portrait):
		var surface: Dictionary = _surfaces.get(profile_id, {})
		if not surface.is_empty() and portrait.texture == surface["viewport"].get_texture():
			portrait.texture = null

	_update_running()


## 画面上没有本卡时停掉 SubViewport 刷新，避免白白渲染。
func _process(_delta: float) -> void:
	for index in range(_targets.size() - 1, -1, -1):
		var portrait := _targets[index]
		if not is_instance_valid(portrait):
			_target_profiles.erase(portrait)
			_targets.remove_at(index)

	_update_running()
	if _targets.is_empty():
		set_process(false)


## 取（必要时创建）某档案的 SubViewport。
func _ensure_surface(profile_id: String) -> Dictionary:
	if _surfaces.has(profile_id):
		return _surfaces[profile_id]
	if not PROFILES.has(profile_id):
		return {}

	var profile: Dictionary = PROFILES[profile_id]
	var viewport := SubViewport.new()
	viewport.size = VIEWPORT_SIZE
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(viewport)

	# cover：整组按「刚好盖住画布」等比放大，多出来的部分由卡面四边裁掉。
	# 三层共用同一套 position / scale，因此不会互相错位。
	var bounds: Rect2 = profile["bounds"]
	var fit := maxf(VIEWPORT_SIZE.x / bounds.size.x, VIEWPORT_SIZE.y / bounds.size.y)
	var origin := Vector2(VIEWPORT_SIZE) * 0.5 - bounds.get_center() * fit

	var bodies: Array[SpineSprite] = []
	var anims: Array = []
	for layer in profile["layers"]:
		var skel_path: String = str(layer.get("skel", ""))
		# 素材缺失必须在这里报出来：否则整张卡面会悄悄变成一张空白/静态图，
		# 而日志里什么线索都没有（“卡面不是动态的”最难查的一类故障）。
		if skel_path.is_empty() or not ResourceLoader.exists(skel_path):
			push_warning("heidemarie_card_art: 档案 '%s' 的骨架资源缺失，该层已跳过：%s"
				% [profile_id, skel_path])
			continue

		var body := SpineSprite.new()
		body.skeleton_data_res = load(skel_path)
		body.scale = Vector2.ONE * fit
		body.position = origin
		body.z_index = int(layer.get("z", 1))
		body.process_mode = Node.PROCESS_MODE_DISABLED
		viewport.add_child(body)
		_play_sequence(body, layer["animations"])
		bodies.append(body)
		anims.append(layer["animations"])

	# 一层都没建成 = 这个档案不可用。返回空字典让 register_portrait 放弃接管，
	# 卡面回落到静态兜底图（宁可显示一张静态图，也不要一张空白卡）。
	if bodies.is_empty():
		viewport.queue_free()
		return {}

	var surface := {"viewport": viewport, "bodies": bodies, "anims": anims, "running": false}
	_surfaces[profile_id] = surface
	return surface


## 按序列播动画：前几段接续播，最后一段循环。
func _play_sequence(body: SpineSprite, animations: Array) -> void:
	var state = body.get_animation_state()
	if state == null or animations.is_empty():
		return

	var last := animations.size() - 1
	state.set_animation(animations[0], last == 0, 0)
	for index in range(1, animations.size()):
		# add_animation(name, loop, track, delay)：delay 0 = 接在上一段之后
		state.add_animation(animations[index], index == last, 0, 0.0)


## 自愈：骨架刚 new 出来 / 图集纹理尚未就绪时，`set_animation` 可能没真正挂上轨道，
## 那一层就会永远停在 setup 姿势 —— 看上去就是「卡面是一张静态图」。
## 这里在每次启用档案时检查一次，没轨道就重播一次；只在真的缺失时才动手，正常情况零开销。
func _ensure_animations(surface: Dictionary) -> void:
	var bodies: Array = surface["bodies"]
	var anims: Array = surface["anims"]
	for index in bodies.size():
		var body: SpineSprite = bodies[index]
		if not is_instance_valid(body):
			continue
		var state = body.get_animation_state()
		if state != null and state.get_current(0) != null:
			continue
		_play_sequence(body, anims[index])


## 按「有没有可见的卡在被渲染」逐档案启停 viewport 与骨架推进。
func _update_running() -> void:
	var active := {}
	for portrait in _targets:
		var profile_id: String = _target_profiles.get(portrait, "")
		if profile_id.is_empty():
			continue
		if is_instance_valid(portrait) and portrait.is_visible_in_tree():
			active[profile_id] = true

	for profile_id in _surfaces.keys():
		var surface: Dictionary = _surfaces[profile_id]
		var should_run: bool = active.has(profile_id)
		if surface["running"] == should_run:
			continue

		surface["running"] = should_run
		surface["viewport"].render_target_update_mode = (
			SubViewport.UPDATE_ALWAYS if should_run else SubViewport.UPDATE_DISABLED
		)
		for body in surface["bodies"]:
			body.process_mode = Node.PROCESS_MODE_INHERIT if should_run else Node.PROCESS_MODE_DISABLED
		if should_run:
			_ensure_animations(surface)
