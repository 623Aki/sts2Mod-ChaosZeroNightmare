// 「卡牌连接」光带着色器
//
// 由卡厄斯梦境原版 system/program/sprite_card_connect_curve.vert / .frag 移植
// （Cocos GLSL → Godot 4 canvas_item）。
//
// ── 与原版的一处架构差异（重要） ───────────────────────────────────────────
// 原版把整条光带的形状完全放在顶点着色器里求值：网格顶点全为 0，
// 真正位置由 u_origin / u_control1 / u_control2 / u_end_point 四个 uniform 决定。
//
// 搬到 Godot 后发现这条路线有个致命副作用：**网格顶点全为 0 ⇒ AABB 为空**，
// Godot 的 canvas 渲染会据此判定该 MeshInstance2D「不可见」而直接跳过，
// 表现就是「代码跑了、节点也在，但屏幕上什么都没有」。
//
// 因此本工程改成：**顶点位置由 C# 侧每帧求值并写入网格**（见
// Integration/HeidemarieLinkOverlay.cs 的 FillRibbonGeometry），
// 着色器只保留「片元流动」这一原版核心视觉。
// 下面的贝塞尔求值代码保留下来（u_use_bezier = true 时可切回原版路线），
// 默认 false —— 此时顶点直通、不做任何位移。
//
// 网格约定（由 C# 侧构造）：
//   UV.x = 沿曲线参数 t (0..1)，UV.y = 0 / 1（横向两端）
shader_type canvas_item;
render_mode blend_add, unshaded;

uniform sampler2D u_texture : repeat_enable, filter_linear;

// ---- 曲线：贝塞尔模式（原版主模式；本工程默认关闭，改由 C# 提供顶点） ----
uniform bool u_use_bezier = false;
uniform vec2 u_origin = vec2(0.0);
uniform vec2 u_control1 = vec2(0.0);
uniform vec2 u_control2 = vec2(0.0);
uniform vec2 u_end_point = vec2(0.0);
uniform float u_half_thickness = 34.0;

// ---- 流动 ----
uniform float u_flow_speed = 0.35;  // 沿光带滚动速度（原版 u_flow_speed）
uniform float u_flow_force = 0.02;  // 横向轻微波动幅度（原版 u_flow_force）
uniform float u_time_offset = 0.0;  // 每条光带的相位错开，避免多条完全同步
uniform vec3 u_tint_color = vec3(0.0);
uniform float u_tint_strength = 0.0;
uniform float u_alpha_mul = 1.0;

// 贴图 1024x256；原版写死 1.5/1028 与 1.5/260（1.5 为防采样溢色留的边距）
uniform float u_texel_x = 1.5 / 1024.0;
uniform float u_texel_y = 1.5 / 256.0;

// ---- 原版 cubicBezier / cubicBezierTangent：原样保留 ----
vec2 cubic_bezier(vec2 p0, vec2 p1, vec2 p2, vec2 p3, float t) {
	float mt = 1.0 - t;
	return mt * mt * mt * p0
		+ 3.0 * mt * mt * t * p1
		+ 3.0 * mt * t * t * p2
		+ t * t * t * p3;
}

vec2 cubic_bezier_tangent(vec2 p0, vec2 p1, vec2 p2, vec2 p3, float t) {
	float mt = 1.0 - t;
	return 3.0 * mt * mt * (p1 - p0)
		+ 6.0 * mt * t * (p2 - p1)
		+ 3.0 * t * t * (p3 - p2);
}

void vertex() {
	// 默认（u_use_bezier = false）：顶点已由 C# 写好真实位置，直通即可。
	if (!u_use_bezier) {
		return;
	}

	float t = clamp(UV.x, 0.0, 1.0);
	vec2 curve = cubic_bezier(u_origin, u_control1, u_control2, u_end_point, t);
	vec2 tangent = cubic_bezier_tangent(u_origin, u_control1, u_control2, u_end_point, t);
	vec2 normal = vec2(-tangent.y, tangent.x) / max(length(tangent), 0.0001);
	// UV.y = 0 / 1 → 沿曲线法线撑开 ±u_half_thickness
	VERTEX = curve + normal * (UV.y - 0.5) * 2.0 * u_half_thickness;
}

// ---- 原版 getFlowUV：横向按 phase 滚动 + 纵向轻微波动，并扣掉采样边距 ----
vec2 flow_uv(float phase_x, float phase_v) {
	vec2 uv = UV;
	uv.x = mix(u_texel_x, 1.0 - u_texel_x, phase_x);
	uv.y = fract(uv.y + sin((phase_v + (TIME + u_time_offset) * 0.35) * 6.28318) * u_flow_force);
	uv.y = mix(u_texel_y, 1.0 - u_texel_y, uv.y);
	return uv;
}

void fragment() {
	float phase = fract(UV.x + (TIME + u_time_offset) * u_flow_speed);

	// 双采样消接缝：两个采样相差半周期，按「到各自接缝的距离」加权混合，
	// 这样滚动到接缝处时由另一份采样补位，看不到缝。
	float phase_b = fract(phase + 0.5);
	vec4 a = texture(u_texture, flow_uv(phase, phase));
	vec4 b = texture(u_texture, flow_uv(phase_b, phase));

	float d_a = min(phase, 1.0 - phase);
	float d_b = abs(phase - 0.5);
	float w = d_a / (d_a + d_b + 0.0001);
	vec4 tex = mix(a, b, w);

	vec3 tinted = mix(tex.rgb, tex.rgb + u_tint_color, u_tint_strength);
	COLOR = vec4(tinted, tex.a * u_alpha_mul) * COLOR;
}
