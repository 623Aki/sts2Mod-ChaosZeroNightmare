extends CanvasLayer

const CG_FRAMING = preload("res://CznHeidemarie/scenes/defeat_cg_framing.gd")

signal stage_started
signal stage_finished

# Heidemarie 30093 defeat overlay. The six shared gloom Spine layers and the
# twelve CG/afterimage tracks are direct official assets (ported from the
# Tiphera reference mod). Character-specific scale and offset are supplied
# from 30093.srue before this node enters tree.
# STS2 renderer adapters fit the official 2044x900 surface by viewport height
# and host-disable one incompatible full-screen mobile reflection transition.
# Source animation data, the other five FX layers and all CG tracks stay intact.

const TIMELINE := \
	"res://CznHeidemarie/data/screen_fx_collapse_gloom_cha_node.json"
const FX_MOBILE_FRAME := Vector2(2044, 900)
const DURATION := 2.0
const CFX_SCALE := 0.8
const FX_ROOT := \
	"res://CznHeidemarie/animations/effects/collapse_gloom/"
const PMA_MATERIAL := \
	"res://CznHeidemarie/materials/heidemarie_gloom_pma_alpha.tres"
const PMA_ADDITIVE_MATERIAL := \
	"res://CznHeidemarie/materials/heidemarie_gloom_pma_additive.tres"
const FX := [
	{"stem":"screen_fx_collapse_gloom_sin_bg3", "z":24, "visible":2.0},
	{"stem":"screen_fx_collapse_gloom_sin_bg2", "z":26, "visible":2.0},
	{"stem":"screen_fx_collapse_gloom_sin_bg1", "z":28, "visible":2.0},
	{"stem":"screen_fx_collapse_gloom_sin_glass", "z":72, "visible":2.0},
	{"stem":"screen_fx_collapse_gloom_sin_lighting", "z":74, "visible":2.0},
	# STS2 calibrated adapter: this 4124x2256 mobile transition becomes opaque
	# diagonal slabs on the STS2 combat surface. The other five official gloom
	# layers and the full CG timeline remain enabled.
	{
		"stem":"screen_fx_collapse_gloom_sin_screen_refl",
		"z":76,
		"visible":0.6,
		"host_visible":false,
	},
]

@export var cg_texture_path := \
	"res://CznHeidemarie/images/defeat/collapse_30093_02.png"
@export var cg_outer_scale := 0.9
@export var srue_offset := Vector2(0, 160)
@export var preview_manual := false
@export var park_on_finish := false
# 预览工具注入的骨架数据（stem → SpineSkeletonDataResource）。游戏内 pck 的
# .tres 导入链完好，此项留空；只有在导入链不可用的环境（如 AnimLab 的纯运行时
# spine 构建）里才由工具用原始 .skel/.atlas 现建后注入。
@export var fx_skeleton_override: Dictionary = {}

var _source: Dictionary = {}
var _anim: Dictionary = {}
var _anim_bones: Dictionary = {}
var _anim_slots: Dictionary = {}
var _bone_defs: Dictionary = {}
var _slot_defs: Dictionary = {}
var _cg_layers: Array = []
var _cg_reference: Dictionary = {}
var _cg_content: Rect2
var _fx_layers: Array = []
var _backdrop: ColorRect
var _pma_material: Material
var _pma_additive_material: Material
var _epoch_msec := 0
var _started := false
var _finished := false
var _failed := false
var _clock := 0.0

# ── 以下为 CZN_AnimLab 预览工具的勾选 / 查询钩子，不参与演出逻辑 ──
# stem → true 表示工具侧取消勾选该特效层（_apply_frame 每帧都会查）
var _fx_disabled: Dictionary = {}


func _ready() -> void:
	layer = 1000
	process_mode = Node.PROCESS_MODE_ALWAYS
	process_priority = -100
	set_process(false)
	_build_backdrop()
	if not _load_source():
		call_deferred("_finish_stage")
		return
	_build_fx()
	if _failed:
		call_deferred("_finish_stage")
		return
	_build_cg()
	if not _load_cg(cg_texture_path):
		call_deferred("_finish_stage")
		return
	get_viewport().size_changed.connect(_layout)
	_layout()
	print(
		"heidemarie_defeat_runtime_built fx=", _fx_layers.size(),
		" slots=", _cg_layers.size(),
		" cg=", cg_texture_path,
		" texture=", _cg_texture_size(),
		" outer_scale=", cg_outer_scale,
		" srue_offset=", srue_offset)
	if preview_manual:
		seek_preview(0.0)
	else:
		restart_stage()


func _process(_delta: float) -> void:
	if preview_manual or _finished or _failed:
		return
	if not _started:
		_started = true
		_epoch_msec = Time.get_ticks_msec()
		emit_signal("stage_started")
	_clock = float(Time.get_ticks_msec() - _epoch_msec) / 1000.0
	_apply_frame(minf(_clock, DURATION))
	if _clock >= DURATION:
		_finish_stage()


