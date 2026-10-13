extends CanvasLayer

# Heidemarie「全屏演出」合成器。
#
# 与参考 mod 的 `nine_cinematic_stage.gd` / Tiphera 的 `combat_ug_overlay.tscn` 同一套架构：
# 把整个战斗画面接管过来，按毫秒时钟推进一批互相独立的 Spine 特效骨架，
# 再叠上黑幕、镜头抖动与背景层，最终由角色侧的 C# 运行时在正确的时机放行伤害结算。
#
# 三条硬约定（改之前先想清楚）：
#   1) 时钟是自己走的（`Time.get_ticks_usec()` 差值），不依赖 AnimationPlayer；
#      每个图层只做 set_track_time + update_skeleton，图层可以被任意裁剪。
#   2) SCREEN 层不受镜头影响，STAGE 层受镜头抖动影响；两者同在一个 canvas 下保持 z 序交织。
#   3) 任何一层骨架缺失都只丢这一层（`_build_layer` 返回空字典），
#      但整段演出一个可用图层都没有时必须让 `configure` 返回 false，交回原生出牌演出。

signal presentation_started
signal hit_reached
signal actors_restored
signal stage_finished
signal resources_finished

const DESIGN_WIDTH := 1280.0
const DESIGN_HEIGHT := 720.0
const DESIGN_SIZE := Vector2(DESIGN_WIDTH, DESIGN_HEIGHT)

## 时间轴与骨架所在目录，路径由 mod id 推导，换 mod 只改这两行。
const TIMELINE_ROOT := "res://CznHeidemarie/cinematic/"
const SKEL_DATA_ROOT := "res://CznHeidemarie/animations/effects/"

## 接管层：远高于战斗 HUD，保证不会被任何界面挡住。
const TAKEOVER_LAYER := 4090

## 黑幕的绝对深度，必须低于所有图层。
##
## Godot 的 CanvasItem.z_index 合法区间就是 [-4096, 4096]，越界**不是截断**：
## set_z_index 会 ERR_FAIL 直接返回，值保持原样（0），黑幕就跑到中间层，
## 把一半深度的图层压黑（日志里那条
## `Condition "p_z < RenderingServer::CANVAS_ITEM_Z_MIN" is true` 就是它）。
## 所以这里取区间下界本身 —— 取更小的 -30000（.cfx 真值最底层是 -10000）
## 反而会踩这个坑。
const MATTE_Z_INDEX := -4096

## 图层 z 的落点区间。
##
## 原游戏的 .cfx 深度是真值：ux_attack 最深到 -10000，
## u3_attack_target_flash 组更是 zorder=100001，两者都超出 Godot 的
## [-4096, 4096]。直接照搬同样会被 set_z_index 拒绝，一整片底层背景会一起停在 0，
## 层级又乱了 —— 参考 mod 的 `z_index = index - 100` 正是这个保序压缩，
## 只是它的输入 z 是编的，压缩谁都一样错。
##
## 所以：JSON 里留 .cfx 原值（便于复核），进 Godot 前按**名次**线性映射进
## [LAYER_Z_MIN, LAYER_Z_MAX]。名次映射严格保序，同 z 仍并列，
## 且下界 -4000 高于黑幕的 -4096，黑幕永远在最底下。
const LAYER_Z_MIN := -4000
const LAYER_Z_MAX := 4000

## 时钟用（毫秒）。
var _clock_ms: float = 0.0
var _epoch_usec: int = 0

## 生命周期。
var _route: String = ""
var _prepared: bool = false
var _starting: bool = false
var _playing: bool = false
var _visuals_finished: bool = false
var _resources_released: bool = false

## 节点。
var _canvas: Node2D = null
var _stage: Node2D = null
var _screen: Node2D = null
var _matte: ColorRect = null

## 时间轴解析结果。
var _layers: Array = []
var _shakes: Array = []
var _bg: Dictionary = {}
var _duration_ms: float = 0.0
var _hit_ms: float = -1.0
var _actor_restore_ms: float = -1.0
var _presentation_end_ms: float = 0.0

## bone_type=ATTACH 却拿不到挂点的图层（srmd 的 slot/bone 为空）。
## 不阻断演出，只在 configure 结束时汇总成一条告警，免得刷屏。
var _attach_without_anchor: Dictionary = {}

## 时间轴 z 值 -> 压进 Godot 合法区间的 z 值。见 LAYER_Z_MIN 注释。
var _z_ranks: Dictionary = {}

## 一次性信号闸门。
var _hit_sent: bool = false
var _actors_released: bool = false
var _presentation_finished: bool = false


# ──────────────────────────────────────────────────────────────────────────────
# 对外契约（C# 运行时按名字调用，改动要同步 HeidemarieCinematicRuntime）
# ──────────────────────────────────────────────────────────────────────────────

