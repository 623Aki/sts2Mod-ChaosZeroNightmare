extends RefCounted

# User-directed STS2 framing, 2026-10-07. This is not an official CZN scale.
# Shared with Presentation/DefeatCgFraming.cs for the C# overlay hosts.
# Fit the complete visible CG inside both viewport axes, with breathing room
# for the original animation pulses. Never crop the lower body to fill height.
const FIT_RATIO := 0.9

static func frame(viewport: Vector2, reference_center: Vector2, reference_size: Vector2) -> Transform2D:
	if viewport.x <= 0.0 or viewport.y <= 0.0 or reference_size.x <= 0.001 or reference_size.y <= 0.001:
		return Transform2D.IDENTITY
	var zoom := minf(viewport.x / reference_size.x, viewport.y / reference_size.y) * FIT_RATIO
	var target := viewport * 0.5
	return Transform2D(Vector2(zoom, 0), Vector2(0, zoom), target - reference_center * zoom)

static func content_rect(texture: Texture2D) -> Rect2:
	var fallback := Rect2(Vector2.ZERO, texture.get_size())
	var image := texture.get_image()
	if image == null or (image.is_compressed() and image.decompress() != OK):
		return fallback
	var used := image.get_used_rect()
	return Rect2(used) if used.has_area() else fallback

# The JSON-driven hosts share the same official chain/color sampler contract.
# Pick the dominant normal layer at the timeline midpoint, not an additive
# flash or a hidden afterimage. Sampling does not seek or retime playback.
static func reference_pose(overlay: Node, layers: Array, time_s: float) -> Dictionary:
	var pose := {"position": Vector2.ZERO, "scale": Vector2.ONE}
	var strongest := -1.0
	for layer_data in layers:
		if not str(layer_data.slot).ends_with("_normal"):
			continue
		var color: Color = overlay.call("_sample_color", str(layer_data.slot), time_s)
		if color.a > strongest:
			strongest = color.a
			pose = overlay.call("_sample_chain", layer_data.chain, time_s)
	return pose