func configure(texture_path: String, outer_scale: float, offset: Vector2) -> void:
	cg_texture_path = texture_path
	cg_outer_scale = maxf(0.001, outer_scale)
	srue_offset = offset
	if is_node_ready():
		_load_cg(cg_texture_path)
		_apply_frame(_clock)


func restart_stage() -> void:
	_finished = false
	_started = false
	_epoch_msec = 0
	_clock = 0.0
	visible = true
	_restart_fx()
	_apply_frame(0.0)
	set_process(not preview_manual and not _failed)


func seek_preview(time_s: float) -> void:
	_clock = clampf(time_s, 0.0, DURATION)
	_apply_frame(_clock)


func is_failed() -> bool:
	return _failed


func debug_state() -> Dictionary:
	return {
		"failed": _failed,
		"fx_count": _fx_layers.size(),
		"slot_count": _cg_layers.size(),
		"cg_path": cg_texture_path,
		"cg_size": _cg_texture_size(),
		"cg_outer_scale": cg_outer_scale,
		"srue_offset": srue_offset,
	}


## 工具钩子：6 层 gloom FX 的静态清单（z / 显示时长 / 宿主默认可见性）
func fx_stems() -> Array:
	var out: Array = []
	for spec in FX:
		out.append({
			"stem": str(spec.stem),
			"z": int(spec.z),
			"visible": float(spec.visible),
			"host_visible": bool(spec.get("host_visible", true)),
		})
	return out


## 工具钩子：勾选 / 取消勾选某一特效层
func set_fx_enabled(stem: String, on: bool) -> void:
	if on:
		_fx_disabled.erase(stem)
	else:
		_fx_disabled[stem] = true


func _build_backdrop() -> void:
	_backdrop = ColorRect.new()
	_backdrop.name = "HeidemarieDefeatBackdrop"
	_backdrop.color = Color(0.008, 0.009, 0.015, 1.0)
	_backdrop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_backdrop.z_index = -1000
	_backdrop.z_as_relative = false
	add_child(_backdrop)


func _load_source() -> bool:
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(TIMELINE))
	if typeof(parsed) != TYPE_DICTIONARY:
		_fail("Heidemarie defeat cha_node parse failed: " + TIMELINE)
		return false
	_source = parsed
	_anim = _source.get("animations", {}).get("animation", {})
	_anim_bones = _anim.get("bones", {})
	_anim_slots = _anim.get("slots", {})
	for definition in _source.get("bones", []):
		_bone_defs[str(definition.get("name", ""))] = definition
	for definition in _source.get("slots", []):
		_slot_defs[str(definition.get("name", ""))] = definition
	if _slot_defs.size() != 12:
		_fail("Expected 12 official gloom cha_node slots, got %d" % _slot_defs.size())
		return false
	return true


func _build_fx() -> void:
	_pma_material = ResourceLoader.load(
		PMA_MATERIAL, "", ResourceLoader.CACHE_MODE_REUSE) as Material
	_pma_additive_material = ResourceLoader.load(
		PMA_ADDITIVE_MATERIAL, "", ResourceLoader.CACHE_MODE_REUSE) as Material
	if _pma_material == null or _pma_additive_material == null:
		_fail("Missing Heidemarie gloom PMA renderer materials")
		return
	for spec in FX:
		var stem := str(spec.stem)
		var path := FX_ROOT + stem + "/" + stem + "_skel_data.tres"
		var data = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REUSE)
		# .tres 能加载但骨架没就绪 = 导入链不可用（预览工具的运行时 spine 构建），
		# 退回工具注入的现建骨架数据
		if data != null and not (data as SpineSkeletonDataResource).is_skeleton_data_loaded():
			data = null
		if data == null:
			data = fx_skeleton_override.get(stem, null)
		if data == null:
			_fail("Missing converted Heidemarie gloom Spine: " + path)
			continue
		var sprite := SpineSprite.new()
		sprite.name = stem
		sprite.z_index = int(spec.z)
		sprite.z_as_relative = false
		sprite.set_skeleton_data_res(data)
		sprite.normal_material = _pma_material
		sprite.additive_material = _pma_additive_material
		add_child(sprite)
		var state: SpineAnimationState = sprite.get_animation_state()
		if state != null:
			state.set_time_scale(0.0)
		var entry = state.set_animation("animation", false, 0) if state != null else null
		if entry == null:
			_fail("Failed Heidemarie gloom animation: " + stem)
		_fx_layers.append({
			"sprite": sprite,
			"stem": stem,
			"entry": entry,
			"visible": float(spec.visible),
			"host_visible": bool(spec.get("host_visible", true)),
		})