## 挂载后配置：读时间轴、建节点、把 owner 位置换算进设计画布。
## 返回 false 表示资源或时序不齐，调用方必须放弃这次全屏演出（走原生出牌演出）。
func configure(route_id: String, owner: Node2D, owner_position: Vector2) -> bool:
	if _prepared:
		return false

	_route = route_id
	var timeline := _load_timeline(route_id)
	if timeline.is_empty():
		return false

	layer = TAKEOVER_LAYER
	_canvas = Node2D.new()
	_canvas.name = "CinematicCanvas"
	add_child(_canvas)
	_layout()
	get_viewport().size_changed.connect(_layout)

	# 黑色遮罩：负责把战斗画面压掉，形成「接管」感。
	# z 必须压到所有图层之下。ux_attack 的最底层来自 .cfx，真值到 -10000，
	# 所以这里取 -30000，并且关掉 z_as_relative —— 否则它会叠在 _canvas(0) 之上，
	# 把整段特效盖住。
	_matte = ColorRect.new()
	_matte.name = "CinematicMatte"
	_matte.position = Vector2.ZERO
	_matte.size = DESIGN_SIZE
	_matte.color = Color(0.0, 0.0, 0.0, 0.0)
	_matte.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_matte.z_as_relative = false
	_matte.z_index = MATTE_Z_INDEX
	_canvas.add_child(_matte)

	# 世界层：受镜头抖动影响（地面、剑光、命中特效）。
	_stage = Node2D.new()
	_stage.name = "CinematicStage"
	_stage.position = DESIGN_SIZE * 0.5
	_canvas.add_child(_stage)

	# 屏幕层：不受镜头抖动影响（全屏闪白、背景、cut-in 立绘）。
	_screen = Node2D.new()
	_screen.name = "CinematicScreen"
	_screen.position = DESIGN_SIZE * 0.5
	_canvas.add_child(_screen)

	var owner_local := Vector2.ZERO
	if is_instance_valid(owner):
		var cover: float = _canvas.scale.x
		owner_local = (owner_position - _canvas.position) / cover - DESIGN_SIZE * 0.5

	_bg = timeline.get("bg", {})
	_duration_ms = float(timeline.get("duration_ms", 0.0))
	_hit_ms = float(timeline.get("hit_ms", -1.0))
	_actor_restore_ms = float(timeline.get("actor_restore_ms", -1.0))
	_presentation_end_ms = float(timeline.get("presentation_end_ms", _duration_ms))

	for row in timeline.get("shakes", []):
		_shakes.append({
			"start_ms": float(row.get("start_ms", 0.0)),
			"duration_ms": maxf(1.0, float(row.get("duration_ms", 200.0))),
			"amplitude": float(row.get("amplitude", 10.0)),
			"frequency": float(row.get("frequency", 30.0)),
			"angle": deg_to_rad(float(row.get("angle", 0.0))),
		})

	# 按 z 从小到大添加，保证后建的盖在前面。
	# z 相同（.cfx 里两个不同阶段共用同一深度）时退回原序，避免排序不稳定。
	var rows: Array = timeline.get("layers", [])
	var order: Array = rows.duplicate()
	for i in order.size():
		order[i] = order[i].duplicate()
		order[i]["__order"] = i
	order.sort_custom(func(a, b):
		var za := int(a.get("z", 0))
		var zb := int(b.get("z", 0))
		if za == zb:
			return int(a["__order"]) < int(b["__order"])
		return za < zb)

	# 把 .cfx 的真值深度按名次压进 Godot 的合法区间（见 LAYER_Z_MIN 注释）。
	_z_ranks = _build_z_ranks(order)

	var index := 0
	for row in order:
		var entry: Dictionary = _build_layer(row, index, owner_local)
		index += 1
		if not entry.is_empty():
			_layers.append(entry)

	if _layers.is_empty():
		push_error("[Heidemarie] 全屏演出没有任何可用图层：%s" % _route)
		return false
	if _hit_ms < 0.0 or _actor_restore_ms < 0.0 or _duration_ms <= 0.0:
		push_error("[Heidemarie] 全屏演出缺少时序契约：%s" % _route)
		return false
	if not _attach_without_anchor.is_empty():
		push_warning("[Heidemarie] 全屏演出有 %d 层缺少 ATTACH 挂点（srmd 的 slot/bone 为空），已按绝对坐标渲染：%s"
			% [_attach_without_anchor.size(), str(_attach_without_anchor.keys())])

	_apply(0.0)
	_canvas.hide()
	_prepared = true
	return true


