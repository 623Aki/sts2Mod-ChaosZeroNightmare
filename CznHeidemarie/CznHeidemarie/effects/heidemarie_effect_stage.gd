extends Node2D

# Heidemarie 单体技能特效舞台。
#
# 由 `HeidemarieMoveEffectsRuntime` 实例化后挂到战斗房间的**前台特效容器**
# （`NCombatRoom.CombatVfxContainer`）下，而不是某个单位的骨架节点下。
# 根节点的世界变换（位置 / 缩放 / 镜像）由 C# 侧复刻目标单位的 `Body` 后设置；
# 本脚本只负责三件事：
#   1. 按 `heidemarie_moves.json` 解析本次 bucket 的图层行；
#   2. 逐层建 `SpineSprite`，用 .cfx 真值设 offset / scale / z；
#   3. 用自己的毫秒时钟驱动 start_ms / end_ms，播完发 `finished`。
#
# 因此本脚本**不再**读取父节点缩放 / 父节点 z，也不做锚点策略或归一。
# 释放责任在 C# 侧：`finished` → `QueueFreeSafely()` 释放整个根节点。
#
# 时间轴数据在 `res://CznHeidemarie/effects/heidemarie_moves.json`。

signal finished

## 特效骨架 `_skel_data.tres` 所在目录。
const SKEL_DATA_ROOT := "res://CznHeidemarie/animations/effects/"

## Godot 的 CanvasItem.z_index 只接受 [-4096, 4096]，越界会被直接拒绝；
## .cfx 真值里存在 100001 这种值，所以只做越界钳位，不做名次压缩。
const LAYER_Z_MIN := -4000
const LAYER_Z_MAX := 4000

## Spine 的混合模式枚举（4.x）：0 normal / 1 additive / 2 multiply / 3 screen。
const SPINE_BLEND_NORMAL := 0
const SPINE_BLEND_MULTIPLY := 2

var _layers: Array = []
var _duration_ms: float = 0.0
var _clock_ms: float = 0.0
var _epoch_usec: int = 0
var _playing: bool = false
var _done: bool = false


## 读时间轴并建节点，返回演出时长（秒）。返回 0 表示没有任何可用图层，调用方应直接释放本节点。
##
## data_path  : `res://CznHeidemarie/effects/heidemarie_moves.json`
## effect_key : 档位键，如 `AttackLight`
## bucket     : `owner`（施法者）或 `target`（目标）
## base_z     : 舞台根节点的有效 z（C# 侧复刻的 Body 有效 z；通常 -10）
## z_lift     : 目标侧整体抬升量；压缩后 min_z 已 ≥ 0，这里自然退化成 0
## part       : `front`（z ≥ 0，角色之前）/ `back`（z < 0，角色之后）/ `all`
##
## 分层原因：战斗画布里背景与角色同在 z = -10，任何负的相对 z 都会掉到背景后面。
## 因此负值层必须改挂 `BackCombatVfxContainer`（背景之后、角色之前，靠子树顺序排），
## 且在该容器里统一用 z = 0、由子节点插入顺序决定前后；正值层留在前台容器，按名次取 1..N。
func configure(data_path: String, effect_key: String, bucket: String, base_z: int = 0, z_lift: int = 0, part: String = "all") -> float:
	_layers.clear()
	_duration_ms = 0.0
	_clock_ms = 0.0
	_playing = false
	_done = false

	var rows: Array = _load_rows(data_path, effect_key, bucket)
	rows = _band_rows(rows, part)
	if rows.is_empty():
		return 0.0

	# 按 z 从小到大建，保证后建的盖在前面（同层内保持配置顺序）。
	# 必须自己带下标做稳定排序：后台分层的 z 全是 0，而 Godot 的 sort_custom
	# 不稳定，直接排会把层序打乱。
	var indexed: Array = []
	for index in rows.size():
		indexed.append({"z": int((rows[index] as Dictionary).get("z", 0)), "i": index, "row": rows[index]})
	indexed.sort_custom(func(a, b):
		if int(a["z"]) == int(b["z"]):
			return int(a["i"]) < int(b["i"])
		return int(a["z"]) < int(b["z"]))

	var sorted: Array = []
	for item in indexed:
		sorted.append(item["row"])

	# 旧版 target_z_lift 的算法：把最小 z 抬到「身体有效 z + lift」。
	# 舞台根节点已经是 body 的有效 z，所以图层再加上同一个 delta 即可；
	# 不能简单给每层 +4，否则重击 u1 的 -50 层会被压到背景后。
	var z_delta := 0
	if z_lift > 0 and not sorted.is_empty():
		z_delta = maxi(0, base_z + z_lift - int(sorted[0].get("z", 0)))

	for row in sorted:
		var entry: Dictionary = _build_layer(row, z_delta)
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


