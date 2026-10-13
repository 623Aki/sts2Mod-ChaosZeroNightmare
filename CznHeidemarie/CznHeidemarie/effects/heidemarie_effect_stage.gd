extends Node2D

# Heidemarie 单体技能特效舞台。
#
# 与「全屏演出」(`heidemarie_cinematic_stage.gd`) 同一套毫秒时间轴思路，区别在于：
#   - 本节点是 Node2D，**挂到某个战斗单位自己的骨架节点下**，因此自动继承该单位的
#     位置、朝向与缩放 —— 不需要任何坐标换算，也不需要跟着镜头走；
#   - 没有黑幕、没有镜头抖动、不接管画面，纯粹是叠在角色/目标身上的一批特效骨架。
#
# 一个舞台只演一个锚点（施法者或目标）的一组图层；卡牌两侧都有特效时，
# 由 `HeidemarieMoveEffectsRuntime` 各建一个舞台，各自挂到对应的单位上。
#
# 时间轴数据在 `res://CznHeidemarie/effects/heidemarie_moves.json`。

signal finished

## 特效骨架 `_skel_data.tres` 所在目录。
const SKEL_DATA_ROOT := "res://CznHeidemarie/animations/effects/"

## z 的落点区间。Godot 的 CanvasItem.z_index 只接受 [-4096, 4096]，
## 越界会被 set_z_index 直接拒绝（不是截断），而 .cfx 真值里
## u3_attack_target_flash 组的 zorder 就是 100001。
## 这里按名次线性压进该区间：严格保序，同 z 仍并列。
## 相对模式是对的——本舞台所有图层都是同一个 Node2D 的直接子节点，
## 舞台自身挂在战斗单位的骨架下，相对 z 会叠加到单位的深度上。
const LAYER_Z_MIN := -4000
const LAYER_Z_MAX := 4000

var _layers: Array = []
var _duration_ms: float = 0.0
var _clock_ms: float = 0.0
var _epoch_usec: int = 0
var _playing: bool = false
var _done: bool = false
var _z_ranks: Dictionary = {}


## 读时间轴并建节点，返回演出时长（秒）。返回 0 表示没有任何可用图层，调用方应直接释放本节点。
##
## 时间轴由本脚本自己解析 —— C# 侧只给路径与键，不必在托管代码里拼 Godot 的 Array/Dictionary。
##   data_path  : `res://CznHeidemarie/effects/heidemarie_moves.json`
##   effect_key : 卡牌的 C# 类型名，如 `HeidemarieStrike`
##   bucket     : `owner`（施法者）或 `target`（目标）
func configure(data_path: String, effect_key: String, bucket: String) -> float:
	var rows: Array = _load_rows(data_path, effect_key, bucket)
	if rows.is_empty():
		return 0.0

	var index := 0
	# 按 z 从小到大建，保证后建的盖在前面（同层内保持配置顺序）
	var sorted: Array = rows.duplicate()
	sorted.sort_custom(func(a, b): return int(a.get("z", 0)) < int(b.get("z", 0)))
	_z_ranks = _build_z_ranks(sorted)

	for row in sorted:
		var entry: Dictionary = _build_layer(row, index)
		index += 1
		if entry.is_empty():
			continue
		_layers.append(entry)
		_duration_ms = maxf(_duration_ms, float(entry["end_ms"]))

	if _layers.is_empty():
		return 0.0

	_apply(0.0)
	set_process(false)
	return _duration_ms / 1000.0


## 开始计时播放。时长走完后自动隐藏并发出 `finished`。
func start() -> void:
	if _playing or _done or _layers.is_empty():
		return
	_playing = true
	_clock_ms = 0.0
	_epoch_usec = Time.get_ticks_usec()
	set_process(true)


## 立即收尾（换卡 / 单位死亡 / 房间切换）。
func cancel() -> void:
	_playing = false
	set_process(false)
	for entry in _layers:
		var node: SpineSprite = entry["node"]
		if is_instance_valid(node):
			node.hide()


func _process(_delta: float) -> void:
	if not _playing:
		return

	_clock_ms = float(Time.get_ticks_usec() - _epoch_usec) / 1000.0
	_apply(_clock_ms)

	if _clock_ms >= _duration_ms:
		_finish()


func _finish() -> void:
	if _done:
		return
	_done = true
	_playing = false
	set_process(false)
	for entry in _layers:
		var node: SpineSprite = entry["node"]
		if is_instance_valid(node):
			node.hide()
	finished.emit()


func _apply(ms: float) -> void:
	for entry in _layers:
		var node: SpineSprite = entry["node"]
		if not is_instance_valid(node):
			continue
		var visible: bool = ms >= float(entry["start_ms"]) and ms < float(entry["end_ms"])
		node.visible = visible
		if visible:
			var track = entry["track"]
			if track != null:
				track.set_track_time(maxf(0.0, (ms - float(entry["start_ms"])) / 1000.0))
			node.update_skeleton(0.0)


