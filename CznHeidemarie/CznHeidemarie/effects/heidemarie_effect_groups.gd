class_name HeidemarieEffectGroups
extends RefCounted

# Heidemarie 特效组库 · 共享展开器。
#
# 组库由 `tools/list_effect_groups.py` 从 CZN 源数据生成，**不要手改**。
# 卡牌映射（`heidemarie_moves.json`）与全屏演出时间轴（`heidemarie_{ug,ux}.json`）
# 都只写「组键 + 起播时刻」，图层构成一律从这里取 —— 层只存一份，抄不歪。
#
# 三个舞台（单体特效 / 全屏演出 / 出牌特写）都调本类的 `expand_key`，
# 再各自把中立行映射成自己需要的形状（单体舞台用 [scale, scale]，
# 全屏舞台用单个 float，等等）。
#
# 展开规则（与 CZN 源数据的语义一一对应）：
#   起播 = 组键给的 start_ms（已含动作链累计偏移）+ 挂载 delay + 组内该层 delay
#   深度 = 挂载 zorder + .cfx primitive z   ← 这就是源的 final_z
#   偏移 = 挂载 offset_xy + 组内该层 (x, y)
#   缩放 = 挂载 scale × 组内该层 scale
#   不透明 = 挂载 opacity / 255
#
# **粒度分两档**（由 key 自带的字段决定，两档都要支持）：
#   ① 只有 `group`：把整组展开 —— `heidemarie_moves.json` 的 owner / target 就是这一档。
#   ② `group` + `name`：只展开组内 `source == name` 的那一层 ——
#      `heidemarie_{ug,ux}.json` 瘦身后的图层项属于这一档（同一组里逐层登记了
#      不同骨架名）。**不加这层过滤，一个组键会把自己那一组全量重复展开**，
#      组内必然出现重复记录，舞台的 `seen` 去重会判成「重复组装」→ 整条路线降级，
#      表现就是「全屏演出一路都放不出来」。

## 组库路径。
const LIBRARY_PATH := "res://CznHeidemarie/effects/heidemarie_effect_groups.json"

## 组库里量不到 clip 时长时的兜底（`.cfx` 指向静态姿势，没有任何关键帧）。
const FALLBACK_CLIP_MS := 1000.0

## 目标特效组受击点偏移兜底。
##
## 重(u1) / 顶(u3) 档的目标特效组在 CZN 源数据里缺失每层的 (x, y) 受击点偏移，
## 导致最终落点落在挂载原点（敌人脚下/身后），表现为「落点错位 / 在背景后」。
## 这里按组键强制把挂载偏移对齐到与轻/中档目标组一致的最终落点 (-620, -50)。
## 即便 `effect_groups.json` 被生成脚本重新生成、偏移被刷回 "0,0"，此处仍强制生效，
## 相当于把偏移「回写」到了不会被覆盖的持久一侧。
## （生成脚本 `tools/list_effect_groups.py` 不在本仓库内，故在此处固化。）
const TARGET_OFFSET_OVERRIDES := {
	"heidemarie_30093_eff_u1_attack_play_target": "-620,-50",
	"heidemarie_30093_eff_u3_attack_target": "-620,-50",
	"heidemarie_30093_eff_u3_attack_target_flash": "-620,-50",
	"heidemarie_30093_eff_u3_attack_ready_target": "-620,-50",
}

## 组键 → 组字典。进程内只读一次。
static var _index: Dictionary = {}

## 一次载入失败就不要再反复报错。
static var _index_failed: bool = false