## 立即收尾（换房 / 玩家死亡 / 设置关闭）。只停时钟并隐藏图层；
## 节点本身由 C# 侧释放，避免脚本里再攒一层释放逻辑。
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


func _build_layer(row: Dictionary, z_delta: int) -> Dictionary:
	var layer_name := str(row.get("name", ""))
	if layer_name.is_empty():
		return {}

	var path: String = SKEL_DATA_ROOT + layer_name + "_skel_data.tres"
	if not ResourceLoader.exists(path):
		push_warning("[Heidemarie] 技能特效图层缺少骨架，已跳过：%s" % path)
		return {}

	var spine := SpineSprite.new()
	spine.name = layer_name
	# 必须在赋给 SpineSprite 之前改槽位数据：实例是按当时的数据建出来的。
	var skeleton_data: Resource = load(path)
	if skeleton_data == null:
		push_warning("[Heidemarie] 技能特效图层骨架加载失败，已跳过：%s" % path)
		return {}
	_fix_multiply_slots(skeleton_data)
	spine.skeleton_data_res = skeleton_data
	if spine.skeleton_data_res == null:
		push_warning("[Heidemarie] 技能特效图层骨架加载失败，已跳过：%s" % path)
		return {}

	add_child(spine)

	# 图层 offset / scale 都是 CZN 源数据的 Spine 局部单位；
	# 舞台根节点已经复刻了目标单位 Body 的全局基，所以这里不需要再乘父缩放。
	var offset := _to_vector2(row.get("offset", [0.0, 0.0]))
	var scale_xy := _to_vector2(row.get("scale", [1.0, 1.0]))
	# 骨架是 Spine 的 y 向上，Godot 是 y 向下，所以 y 取反。
	spine.position = Vector2(offset.x, -offset.y)
	spine.scale = scale_xy
	spine.rotation_degrees = float(row.get("rotation", 0.0))
	spine.modulate = Color(1.0, 1.0, 1.0, float(row.get("opacity", 1.0)))
	# 相对模式是对的：图层都挂在同一个舞台根节点下，舞台根节点的绝对 z
	# 已在 C# 侧设为目标 Body 的有效 z，于是正负层仍按 .cfx 真值分前后。
	spine.z_as_relative = true
	spine.z_index = clampi(int(row.get("z", 0)) + z_delta, LAYER_Z_MIN, LAYER_Z_MAX)

	var track = _start_clip(spine, str(row.get("clip", "")))
	if track == null:
		push_warning("[Heidemarie] 技能特效图层找不到可用 clip，已跳过：%s/%s"
			% [layer_name, str(row.get("clip", ""))])
		spine.queue_free()
		return {}

	spine.hide()
	return {
		"node": spine,
		"track": track,
		"start_ms": float(row.get("start_ms", 0.0)),
		"end_ms": float(row.get("end_ms", _duration_ms)),
	}


