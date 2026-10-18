class_name HeidemarieEffectCompose
extends RefCounted

# Heidemarie 特效组库 · 自组装组（compose 形态）。
#
# 与原子组库 `heidemarie_effect_groups.gd` 的分工：
#   · 原子组库（HeidemarieEffectGroups）= 源数据生成的「组 → spine_layers + mounts」，
#     展开时要叠 mounts 的 scale / offset / zorder / delay；
#   · 本文件（compose）= 手工/工具维护的「组 → owner[] + target[]」，**层值就是最终值**，
#     不再叠 mounts —— 工具所见即游戏所得。
#
# 一个组自带两侧：`owner`（施法者）/ `target`（目标），
# 由调用方按 bucket 取对应侧，所以卡牌映射里一个组名就够。
#
# 层字段（与 heidemarie_effect_stage.gd._build_layer 消费的 v3 行一一对应）：
#   name / clip / z / offset[x,y] / scale / rotation / opacity / delay_ms / clip_ms
#
# 展开规则（与 tools/HeidemarieEffectLab 的 _compose_rows 完全一致）：
#   start_ms = 组键 base_ms + 层 delay_ms
#   end_ms   = start_ms + clip_ms

## 组库路径。
const LIBRARY_PATH := "res://CznHeidemarie/effects/heidemarie_effect_compose.json"

## 组里量不到 clip 时长时的兜底（毫秒）。
const FALLBACK_CLIP_MS := 1000.0

## 组名 → 组字典。进程内只读一次。
static var _index: Dictionary = {}

## 一次载入失败就不要再反复报错。
static var _index_failed: bool = false


## 惰性载入组库。
static func ensure_index() -> void:
	if not _index.is_empty() or _index_failed:
		return
	if not FileAccess.file_exists(LIBRARY_PATH):
		_index_failed = true
		push_error("[Heidemarie] 缺少自组装特效组库：%s" % LIBRARY_PATH)
		return

	var parsed = JSON.parse_string(FileAccess.get_file_as_string(LIBRARY_PATH))
	if typeof(parsed) != TYPE_DICTIONARY:
		_index_failed = true
		push_error("[Heidemarie] 自组装特效组库解析失败：%s" % LIBRARY_PATH)
		return

	var groups: Variant = (parsed as Dictionary).get("groups")
	if typeof(groups) != TYPE_DICTIONARY:
		_index_failed = true
		push_error("[Heidemarie] 自组装特效组库缺少 groups：%s" % LIBRARY_PATH)
		return

	for gname in (groups as Dictionary):
		var g: Variant = (groups as Dictionary)[gname]
		if g is Dictionary:
			_index[str(gname)] = g


## 组名 → 组字典；找不到返回空字典。
static func get_group(group_name: String) -> Dictionary:
	ensure_index()
	var node: Variant = _index.get(group_name)
	return node if node is Dictionary else {}


## 组库里登记过的全部组名。
static func group_names() -> Array:
	ensure_index()
	return _index.keys()


## 该组某一侧（owner / target）用到的全部骨架名（预加载用）。
static func skeletons_of(group_name: String, bucket: String) -> Array:
	var node := get_group(group_name)
	var out: Array = []
	var layers: Variant = node.get(bucket)
	if typeof(layers) != TYPE_ARRAY:
		return out
	for layer in (layers as Array):
		if layer is Dictionary:
			var source := str((layer as Dictionary).get("source", ""))
			# compose 的层字段叫 name（不是原子组库的 source），两个都认
			if source.is_empty():
				source = str((layer as Dictionary).get("name", ""))
			if not source.is_empty() and not out.has(source):
				out.append(source)
	return out


## 把组的一侧展开成**中立图层行**数组。
##
## 返回的每一行：
##   name / clip / z / offset_x / offset_y / scale / rotation / opacity
##   clip_ms / start_ms / end_ms
static func expand(group: Dictionary, bucket: String, base_ms: float) -> Array:
	var rows: Array = []
	var layers: Variant = group.get(bucket)
	if typeof(layers) != TYPE_ARRAY:
		return rows

	for layer in (layers as Array):
		if not (layer is Dictionary):
			continue
		var L: Dictionary = layer
		var name := str(L.get("name", ""))
		if name.is_empty():
			continue

		var clip_ms := _num(L.get("clip_ms"), 0.0)
		if clip_ms <= 0.0:
			clip_ms = FALLBACK_CLIP_MS
		var start_ms := base_ms + _num(L.get("delay_ms"), 0.0)
		var offset: Array = L.get("offset", []) if L.get("offset") is Array else []
		var scale := _num(L.get("scale"), 1.0)
		if scale <= 0.0:
			scale = 1.0

		rows.append({
			"name": name,
			"clip": str(L.get("clip", "")),
			"z": int(_num(L.get("z"), 0.0)),
			"offset_x": _num(offset[0] if offset.size() > 0 else 0.0, 0.0),
			"offset_y": _num(offset[1] if offset.size() > 1 else 0.0, 0.0),
			"scale": scale,
			"rotation": _num(L.get("rotation"), 0.0),
			"opacity": _num(L.get("opacity"), 1.0),
			"clip_ms": clip_ms,
			"start_ms": start_ms,
			"end_ms": start_ms + clip_ms,
		})
	return _compress_z(rows)


## z 保序压缩。
##
## 源 `.cfx` 的 z 只是**组内排序号**（同一组里出现 -100 / +100 这种跨度），
## 而游戏战斗画布的 z 预算极窄：背景与角色同在 -10，前台容器 -9。
## 真值直接当 z 用，负值层会掉到背景之后，表现为「特效只播了一半」。
##
## 压缩规则（只保留前后关系，不动正负号）：
##   z < 0   → -N .. -1（角色之后，由舞台分到后台容器）
##   z >= 0  →  1 ..  M（角色之前，由舞台分到前台容器）
## 同 z 的层保持 cfx 里的原始顺序（按来源下标做次序键）。
static func _compress_z(rows: Array) -> Array:
	var order: Array = []
	for index in rows.size():
		var row: Variant = rows[index]
		if row is Dictionary:
			order.append({"z": float((row as Dictionary).get("z", 0.0)), "i": index, "row": row})
	order.sort_custom(func(a, b):
		if float(a["z"]) == float(b["z"]):
			return int(a["i"]) < int(b["i"])
		return float(a["z"]) < float(b["z"]))

	var back_count := 0
	for item in order:
		if float(item["z"]) < 0.0:
			back_count += 1

	var out: Array = []
	var back_step := 0
	var front_step := 0
	for item in order:
		var row: Dictionary = (item["row"] as Dictionary).duplicate()
		if float(item["z"]) < 0.0:
			row["z"] = -back_count + back_step
			back_step += 1
		else:
			row["z"] = 1 + front_step
			front_step += 1
		out.append(row)
	return out

## 数值字段可能是 null（没写就是不指定），统一兜底。
static func _num(value, fallback: float) -> float:
	if value == null:
		return fallback
	if value is float or value is int:
		return float(value)
	return fallback