## 惰性载入组库，把五类桶（own / enemy / cinematic / battle_ready / extra）
## 压成「组键 → 组」的一层字典。
static func ensure_index() -> void:
	if not _index.is_empty() or _index_failed:
		return
	if not FileAccess.file_exists(LIBRARY_PATH):
		_index_failed = true
		push_error("[Heidemarie] 缺少特效组库：%s" % LIBRARY_PATH)
		return

	var parsed = JSON.parse_string(FileAccess.get_file_as_string(LIBRARY_PATH))
	if typeof(parsed) != TYPE_DICTIONARY:
		_index_failed = true
		push_error("[Heidemarie] 特效组库解析失败：%s" % LIBRARY_PATH)
		return

	var classes: Variant = (parsed as Dictionary).get("classes")
	if typeof(classes) != TYPE_DICTIONARY:
		_index_failed = true
		push_error("[Heidemarie] 特效组库缺少 classes：%s" % LIBRARY_PATH)
		return

	for bucket_name in (classes as Dictionary):
		var table: Variant = (classes as Dictionary)[bucket_name]
		if typeof(table) != TYPE_DICTIONARY:
			continue
		for group_name in (table as Dictionary):
			_index[group_name] = (table as Dictionary)[group_name]


## 组键 → 组字典（找不到返回空字典）。
static func get_group(group_name: String) -> Dictionary:
	ensure_index()
	var node: Variant = _index.get(group_name)
	return node if node is Dictionary else {}


## 取某条 command 下的挂载参数（scale / offset_xy / opacity / zorder / delay / bone_type / slot）。
static func get_mount(group_name: String, command_name: String) -> Dictionary:
	var node := get_group(group_name)
	if node.is_empty():
		return {}
	var mounts: Variant = node.get("mounts")
	if typeof(mounts) != TYPE_DICTIONARY:
		return {}
	var profile: Variant = (mounts as Dictionary).get(command_name)
	return profile if profile is Dictionary else {}


## 该组用到的全部骨架名（预加载用）。
static func skeletons_of(group_name: String) -> Array:
	var node := get_group(group_name)
	var out: Array = []
	for layer in (node.get("spine_layers", []) as Array):
		if layer is Dictionary:
			var source := str((layer as Dictionary).get("source", ""))
			if not source.is_empty() and not out.has(source):
				out.append(source)
	return out