## 把 `multiply` 槽位降级成 `normal`。
##
## spine-godot 4.2 × Godot 4.5.1 会把 multiply 混合画成**纯黑方框**
## （premultiplied 贴图叠 Godot 的混合因子），而 CZN 自己渲染器里它是「压暗」层。
## 全库只有两个骨架用到 multiply，且都在顶击 u3 的目标侧：
##   heidemarie_30093_eff_u3_attack_target_b   → 槽位 cra（capri_unique1 333×333）
##   heidemarie_30093_eff_u3_attack_target_ff2 → 槽位 crack_s_1 / crack_s_2 / crack_s_3
func _fix_multiply_slots(skeleton_data: Resource) -> void:
	if skeleton_data == null or not skeleton_data.has_method("get_slots"):
		return
	for slot in skeleton_data.call("get_slots"):
		if slot == null or not slot.has_method("get_blend_mode"):
			continue
		if int(slot.call("get_blend_mode")) == SPINE_BLEND_MULTIPLY:
			slot.call("set_blend_mode", SPINE_BLEND_NORMAL)


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

	# compose 形态（自组装组）：组自带 owner / target 两侧，这里按 bucket 取对应侧。
	var compose: Variant = entry.get("compose")
	if typeof(compose) == TYPE_ARRAY and not (compose as Array).is_empty():
		return _expand_compose(compose, bucket)

	var rows: Variant = entry.get(bucket)
	if typeof(rows) != TYPE_ARRAY:
		return []

	var source: Array = rows
	if source.is_empty():
		return []

	# 两代 schema 都接受：
	#   v3（旧）：bucket 里直接是图层行 { name, clip, z, start_ms, end_ms, … }
	#   v4（新）：bucket 里是「特效组键」{ group, command, start_ms }，在这里展开
	var expanded: Array = []
	for neutral in HeidemarieEffectGroups.expand_rows(source):
		if not (neutral is Dictionary):
			continue
		var row: Dictionary = neutral
		if not row.has("offset_x"):
			# v3 的原始行，原样带走。
			expanded.append(row)
			continue
		# 中立行 → 本舞台的形状：offset 与 scale 都用 [x, y] 数组。
		expanded.append({
			"name": row["name"],
			"clip": row["clip"],
			"z": row["z"],
			"offset": [row["offset_x"], row["offset_y"]],
			"scale": [row["scale"], row["scale"]],
			"rotation": row["rotation"],
			"opacity": row["opacity"],
			"start_ms": row["start_ms"],
			"end_ms": row["end_ms"],
		})
	return expanded


## 把 entry 的 `compose` 数组展开成本舞台的图层行。
##
## compose 项 = { group: 组名, start_ms: 起播时刻 }；组构成在自组装组库里，
## 一个组自带 owner / target 两侧，这里按本次 bucket 取对应侧。
func _expand_compose(compose: Array, bucket: String) -> Array:
	var out: Array = []
	for entry_key in compose:
		if not (entry_key is Dictionary):
			continue
		var item: Dictionary = entry_key
		var group_name := str(item.get("group", ""))
		if group_name.is_empty():
			continue
		var group: Dictionary = HeidemarieEffectCompose.get_group(group_name)
		if group.is_empty():
			push_warning("[Heidemarie] 自组装组库没有这个特效组，已跳过：%s" % group_name)
			continue
		var base_ms := float(item.get("start_ms", 0.0))
		for neutral in HeidemarieEffectCompose.expand(group, bucket, base_ms):
			out.append({
				"name": neutral["name"],
				"clip": neutral["clip"],
				"z": neutral["z"],
				"offset": [neutral["offset_x"], neutral["offset_y"]],
				"scale": [neutral["scale"], neutral["scale"]],
				"rotation": neutral["rotation"],
				"opacity": neutral["opacity"],
				"start_ms": neutral["start_ms"],
				"end_ms": neutral["end_ms"],
			})
	return out


## 按 z 正负把图层分成前台 / 后台两路，并把 z 换成该容器里安全的取值。
##
## compose 的 `expand()` 已经压过一次（负值 -N..-1、非负 1..M），这里只做分流与落地：
##   front：保留 1..N（相对舞台根节点的正值 → 落在角色之上）
##   back ：统一 0，靠 `add_child` 顺序决定层序（该容器里 z=0 正好是「背景之上、角色之下」）
##   all  ：两路合在一起，旧行为兜底（负值也归 0，至少不会再消失）
func _band_rows(rows: Array, part: String) -> Array:
	var front: Array = []
	var back: Array = []
	for index in rows.size():
		var row: Variant = rows[index]
		if not (row is Dictionary):
			continue
		var item := {
			"z": float((row as Dictionary).get("z", 0.0)),
			"i": index,
			"row": row,
		}
		if float(item["z"]) < 0.0:
			back.append(item)
		else:
			front.append(item)

	# 稳定排序：z 相同时按来源顺序（Godot 的 sort_custom 不稳定）
	var byz := func(a, b):
		if float(a["z"]) == float(b["z"]):
			return int(a["i"]) < int(b["i"])
		return float(a["z"]) < float(b["z"])
	back.sort_custom(byz)
	front.sort_custom(byz)

	var out: Array = []
	if part != "front":
		for item in back:
			var back_row: Dictionary = (item["row"] as Dictionary).duplicate()
			back_row["z"] = 0
			out.append(back_row)
	if part != "back":
		var rank := 1
		for item in front:
			var front_row: Dictionary = (item["row"] as Dictionary).duplicate()
			front_row["z"] = rank
			rank += 1
			out.append(front_row)
	return out


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