func _build_layer(row: Dictionary, index: int) -> Dictionary:
	var layer_name := str(row.get("name", ""))
	if layer_name.is_empty():
		return {}

	var path: String = SKEL_DATA_ROOT + layer_name + "_skel_data.tres"
	if not ResourceLoader.exists(path):
		push_warning("[Heidemarie] 技能特效图层缺少骨架，已跳过：%s" % path)
		return {}

	var spine := SpineSprite.new()
	spine.name = layer_name
	spine.skeleton_data_res = load(path)
	if spine.skeleton_data_res == null:
		push_warning("[Heidemarie] 技能特效图层骨架加载失败，已跳过：%s" % path)
		return {}

	add_child(spine)

	var offset := _to_vector2(row.get("offset", [0.0, 0.0]))
	var scale_xy := _to_vector2(row.get("scale", [1.0, 1.0]))
	# 骨架是 Spine 的 y 向上，Godot 是 y 向下，所以 y 取反。
	spine.position = Vector2(offset.x, -offset.y)
	spine.scale = scale_xy
	spine.rotation_degrees = float(row.get("rotation", 0.0))
	spine.modulate = Color(1.0, 1.0, 1.0, float(row.get("opacity", 1.0)))
	spine.z_as_relative = true
	spine.z_index = int(_z_ranks.get(int(row.get("z", 0)), 0))

	var track = _start_clip(spine, str(row.get("clip", "")))
	if track == null:
		push_warning("[Heidemarie] 技能特效图层找不到可用 clip，已跳过：%s/%s" % [layer_name, str(row.get("clip", ""))])
		spine.queue_free()
		return {}

	spine.hide()
	return {
		"node": spine,
		"track": track,
		"start_ms": float(row.get("start_ms", 0.0)),
		"end_ms": float(row.get("end_ms", _duration_ms)),
	}


## 把 .cfx 的真值深度按名次压进 [LAYER_Z_MIN, LAYER_Z_MAX]。严格保序，同 z 并列。
func _build_z_ranks(rows: Array) -> Dictionary:
	var unique_z: Array = []
	for row in rows:
		var z := int(row.get("z", 0))
		if not unique_z.has(z):
			unique_z.append(z)
	unique_z.sort()

	var ranks: Dictionary = {}
	var count := unique_z.size()
	if count == 0:
		return ranks
	if count == 1:
		ranks[unique_z[0]] = LAYER_Z_MIN
		return ranks

	var span := float(LAYER_Z_MAX - LAYER_Z_MIN)
	for i in count:
		ranks[unique_z[i]] = int(round(LAYER_Z_MIN + span * float(i) / float(count - 1)))
	return ranks


## 把图层摁在 0 时刻并冻结时间轴，之后由 `_apply` 逐帧给时间。
func _start_clip(spine: SpineSprite, preferred: String):
	var state = spine.get_animation_state()
	if state == null:
		return null

	var candidates: Array = []
	if not preferred.is_empty():
		candidates.append(preferred)
	for fallback in ["animation", "ready", "play", "setup"]:
		if not candidates.has(fallback):
			candidates.append(fallback)

	for clip in candidates:
		var track = state.set_animation(clip, false, 0)
		if track != null:
			track.set_mix_duration(0.0)
			track.set_time_scale(0.0)
			return track
	return null


## 读取 `effects[effect_key][bucket]` 的图层表；任何一环缺失都返回空数组（= 不演）。
func _load_rows(data_path: String, effect_key: String, bucket: String) -> Array:
	if not FileAccess.file_exists(data_path):
		push_error("[Heidemarie] 缺少技能特效时间轴：%s" % data_path)
		return []

	var text := FileAccess.get_file_as_string(data_path)
	if text.is_empty():
		push_error("[Heidemarie] 技能特效时间轴为空：%s" % data_path)
		return []

	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("[Heidemarie] 技能特效时间轴解析失败：%s" % data_path)
		return []

	var effects: Variant = parsed.get("effects")
	if typeof(effects) != TYPE_DICTIONARY:
		return []

	var entry: Variant = effects.get(effect_key)
	if typeof(entry) != TYPE_DICTIONARY:
		return []

	var rows: Variant = entry.get(bucket)
	if typeof(rows) != TYPE_ARRAY:
		return []
	return rows


## 同时接受 [x, y] 数字数组与单个数字。
func _to_vector2(value) -> Vector2:
	if value is Array:
		var arr: Array = value
		if arr.size() >= 2:
			return Vector2(float(arr[0]), float(arr[1]))
		if arr.size() == 1:
			return Vector2(float(arr[0]), float(arr[0]))
		return Vector2.ONE
	if value is float or value is int:
		var f := float(value)
		return Vector2(f, f)
	return Vector2.ONE