## 把一个「组键」展开成**中立图层行**数组。
##
## key 字段：
##   group     组键（必填）
##   command   该组挂在哪条 command 上（决定用哪份挂载参数，缺省时退化为默认）
##   start_ms  起播时刻（已含动作链累计偏移）
##   name      可选。**给了就只展开组内该骨架名的那一层**，不给则整组展开。
##             全屏演出时间轴（`heidemarie_{ug,ux}.json`）逐层登记了骨架名，
##             必须走这一档；单卡映射（`heidemarie_moves.json`）不给 name，走整组档。
##
## 返回的每一行：
##   name / clip / z / offset_x / offset_y / scale / rotation / opacity
##   clip_ms / start_ms / end_ms
static func expand_key(key: Dictionary) -> Array:
	var group_name := str(key.get("group", ""))
	if group_name.is_empty():
		return []

	var node := get_group(group_name)
	if node.is_empty():
		push_warning("[Heidemarie] 组库里没有这个特效组，已跳过：%s" % group_name)
		return []

	var only_name := str(key.get("name", ""))
	var command_name := str(key.get("command", ""))
	var profile := get_mount(group_name, command_name)
	if profile.is_empty() and not command_name.is_empty():
		push_warning("[Heidemarie] 组 %s 没有 %s 这条 command 的挂载参数，按默认演"
			% [group_name, command_name])

	var base_ms := float(key.get("start_ms", 0.0)) + _num(profile.get("delay_ms"), 0.0)
	var zorder := int(_num(profile.get("zorder"), 0.0))

	var mount_scale := _num(profile.get("scale"), 1.0)
	if mount_scale <= 0.0:
		mount_scale = 1.0
	var mount_offset := parse_xy(str(profile.get("offset_xy", "")))
	if TARGET_OFFSET_OVERRIDES.has(group_name):
		mount_offset = parse_xy(TARGET_OFFSET_OVERRIDES[group_name])
	var mount_alpha := _num(profile.get("opacity"), 255.0) / 255.0

	var rows: Array = []
	var matched := only_name.is_empty()
	for layer in (node.get("spine_layers", []) as Array):
		if not (layer is Dictionary):
			continue
		var row: Dictionary = layer

		# 逐层档：只要组里那一个骨架名。组里没有这个名字说明时间轴与组库脱节，
		# 直接返回空 —— 与「组不存在」同级，让调用方的去重/降级逻辑照常生效。
		var source := str(row.get("source", ""))
		if not only_name.is_empty() and source != only_name:
			continue
		matched = true

		var layer_scale := _num(row.get("scale"), 1.0)
		if layer_scale <= 0.0:
			layer_scale = 1.0
		var clip_ms := _num(row.get("clip_ms"), 0.0)
		if clip_ms <= 0.0:
			clip_ms = FALLBACK_CLIP_MS
		var start_ms := base_ms + _num(row.get("delay_ms"), 0.0)

		rows.append({
			"name": source,
			"clip": str(row.get("clip", "")),
			"z": zorder + int(_num(row.get("z"), 0.0)),
			"offset_x": mount_offset.x + _num(row.get("x"), 0.0),
			"offset_y": mount_offset.y + _num(row.get("y"), 0.0),
			"scale": mount_scale * layer_scale,
			"rotation": _num(row.get("rotate"), 0.0),
			"opacity": mount_alpha,
			"clip_ms": clip_ms,
			"start_ms": start_ms,
			"end_ms": start_ms + clip_ms,
		})
		# 逐层档命中一层就够；整组档要继续把组里剩下的层都展开。
		if not only_name.is_empty():
			break

	# overrides：人工微调过的字段（CZN_AnimLab 保存时会写成这个形状，
	# 见 tools/slim_cinematic_layers.py 的同名字段）。只覆盖推导出来的字段，
	# start_ms 一律以组键为准 —— 绝对时刻里含挂载/层内 delay，不能回写。
	var overrides: Dictionary = key.get("overrides", {})
	if not rows.is_empty() and overrides is Dictionary and not overrides.is_empty():
		var last: Dictionary = rows.back()
		if overrides.has("clip"):
			last["clip"] = str(overrides["clip"])
		if overrides.has("z"):
			last["z"] = int(_num(overrides["z"], 0.0))
		if overrides.has("scale"):
			last["scale"] = _num(overrides["scale"], 1.0)
		if overrides.has("rotation"):
			last["rotation"] = _num(overrides["rotation"], 0.0)
		if overrides.has("opacity"):
			last["opacity"] = _num(overrides["opacity"], 1.0)
		if overrides.has("end_ms"):
			last["end_ms"] = _num(overrides["end_ms"], float(last["end_ms"]))
		if overrides.has("offset"):
			var xy: Array = overrides["offset"]
			last["offset_x"] = _num(xy[0] if xy.size() > 0 else 0.0, 0.0)
			last["offset_y"] = _num(xy[1] if xy.size() > 1 else 0.0, 0.0)

	if not matched:
		push_warning("[Heidemarie] 组 %s 里没有骨架 %s，该图层已跳过" % [group_name, only_name])
	return rows


## 数组里第一项带 "group" 就说明是组键形态（v4）。
static func is_keyed(rows: Array) -> bool:
	if rows.is_empty():
		return false
	var first: Variant = rows[0]
	return first is Dictionary and (first as Dictionary).has("group")


## 把一组「组键」整体展开成中立图层行；已是逐层形态时原样返回。
static func expand_rows(rows: Array) -> Array:
	if not is_keyed(rows):
		return rows
	var out: Array = []
	for key in rows:
		if key is Dictionary:
			out.append_array(expand_key(key))
	return out


## 组库里的数值字段可能是 null（`.cfx` 没写就是不指定），统一兜底。
static func _num(value, fallback: float) -> float:
	if value == null:
		return fallback
	if value is float or value is int:
		return float(value)
	return fallback


## 解析 `.srmd` 的 `"x,y"` 形式偏移串；空串/格式不对时返回零。
static func parse_xy(text: String) -> Vector2:
	var parts := text.strip_edges().split(",")
	if parts.size() >= 2:
		return Vector2(float(parts[0]), float(parts[1]))
	return Vector2.ZERO