func _build_cg() -> void:
	var slots: Array = _source.get("slots", [])
	for i in range(slots.size()):
		var definition: Dictionary = slots[i]
		var slot := str(definition.get("name", ""))
		var bone := str(definition.get("bone", slot))
		var sprite := Sprite2D.new()
		sprite.name = "Cg_" + slot
		sprite.centered = true
		sprite.z_index = 50 + i
		sprite.z_as_relative = false
		if str(definition.get("blend", "")) == "additive":
			var material := CanvasItemMaterial.new()
			material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			sprite.material = material
		add_child(sprite)
		_cg_layers.append({
			"sprite": sprite,
			"slot": slot,
			"chain": _bone_chain(bone),
		})


func _load_cg(path: String) -> bool:
	var texture = ResourceLoader.load(
		path, "", ResourceLoader.CACHE_MODE_REUSE) as Texture2D
	if texture == null:
		_fail("Missing Heidemarie defeat CG: " + path)
		return false
	for layer_data in _cg_layers:
		(layer_data.sprite as Sprite2D).texture = texture
	_cg_content = CG_FRAMING.content_rect(texture)
	_cg_reference = CG_FRAMING.reference_pose(self, _cg_layers, DURATION * 0.5)
	return true


func _restart_fx() -> void:
	for layer_data in _fx_layers:
		var state: SpineAnimationState = layer_data.sprite.get_animation_state()
		if state != null:
			state.set_time_scale(0.0)
		layer_data.entry = state.set_animation(
			"animation", false, 0) if state != null else null


func _layout() -> void:
	var viewport := get_viewport().get_visible_rect().size
	_backdrop.position = Vector2.ZERO
	_backdrop.size = viewport
	_apply_frame(_clock)


func _apply_frame(time_s: float) -> void:
	if _source.is_empty():
		return
	var viewport := get_viewport().get_visible_rect().size
	if viewport.x <= 0.0 or viewport.y <= 0.0:
		return
	var center := viewport * 0.5
	var view_scale := viewport.y / FX_MOBILE_FRAME.y
	var cg_texture_height := _cg_texture_size().y
	var cg_view_scale := \
		viewport.y / cg_texture_height if cg_texture_height > 0.0 else view_scale
	var source_time := clampf(time_s, 0.0, DURATION)
	for layer_data in _fx_layers:
		var sprite: SpineSprite = layer_data.sprite
		sprite.position = center
		sprite.scale = Vector2.ONE * view_scale
		sprite.visible = (
			bool(layer_data.host_visible)
			and time_s <= float(layer_data.visible)
			and not _fx_disabled.has(str(layer_data.stem)))
		if layer_data.entry != null:
			layer_data.entry.set_track_time(source_time)
	var placement_scale := CFX_SCALE * cg_outer_scale
	# User-directed shared STS2 frame; reference pose is sampled without seeking playback.
	var reference_local := Vector2(
		-srue_offset.x + _cg_reference.position.x * placement_scale,
		srue_offset.y - _cg_reference.position.y * placement_scale)
	var content_offset: Vector2 = (_cg_content.get_center() - _cg_texture_size() * 0.5) * _cg_reference.scale * placement_scale * cg_view_scale
	var framing := CG_FRAMING.frame(viewport, center + reference_local * view_scale + content_offset,
		_cg_content.size * (_cg_reference.scale * placement_scale * cg_view_scale).abs())
	for layer_data in _cg_layers:
		var sprite: Sprite2D = layer_data.sprite
		if time_s > DURATION:
			sprite.visible = false
			continue
		var sampled := _sample_chain(layer_data.chain, source_time)
		var local := Vector2(
			-srue_offset.x + sampled.position.x * placement_scale,
			srue_offset.y - sampled.position.y * placement_scale)
		var color := _sample_color(str(layer_data.slot), source_time)
		sprite.position = framing * (center + local * view_scale)
		sprite.scale = sampled.scale * placement_scale * cg_view_scale * framing.x.x
		sprite.modulate = color
		sprite.visible = color.a > 0.001


func _sample_chain(chain: Array, time_s: float) -> Dictionary:
	var position := Vector2.ZERO
	var scale := Vector2.ONE
	for bone_name in chain:
		var local := _sample_bone(str(bone_name), time_s)
		position += local.position * scale
		scale *= local.scale
	return {"position": position, "scale": scale}


func _sample_bone(name: String, time_s: float) -> Dictionary:
	var definition: Dictionary = _bone_defs.get(name, {})
	var tracks: Dictionary = _anim_bones.get(name, {})
	var base_pos := Vector2(
		float(definition.get("x", 0.0)),
		float(definition.get("y", 0.0)))
	var base_scale := Vector2(
		float(definition.get("scaleX", 1.0)),
		float(definition.get("scaleY", 1.0)))
	return {
		"position": base_pos + _sample_pair(
			tracks.get("translate", []), time_s, Vector2.ZERO),
		"scale": base_scale * _sample_pair(
			tracks.get("scale", []), time_s, Vector2.ONE),
	}


