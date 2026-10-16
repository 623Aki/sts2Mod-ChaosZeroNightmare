// 「连接光带」着色器（定稿版）
//
// 来源：`tools/HeidemarieEffectLab/link_ribbon.gdshader`（预览台里逐轮调出来的那份），
// 2026-10-16 定稿后原样搬进 mod。预览台那份是迭代入口，改观感先改那边。
//
// 与原来 `card_connect_flow.gdshader`（Cocos 原版移植，已废弃不用）的区别：
//   1. 流动真正沿带滚动 —— 原版把 uv.x 直接覆盖成纯相位值，沿带方向毫无变化；
//   2. 新增「自左向右扫过的脉冲」，流动感一眼可见；
//   3. 厚度方向用 sin 幂次剖面做柔和软边 + 两端 smoothstep 收口；
//   4. 颜色拆成「核心色 / 边缘色」双色渐变，中线亮青、外圈电蓝，不再白热一片。
//
// ⚠️ 写 Godot 着色器时的两条硬性约束（原版移植版就是栽在这两处）：
//   · uniform 默认值必须是字面常量，不能写 `1.5 / 1024.0` 这类算式；
//   · 内置变量（UV / TIME）只能在 vertex / fragment 入口里用，自定义函数要靠参数传。
shader_type canvas_item;
render_mode blend_add, unshaded;

uniform sampler2D u_texture : repeat_enable, filter_linear;

// ── 流动 ──
uniform float u_flow_speed = 0.55;   // 沿带滚动速度
uniform float u_flow_force = 0.06;   // 纵向波动幅度
uniform float u_time_offset = 0.0;   // 多条带错开相位

// ── 脉冲（自左向右扫过的亮带）──
uniform float u_pulse_speed = 0.35;
uniform float u_pulse_gain = 1.2;
uniform float u_pulse_width = 0.16;

// ── 颜色与柔和度 ──
uniform vec3 u_core_color : source_color = vec3(0.38, 0.86, 1.0);
uniform vec3 u_edge_color : source_color = vec3(0.02, 0.28, 0.95);
uniform float u_alpha_mul = 0.72;
uniform float u_soft_power = 1.0;    // 越大越细硬，越小越柔宽
uniform float u_v_zoom = 0.50;       // 纵向只取贴图中段这么多
uniform float u_core_sharp = 3.5;    // 核心色收窄程度：越大只有中线亮青
uniform float u_end_fade = 0.06;     // 两端收口比例（0 = 不收口）

// 1 = 作为外层光晕绘制（不做脉冲、不叠加亮色）
uniform float u_glow_mix = 0.0;

void fragment() {
	float t = TIME + u_time_offset;

	// 沿带滚动（fract 保证无缝自循环）
	float flow = fract(UV.x - t * u_flow_speed);

	// 采样 UV：横向用 flow，纵向叠一层轻微水波
	float wave = sin((flow + t * 0.55) * 6.2831853) * u_flow_force;
	float vy = fract(UV.y + wave);
	// 只取贴图中段，主线才够粗、够软
	float half_v = 0.5 * clamp(u_v_zoom, 0.05, 1.0);
	vec2 uv = vec2(mix(0.0015, 0.9985, flow), mix(0.5 - half_v, 0.5 + half_v, vy));

	vec4 tex = texture(u_texture, uv);

	// 厚度方向柔和剖面：中间亮、两端化开
	float prof = pow(max(sin(UV.y * 3.14159265), 0.0), max(u_soft_power, 0.05));

	// 自左向右扫过的脉冲
	float travel = fract(UV.x - t * u_pulse_speed);
	float pulse = exp(-pow((travel - 0.5) / max(u_pulse_width, 0.01), 2.0));
	pulse = mix(pulse, 0.0, clamp(u_glow_mix, 0.0, 1.0));

	// 颜色：只有很窄的中线取核心色，其余迅速化到边缘色
	float cprof = pow(prof, max(u_core_sharp, 0.05));
	vec3 col = mix(u_edge_color, u_core_color, cprof);

	// 两端柔和收口：避免相邻两条带在卡牌接点处叠加成一团白亮
	float ef = clamp(u_end_fade, 0.0, 0.49);
	float endfade = smoothstep(0.0, ef, UV.x) * smoothstep(1.0, 1.0 - ef, UV.x);

	float a = tex.a * prof * endfade * u_alpha_mul * (1.0 + u_pulse_gain * pulse);

	COLOR = vec4(col * (1.0 + 0.5 * pulse), a) * COLOR;
}