## 返回时序契约，供 C# 侧做单调性校验（与参考 mod 的 get_runtime_contract 等价）。
func get_runtime_contract() -> Dictionary:
	return {
		"duration_seconds": _duration_ms / 1000.0,
		"hit_release_seconds": _hit_ms / 1000.0,
		"battlefield_actor_release_seconds": _actor_restore_ms / 1000.0,
	}


## 开始演出。走两帧预绘屏障后建立时钟，避免纹理上传 / shader 编译砸在第一帧上。
func start_stage() -> bool:
	if not _prepared or _playing or _starting:
		return false
	_starting = true
	_canvas.show()
	_begin_clock.call_deferred()
	return true


## 立即静默收尾（换卡 / 死亡 / 离房 / 设置变更）。
func cancel_stage() -> void:
	_starting = false
	_playing = false
	_visuals_finished = true
	_resources_released = true
	set_process(false)
	if is_instance_valid(_canvas):
		_canvas.hide()


func set_paused(paused: bool) -> void:
	if not _prepared or _starting or _visuals_finished:
		return
	if paused:
		_playing = false
	else:
		_epoch_usec = Time.get_ticks_usec() - int(_clock_ms * 1000.0)
		_playing = true


# ──────────────────────────────────────────────────────────────────────────────
# 内部实现
# ──────────────────────────────────────────────────────────────────────────────

func _begin_clock() -> void:
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	if not _starting or not is_inside_tree():
		return
	_starting = false
	_playing = true
	_clock_ms = 0.0
	_epoch_usec = Time.get_ticks_usec()
	presentation_started.emit()


func _process(_delta: float) -> void:
	if not _playing:
		return

	_clock_ms = float(Time.get_ticks_usec() - _epoch_usec) / 1000.0
	_apply(_clock_ms)

	if not _hit_sent and _clock_ms >= _hit_ms:
		_hit_sent = true
		hit_reached.emit()
		if not _playing:
			return

	if not _actors_released and _clock_ms >= _actor_restore_ms:
		_actors_released = true
		actors_restored.emit()
		if not _playing:
			return

	if not _presentation_finished and _clock_ms >= _presentation_end_ms:
		_presentation_finished = true
		stage_finished.emit()
		if not _playing:
			return

	if _presentation_finished and _clock_ms >= _duration_ms:
		_finish_visuals()


func _finish_visuals() -> void:
	if _visuals_finished:
		return
	_visuals_finished = true
	_playing = false
	set_process(false)
	if is_instance_valid(_canvas):
		_canvas.hide()
	if not _resources_released:
		_resources_released = true
		resources_finished.emit()


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
	_apply_shake(ms)
	_apply_matte(ms)


func _apply_shake(ms: float) -> void:
	if not is_instance_valid(_stage):
		return
	var shake := Vector2.ZERO
	for s in _shakes:
		var elapsed: float = ms - float(s["start_ms"])
		if elapsed < 0.0 or elapsed >= float(s["duration_ms"]):
			continue
		var decay: float = 1.0 - elapsed / float(s["duration_ms"])
		var phase: float = elapsed / 1000.0 * float(s["frequency"]) * TAU
		var amount: float = float(s["amplitude"]) * decay
		shake += Vector2(sin(phase), cos(phase * 1.37)) * amount
		if not is_zero_approx(float(s["angle"])):
			shake = shake.rotated(float(s["angle"]))
	_stage.position = DESIGN_SIZE * 0.5 + shake


## 把 .cfx 的真值深度按名次压进 [LAYER_Z_MIN, LAYER_Z_MAX]。
##
## 严格保序：不同 z 一定映射到不同值且大小关系不变；相同 z 映射到同一个值（保持并列）。
## Godot 只接受 ±4096 以内的 z，这里主动压到 ±4000，让黑幕的 -4096 稳在最底。
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


func _apply_matte(ms: float) -> void:
	if not is_instance_valid(_matte):
		return
	var peak := float(_bg.get("alpha", 1.0))
	var fade_in := float(_bg.get("fade_in_ms", 0.0))
	var fade_out_at := float(_bg.get("fade_out_at_ms", -1.0))
	var fade_out := float(_bg.get("fade_out_ms", 0.0))
	var alpha := peak
	if fade_in > 0.0 and ms < fade_in:
		alpha = peak * clampf(ms / fade_in, 0.0, 1.0)
	elif fade_out_at >= 0.0 and ms >= fade_out_at:
		alpha = 0.0 if fade_out <= 0.0 else peak * clampf(1.0 - (ms - fade_out_at) / fade_out, 0.0, 1.0)
	_matte.color = Color(0.0, 0.0, 0.0, alpha)