func _sample_pair(value, time_s: float, setup: Vector2) -> Vector2:
	if typeof(value) != TYPE_ARRAY or value.is_empty():
		return setup
	var keys: Array = value
	if time_s < float(keys[0].get("time", 0.0)):
		return setup
	for i in range(1, keys.size()):
		var b: Dictionary = keys[i]
		if time_s < float(b.get("time", 0.0)):
			var a: Dictionary = keys[i - 1]
			var av := Vector2(
				float(a.get("x", setup.x)), float(a.get("y", setup.y)))
			var bv := Vector2(
				float(b.get("x", setup.x)), float(b.get("y", setup.y)))
			var span := maxf(
				0.000001,
				float(b.get("time", 0.0)) - float(a.get("time", 0.0)))
			return av.lerp(
				bv,
				_curve(a, (time_s - float(a.get("time", 0.0))) / span))
	var last: Dictionary = keys[-1]
	return Vector2(
		float(last.get("x", setup.x)),
		float(last.get("y", setup.y)))


func _sample_color(slot: String, time_s: float) -> Color:
	var setup := _rgba(str(_slot_defs.get(slot, {}).get("color", "FFFFFFFF")))
	var keys = _anim_slots.get(slot, {}).get("color", [])
	if typeof(keys) != TYPE_ARRAY or keys.is_empty():
		return setup
	if time_s < float(keys[0].get("time", 0.0)):
		return setup
	for i in range(1, keys.size()):
		var b: Dictionary = keys[i]
		if time_s < float(b.get("time", 0.0)):
			var a: Dictionary = keys[i - 1]
			var span := maxf(
				0.000001,
				float(b.get("time", 0.0)) - float(a.get("time", 0.0)))
			var k := _curve(
				a, (time_s - float(a.get("time", 0.0))) / span)
			return _rgba(str(a.color)).lerp(_rgba(str(b.color)), k)
	return _rgba(str(keys[-1].color))


func _curve(key: Dictionary, progress: float) -> float:
	progress = clampf(progress, 0.0, 1.0)
	var curve = key.get("curve")
	if curve == null:
		return progress
	if typeof(curve) == TYPE_STRING:
		return 0.0 if str(curve) == "stepped" else progress
	if typeof(curve) == TYPE_FLOAT or typeof(curve) == TYPE_INT:
		return _ease(
			progress,
			float(curve),
			float(key.get("c2", 0.0)),
			float(key.get("c3", 1.0)),
			float(key.get("c4", 1.0)))
	return progress


func _ease(progress: float, x1: float, y1: float, x2: float, y2: float) -> float:
	var parameter := progress
	for _i in range(8):
		var error := _axis(parameter, x1, x2) - progress
		if absf(error) < 0.00001:
			break
		var derivative := \
			3.0 * (1.0 - parameter) ** 2 * x1 \
			+ 6.0 * (1.0 - parameter) * parameter * (x2 - x1) \
			+ 3.0 * parameter ** 2 * (1.0 - x2)
		if absf(derivative) < 0.000001:
			break
		parameter = clampf(parameter - error / derivative, 0.0, 1.0)
	return _axis(parameter, y1, y2)


func _axis(t: float, p1: float, p2: float) -> float:
	var u := 1.0 - t
	return \
		3.0 * u * u * t * p1 \
		+ 3.0 * u * t * t * p2 \
		+ t * t * t


func _bone_chain(name: String) -> Array:
	var chain := []
	var seen := {}
	var current := name
	while not current.is_empty() and not seen.has(current):
		seen[current] = true
		chain.push_front(current)
		current = str(_bone_defs.get(current, {}).get("parent", ""))
	return chain


func _rgba(value: String) -> Color:
	if value.length() != 8:
		return Color.WHITE
	return Color(
		float(value.substr(0, 2).hex_to_int()) / 255.0,
		float(value.substr(2, 2).hex_to_int()) / 255.0,
		float(value.substr(4, 2).hex_to_int()) / 255.0,
		float(value.substr(6, 2).hex_to_int()) / 255.0)


func _cg_texture_size() -> Vector2:
	if _cg_layers.is_empty():
		return Vector2.ZERO
	var texture: Texture2D = (_cg_layers[0].sprite as Sprite2D).texture
	return texture.get_size() if texture != null else Vector2.ZERO


func _fail(message: String) -> void:
	_failed = true
	push_error(message)


func _finish_stage() -> void:
	if _finished:
		return
	_finished = true
	set_process(false)
	visible = false
	emit_signal("stage_finished")
	if not park_on_finish and not preview_manual:
		call_deferred("queue_free")
