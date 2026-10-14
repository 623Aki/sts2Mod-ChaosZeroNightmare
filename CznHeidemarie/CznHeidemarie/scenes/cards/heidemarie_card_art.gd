extends Node
## 「解放极光」的动态卡面。
##
## 古代（全幅立绘）牌的卡面由 NCard 交给 %AncientPortrait 显示。这里把 CZN 的卡面
## Spine 骨架渲染进一个 SubViewport，再把 viewport 的实时贴图塞给那个 TextureRect，
## 卡面就动起来了 —— 做法与参考 mod 的 `hew_extreme_art.gd`（「超究武神霸斩」）一致。
##
## 画布取 600x844 = 2 倍卡面（300x422）。古代牌的立绘窗口比例实测是 600:847
## （`mask_region`，见 card_canvas_group_mask_material.tres），与卡面比例一致，
## 所以只要画布按卡面比例、骨架再按“cover”铺满，就不会被拉伸、也不会留边。

const SKEL_DATA_PATH := "res://CznHeidemarie/animations/cards/heidemarie_card_liberated_aurora_skel_data.tres"

## 骨架里唯一的动画名。
const ANIMATION := "animation"

## 画布尺寸 = 2 倍卡面。
const VIEWPORT_SIZE := Vector2i(600, 844)

## 骨架官方 setup 包围盒（Spine 坐标，y 向上）。CZN 全角色的卡面骨架共用同一套模板：
## 声明的设计尺寸都是 418.82 x 654.80，与参考 mod 用的那份完全一致，故沿用同一个值。
const SETUP_BOUNDS := Rect2(-198.96, -327.41, 418.8201, 654.8001)

var _targets: Array[TextureRect] = []
var _surface: SubViewport
var _body: SpineSprite
var _running := false


func _ready() -> void:
	_surface = SubViewport.new()
	_surface.size = VIEWPORT_SIZE
	_surface.transparent_bg = true
	_surface.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(_surface)

	_body = SpineSprite.new()
	_body.skeleton_data_res = load(SKEL_DATA_PATH)
	_surface.add_child(_body)

	var state = _body.get_animation_state()
	if state != null:
		state.set_animation(ANIMATION, true, 0)

	# cover：等比放大到刚好盖住画布，多出来的部分由卡面四边裁掉。
	var fit := maxf(VIEWPORT_SIZE.x / SETUP_BOUNDS.size.x, VIEWPORT_SIZE.y / SETUP_BOUNDS.size.y)
	_body.scale = Vector2.ONE * fit
	_body.position = Vector2(VIEWPORT_SIZE) * 0.5 - SETUP_BOUNDS.get_center() * fit

	_body.process_mode = Node.PROCESS_MODE_DISABLED
	set_process(false)


## 把本渲染器挂到某张卡的 %AncientPortrait 上（同时把贴图换成实时画面）。
func register_portrait(portrait: TextureRect) -> void:
	if _surface == null:
		return
	if not _targets.has(portrait):
		_targets.append(portrait)
	portrait.texture = _surface.get_texture()
	set_process(true)


## 摘掉某张卡。只在贴图确实还是本渲染器那张时才清空
## —— NCard.UpdatePortrait 可能已经把它换成别的牌的了。
func unregister_portrait(portrait: TextureRect) -> void:
	if _targets.has(portrait):
		_targets.erase(portrait)
	if _surface != null and is_instance_valid(portrait) and portrait.texture == _surface.get_texture():
		portrait.texture = null
	if _targets.is_empty():
		_set_running(false)
		set_process(false)


## 画面上没有本卡时停掉 SubViewport 刷新，避免白白渲染。
func _process(_delta: float) -> void:
	var visible_card := false
	for index in range(_targets.size() - 1, -1, -1):
		var portrait := _targets[index]
		if not is_instance_valid(portrait):
			_targets.remove_at(index)
		elif portrait.is_visible_in_tree():
			visible_card = true
	_set_running(visible_card)
	if _targets.is_empty():
		set_process(false)


func _set_running(value: bool) -> void:
	if _running == value:
		return
	_running = value
	if _body != null:
		_body.process_mode = Node.PROCESS_MODE_INHERIT if value else Node.PROCESS_MODE_DISABLED
	if _surface != null:
		_surface.render_target_update_mode = (
			SubViewport.UPDATE_ALWAYS if value else SubViewport.UPDATE_DISABLED
		)