func _build_layer(row: Dictionary, index: int, owner_local: Vector2) -> Dictionary:
	var layer_name := str(row.get("name", ""))
	if layer_name.is_empty():
		return {}

	var path: String = SKEL_DATA_ROOT + layer_name + "_skel_data.tres"
	if not ResourceLoader.exists(path):
		push_warning("[Heidemarie] 全屏演出图层缺少骨架，已跳过：%s" % path)
		return {}

	var spine := SpineSprite.new()
	spine.name = layer_name
	spine.skeleton_data_res = load(path)
	if spine.skeleton_data_res == null:
		push_warning("[Heidemarie] 全屏演出图层骨架加载失败，已跳过：%s" % path)
		return {}

	var attach := str(row.get("target", "SCREEN"))
	var host: Node2D = _screen if attach == "SCREEN" else _stage
	host.add_child(spine)

	# 深度用时间轴里的 z —— 那是 .cfx 的 primitive_z + srmd 的 group_zorder，原游戏真值。
	# z_as_relative 必须关掉：_stage 和 _screen 是兄弟子树，默认相对模式下
	# STAGE 的所有图层会整体画在 SCREEN 的所有图层之前，z 再大也压不上去，
	# 于是「前后景交织」直接退化成两段各自排序 —— 那正是层级错乱的第二个来源。
	spine.z_as_relative = false
	var authored_z = row.get("z", null)
	if authored_z == null:
		# 时间轴没写 z 才退回排序名次，并留下痕迹便于发现数据缺失。
		# 仍要夹进合法区间：z_index 越界会被 set_z_index 直接拒绝（值停在上一次赋值上）。
		push_warning("[Heidemarie] 全屏演出图层缺少 z，回退到排序名次：%s" % layer_name)
		spine.z_index = clampi(index, LAYER_Z_MIN, LAYER_Z_MAX)
	else:
		spine.z_index = int(_z_ranks.get(int(authored_z), 0))

	var offset := _to_vector2(row.get("offset", [0.0, 0.0]))
	var scale_xy := _to_vector2(row.get("scale", 1.0))
	# 官方骨架是 Spine 的 y 向上，Godot 是 y 向下，所以 y 取反。
	spine.position = Vector2(offset.x, -offset.y)
	if attach == "OWNER":
		spine.position += owner_local
	spine.scale = scale_xy
	spine.rotation_degrees = float(row.get("rotation", 0.0))
	spine.modulate = Color(1.0, 1.0, 1.0, float(row.get("opacity", 1.0)))

	# bone_type=ATTACH 但 srmd 的 slot/bone 都为空时挂点不可还原，按绝对坐标渲染。
	if str(row.get("bone_type", "")) == "ATTACH":
		_attach_without_anchor[layer_name] = true

	var track = _start_clip(spine, str(row.get("clip", "")))
	if track == null:
		push_warning("[Heidemarie] 全屏演出图层找不到可用 clip，已跳过：%s" % layer_name)
		spine.queue_free()
		return {}

	spine.hide()
	return {
		"node": spine,
		"track": track,
		"start_ms": float(row.get("start_ms", 0.0)),
		"end_ms": float(row.get("end_ms", _duration_ms)),
	}


## 把图层骨架摁在 0 时刻并冻结时间轴，之后由 `_apply` 逐帧给时间。
func _start_clip(spine: SpineSprite, preferred: String):
	var state = spine.get_animation_state()
	if state == null:
		return null

	var candidates: Array = []
	if not preferred.is_empty():
		candidates.append(preferred)
	# 30093 的特效骨架统一只有一个名为 animation 的动画；
	# 少数带第二套 animation_nocam，留作降级。
	for fallback in ["animation", "animation_nocam", "loop", "idle"]:
		if not candidates.has(fallback):
			candidates.append(fallback)

	for clip in candidates:
		var track = state.set_animation(clip, false, 0)
		if track != null:
			track.set_mix_duration(0.0)
			track.set_time_scale(0.0)
			return track
	return null


func _load_timeline(route_id: String) -> Dictionary:
	var path: String = TIMELINE_ROOT + "heidemarie_" + route_id + ".json"
	if not FileAccess.file_exists(path):
		push_error("[Heidemarie] 缺少全屏演出时间轴：%s" % path)
		return {}

	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		push_error("[Heidemarie] 全屏演出时间轴为空：%s" % path)
		return {}

	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("[Heidemarie] 全屏演出时间轴解析失败：%s" % path)
		return {}
	return parsed


## 把 1280x720 的设计画布按 cover 方式铺满视口，并居中。
func _layout() -> void:
	if not is_instance_valid(_canvas):
		return
	var size: Vector2 = get_viewport().get_visible_rect().size
	if size.x <= 0.0 or size.y <= 0.0:
		return
	var cover: float = maxf(size.x / DESIGN_WIDTH, size.y / DESIGN_HEIGHT)
	_canvas.scale = Vector2.ONE * cover
	_canvas.position = (size - DESIGN_SIZE * cover) * 0.5


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
