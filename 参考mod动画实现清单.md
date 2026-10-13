# 参考 Mod 动画实现清单（通用教程）

> 适用对象：本工作区所有「卡厄思梦境角色 → 杀戮尖塔 2 角色」的移植 mod。
> 举证样本：`CznTipheraCharacter`（下文简称 Tiphera）。
> 结论对 `CznNineCharacter` 同样成立，两者结构一致。
> 落实示例：`CznHeidemarie` 的 `HeidemarieAnimation` / `GenerateAnimator`；
> 全屏动画与战斗开关见第八节，落实示例为 `HeidemarieCinematicRuntime` /
> `HeidemarieMoveEffectsRuntime` / `HeidemarieCutInToggleRuntime`；
> 出牌特写的待机特效层与选人立绘布局见第九节，落实示例为
> `HeidemarieBattleReadyRuntime` / `HeidemarieCharSelectBackgroundRuntime`（仅本工作 mod 有）。

---

## 〇、三个核心问题的结论

| 问题 | 结论 |
|---|---|
| 动画用什么承载？ | **主模型骨架承担全部角色动作**；技能特效是**另一批独立骨架**，不是主模型的子动画 |
| 单个动作还是一系列动作？ | **两者都用**。状态机里用 `AddNextState` 串成**动作链**（触发一次自动接下一段）；演出层用 `SetAnimation + AddAnimation` 直接排**动作序列** |
| 动作名谁指定？ | **代码里显式写死骨架真实动画名**，**绝不交给引擎默认获取**。因为卡厄思骨架没有游戏默认名（`idle_loop` 等），必须 `override GenerateAnimator` 做映射 |
| 驱动方式 | 游戏触发器字符串（`Idle / Attack / Cast / Dead / …` + mod 自定义 `TipheraU3` …）→ `CreatureAnimator` 状态机 → Spine 动画 |

一句话：**主模型带角色动作，独立骨架带技能特效；动作由代码点名并串成链/序列，不依赖引擎默认名。**

---

## 一、资源层清单

| 资源 | 典型路径 | 说明 |
|---|---|---|
| 主模型骨架 | `<modid>/animations/body/<slug>_body.skel`（+`.atlas`+`.png`） | 角色本体，几十段动作全在这里；**禁止再次转换**（游戏运行时是 Spine 4.2.11） |
| 主模型数据 | `<modid>/animations/body/<slug>_body_skel_data.tres` | 指向 atlas + skel，`default_mix` 通常 0.05 |
| 战斗入场姿态 | `<modid>/animations/body/<slug>_battle_ready.skel` | 出牌特写 / 入场演出专用 |
| 选人立绘 | `<modid>/animations/character_select/*.skel` | 选人界面动态背景 |
| 技能特效骨架 | `<modid>/spine_effects/moves/*.skel` | **每个技能动作一套独立骨架 + 场景**（起手 / 施放 / 命中特效各自成组） |
| 通用特效骨架 | `<modid>/spine_effects/common_defense/*`、`system_eff_debuff` … | 防御盾、减益、败北演出等 |

> `_skel_data.tres` 是纯文本、**UTF-8 无 BOM**，批量生成脚本：
> `tools\gen_skel_tres.ps1`。

---

## 二、场景层清单

本体场景是**唯一承载角色动作**的地方：

```
scenes/creature_visuals/<slug>.tscn          ← 本体，必须存在（VisualsPath 是 private，靠 pck 同名覆盖）
scenes/vfx/card_trail_<slug>.tscn
scenes/combat/energy_counters/<slug>_energy_counter.tscn
scenes/merchant/characters/<slug>_merchant.tscn
scenes/rest_site/characters/<slug>_rest_site.tscn
scenes/screens/char_select/char_select_bg_<slug>.tscn
scenes/ui/character_icons/<slug>_icon.tscn
<modid>/scenes/combat_effects/moves/*.tscn   ← 每个技能动作的特效场景（可选，按素材情况）
<modid>/scenes/<slug>_battle_ready.tscn      ← 出牌特写场景（可选）
```

本体场景关键节点（Tiphera 实例，结构三 mod 一致）：

```ini
[node name="Visuals" type="SpineSprite" parent="."]
skeleton_data_res = ExtResource("2_body")     ; 指向 <slug>_body_skel_data.tres
preview_skin = "default"                       ; 卡厄思骨架只有 default 一个皮肤
preview_animation = "b_idle"                   ; 填骨架真实存在的待机动画名
position = Vector2(0, -20)
scale = Vector2(1.36, 1.36)                    ; 见「缩放标定」
unique_name_in_owner = true
```

- 节点名必须是 `Visuals`：`NCreatureVisuals._Ready` 用 `GetNode<Node2D>("%Visuals")` 取本体；
  名字不符 → `SpineBody` 为 null → 游戏 `PushWarning` 后**彻底禁用动画**。
- `Bounds`（Control，±140 宽）、`CenterPos`、`IntentPos` 是 HUD/交互标定，**与 Spine 缩放无关**。

### 缩放标定（经验值）

```
目标：b_idle 渲染高度 ≈ 275px（与角色选择界面立绘一致）
scale = 275 / 骨架 b_idle 包围盒高度
  Nine: 1.55   Tiphera: 1.36   Heidemarie: 1.247（包围盒 220.55）
```
量包围盒的方式：`tools\SpineSkeletonDataConverter.exe <骨架>.skel <临时>.json`，
沿父链累乘 `local = T·R·S` 取所有骨骼原点并集。
商店 / 休息点用**不同缩放**（站位与镜头不同），各自单独标定。

---

## 三、代码层：`GenerateAnimator` 覆盖（动作链 + 触发器映射）

### 3.1 为什么必须覆盖

游戏默认实现用 `idle_loop / attack / cast / hurt / die / relaxed_loop / …`
这套约定名，卡厄思骨架**没有**这些名字。照抄默认实现 → 角色静止不动，
日志只刷 `could not find 'idle_loop' animation`。

必须 `override CreatureAnimator GenerateAnimator(MegaSprite controller, Creature creature)`
把游戏触发器映射到骨架真实动画。

### 3.2 动作链（`AddNextState`）

一条链 = 触发一次后自动续播到回落动画：

| 链 | 动作序列 | 回落 |
|---|---|---|
| 普攻 A | `attack_ready → attack_play1` | `idle` |
| 普攻 B | `attack_ready → attack_play2` | `idle` |
| U3 攻击 | `attack_ready → u3_attack_ready → u3_attack_play → u3_attack_end` | `idle` |
| 防御 | `defense_ready → defense_play` | `idle` |
| U1/U2/U4/U5 增益 | `uN_buff_ready → uN_buff_play` | `idle` |
| 通用 buff | `buff_ready → buff_play` | `idle` |
| 入场 | `enter_ready → enter_play → enter_end` | `idle` |
| 死亡 | `death_ready → death` | — |
| 胜利 | `victory_ready → victory`(loop) | — |
| 姿态切换 | `idle_to_b_idle → b_idle` / `b_idle_to_idle → idle` | — |

### 3.3 触发器映射

游戏写死的触发器名（必须认识） + mod 自定义触发器（自己起名，供卡牌调用）：

| 触发器 | 起始动作 | 来源 |
|---|---|---|
| `Idle` / `Relaxed` | `b_idle` / `idle` | 游戏 |
| `Revive` | 待机 | 游戏 |
| `Hit` | `hit` | 游戏 |
| `Attack` / `Attack2` | `attack_ready`（两条分支） | 游戏 / mod |
| `Cast` / `PowerUp` | `buff_play` | 游戏 |
| `Defend` | `defense_ready` | 游戏 |
| `Dead` / `Fatal` | `death_ready` / `fatal_intro` | 游戏 / mod |
| `Entry` | `enter_ready` | 游戏约定名，**需 mod 自行触发**（订阅战斗开始事件后手动 `SetAnimationTrigger`） |
| `Victory` | `victory_ready` | 游戏约定名 |
| `<Mod>CardReady` / `<Mod>CardRestore` | 姿态互切 | mod |
| `<Mod>U1/U2/U3/U4` … | 对应 `uN_*` 链 | mod |

- 攻击触发器给游戏默认攻击流程用；技能触发器由卡牌自己显式调用。
- 低血量待机、倒地等分支用 `AddAnyState(name, state, predicate)` 重载。

---

## 四、代码层：动作序列（绕过状态机，直接排队播放）

对应 Tiphera 的 `TipheraCombatBody.TryPlayCombatBodySequence`：

```csharp
// animations 形如 ["attack_ready","attack_play1"] / ["defense_ready","defense_play"]
// / ["attack_ready","u3_attack_ready","u3_attack_play","u3_attack_end"]
SpineAnimationCompat.SetAnimation(access, animations[0], loop: false);   // 首段
for (int i = 1; i < animations.Count; i++)
    SpineAnimationCompat.AddAnimation(access, animations[i], 0f, loop: false); // 后续排队
SpineAnimationCompat.AddAnimation(access, "idle");                        // 末尾回待机
```

用途与要点：

- 一次调用播**一串动作**，不依赖状态机事先注册链条；
- 播前做**存在性校验**（`HasAnimation`），任一缺失就放弃并交回状态机（`SetAnimationTrigger` 降级）；
- `MegaAnimationState.SetAnimation(name, loop, trackId)` / `AddAnimation(name, delay, loop, trackId)`
  也可经 `SpineAnimationAccess`（`NCreatureVisuals.SpineAnimation`）调用，效果等价。

---

## 五、卡牌如何触发动作

| 卡类型 | 推荐写法 | 备注 |
|---|---|---|
| 攻击卡 | `DamageCmd.Attack(...).FromCard(this, cardPlay).WithAttackerAnim("<触发器>", <延迟秒>)` | `WithAttackerAnim` 必须排在 `FromCard` 之后；内部转到 `CreatureCmd.TriggerAnim` |
| 技能 / 能力卡 | `override OnEnqueuePlayVfx(Creature? target)` 里 `await CreatureCmd.TriggerAnim(...)` | 游戏为「出牌演出」预留的钩子，先于 `OnPlay` 数值结算 |
| 需要调速 | 动画开始后再 `GetCurrentTrack()?.SetTimeScale(x)` | 速度设早了会被后续 `SetAnimation` 冲掉 |
| 需要多段动作 | `PlaySequence(...)`（见第四节） | 起手 → 施放 → 收招一次排完 |

延迟秒数**必须来自骨架实测**（`Tools\SpineSkeletonDataConverter.exe` 导出 JSON，
取该动画 timeline 最大时间戳），不要凭感觉估。
`TriggerAnim` 只对 `Attack` / `Cast` / `PowerUp` 播角色音效，**自定义触发器不触发音效查找**。

---

## 六、演出调度（时间轴）

- 用调度器 `GodotScheduler.Delay(秒, …)` 按 `ready / play / end` 分段挂：
  主模型动作名 + 特效骨架场景 + SFX / 语音。
- 每个玩家一个 `token`，`IsPresentationTokenCurrent` 防止旧动作覆盖新动作。
- 特效场景分 `Front` / `Back` 屏层，可指定寿命、是否跟随 Spine 层时长。

---

## 七、引擎约定名缺失的两条补法

卡厄思骨架没有 `idle_loop / relaxed_loop / overgrowth_loop / hive_loop /
glory_loop / low_health_loop / hurt` 等游戏约定名，二选一：

1. **改造骨架**（参考 mod 的做法）：把约定名加进骨架 —— 需要重导骨架，**不推荐**。
2. **代码重定向**（本工作 mod 的做法，对游戏原始角色零侵入）：
   - 商店 / 休息点：Harmony 补丁 `Prefix`/`Postfix` 改写 `PlayAnimation` / `_Ready`
     里的动画名，先 `HasAnimation` 判存在，缺了才回退到 `stop / camping / b_idle`；
   - 待机 / 受击判定：补 `NCreatureVisuals.IsPlayingIdleAnimation` /
     `IsPlayingHurtAnimation`，把 `b_idle / idle` 视为待机、`hit` 视为受击。

---

## 八、全屏动画（Cinematic 全屏接管演出）

前七节讲的都是「角色本体范围内的动作 + 技能特效」。**全屏动画是另一码事**：
它是把整个战斗画面**接管**过来演一整段多轨演出（终极技 / 大招），
参考 mod 里叫 **Cinematic**（Tiphera）或 **Cinematic Stage**（Nine）。

> **「层级混乱 / 特效不完整」先看 8.4.1。** 那里写清了参数的唯一权威来源
> （`.srmd` + `.cfx` 三层数据链）、`z` 与时序的推导公式、以及 Godot 侧三个不改就白提 z 的硬约束。
> 本节其余部分讲的是**架构**（谁在演、怎么同步、怎么容错）。

### 8.1 一句话结论

| 问题 | 结论 |
|---|---|
| 谁在演？ | 一个**独立的 Godot 场景**（根节点 `CanvasLayer`，`layer = 4090`），自己搭一块 1280×720 的「设计画布」，盖在战斗画面之上 |
| 数据从哪来？ | 官方 `.cfx` / `.le` 时间轴提取成的 **JSON**，每个终极技一份，放在 `res://<modid>/cinematic/<route>.json` |
| 演什么？ | **一批互相独立的 Spine 特效骨架**（不是主模型的子动画）按时间轴分层摆放 + 镜头抖动/推拉 + 黑幕 + cut-in 立绘/逐帧图 + 音频 |
| 角色本体呢？ | 接管期间**把本体隐藏**；本体的 `ug_attack` / `ux_attack` 由官方演出数据驱动，到 `battlefield_actors_released` 再把本体交还 |
| 和游戏怎么同步？ | 卡牌被 **Hook 补丁**（`Hook.BeforeCardPlayed` / `Hook.AfterCardPlayed`）拦下，先等全屏演出的 **`hit` 放行**，伤害与后续结算才继续 |
| 谁决定用不用？ | 卡牌 → 演出路线（`Ug` / `Ux`）→ 对应场景（`combat_ug_overlay.tscn` / `combat_ux_overlay.tscn`），再经「观众可见性 + 用户设置」两道闸 |

> 参考实现（两处，结构一致）：
> - Tiphera：`CznTipheraCharacter/decompiled/CznTipheraCharacter.Compatibility/TipheraCombatCinematicRuntime.cs`（约 800 行）
>   + `CznTipheraCharacter.Presentation/TipheraCombatResources.LoadCinematicScene`
>   + 场景 `res://CznTipheraCharacter/scenes/combat_{ug,ux}_overlay.tscn`
>   + 资源清单 `res://CznTipheraCharacter/manifests/tiphera_ug_ux_resources.json`
> - Nine：`CznNineCharacter/decompiled/CznNineCharacter/Presentation/NineCinematicRuntime.cs`
>   + 场景 `res://CznNineCharacter/scenes/cinematic/nine_cinematic_stage.tscn`
>   + 合成器 `res://CznNineCharacter/scenes/cinematic/nine_cinematic_stage.gd`
>   + 时间轴 `res://CznNineCharacter/cinematic/{ug_attack,ug_all_attack,ux_all_attack,cutins}.json`

### 8.2 四层结构

```
数据层   cinematic/<route>.json          官方时间轴：图层表 + 事件表 + 契约时长
              ↓
场景层   scenes/cinematic/<slug>_stage.tscn   CanvasLayer(layer=4090)，只挂一个合成器脚本
              ↓
合成器   <slug>_stage.gd                 读 JSON，建 SpineSprite / 黑幕 / cut-in，按毫秒时钟推进
              ↓
接入层   <Mod>CombatCinematicRuntime.cs  Hook 补丁 + 信号桥接 + 本体租借 + 容错
```

四点关键约定：

1. **合成器自己走毫秒时钟**，不依赖 Godot 的 `AnimationPlayer`：
   `clock_ms = (Time.get_ticks_usec() - epoch_usec) / 1000`，
   每帧对每个图层 `set_track_time((clock_ms - start_ms)/1000)` 再 `update_skeleton(0)`。
   这样图层可以被任意裁剪/提速/暂停，也不怕某一层缺失。
2. **首帧预绘（preplay barrier）**：挂载完成后先 `await RenderingServer.frame_post_draw`
   两帧（Tiphera 还额外把 Spine 与 cut-in 贴图放进一个不可见 `SubViewport` 各draw一次），
   再建立时钟。否则纹理上传与 shader 编译会砸在演出第一帧上，表现为开头卡一下。
3. **屏幕层 / 世界层分开**：`screen` 节点不受镜头影响（全屏闪白、黑幕、cut-in 放这里），
   `stage` 节点受镜头抖动/推拉影响（地面碎裂、剑光、目标特效放这里）。
   两者同在一个 `canvas` 下，才能保持 z 序交织。
4. **所有资源先判存在、再降级**：任一层骨架缺失 → 整段演出放弃，交回原生出牌演出，
   **绝不半路断在半截**。

### 8.3 合成器对外契约（C# 侧要认识的名字）

信号（C# 用 `Node.Connect(name, Callable.From(...))` 订阅）：

| 信号 | 含义 | C# 侧动作 |
|---|---|---|
| `presentation_started` | 时钟已建、演出正式开始 | 记录耗时；隐藏本体、停止语音/特写 |
| `hit_reached` / `hit_released` | **到命中点**，可以结算伤害了 | 放行被 Hook 拦住的卡牌结算 |
| `actors_restored` / `battlefield_actors_released` | 官方要求把本体交还给战斗 | 恢复本体可见 + 触发 `Idle` |
| `stage_finished` | 接管结束（特效尾巴还在） | 恢复本体、允许胜利结算 |
| `resources_finished` | 连尾巴也放完了 | `QueueFree()` 整段场景 |

方法：

| 方法 | 用途 |
|---|---|
| `configure(route, owner, owner_position, proxies, positions, sfx_vol, voice_vol[, voice_path]) -> bool` | 挂载后配置：读时间轴、建节点、把目标站位换算进画布；返回 `false` 表示资源不齐，调用方须放弃 |
| `start_stage() -> bool` | 显示画布、走预绘屏障、建立时钟 |
| `cancel_stage()` | 立即静默收尾（换卡 / 死亡 / 离房） |
| `set_paused(bool)` | 暂停/恢复时钟与音频 |
| `is_ready_for_playback() -> bool` | 预演是否就绪（Tiphera 用它做放行检查） |
| `get_runtime_contract() -> Dictionary` | 返回 `duration_seconds` / `hit_release_seconds` / `battlefield_actor_release_seconds`，**C# 侧必须校验单调性**（见 8.5） |
| `get_performance_snapshot()` | 收尾时打点（各阶段耗时），只用于日志 |

### 8.4 时间轴数据（`cinematic/<route>.json`）

Nine 的数据形态（由官方 cfx 提取器生成，**不要手改**）：

```jsonc
{
  "route": "ug_attack",
  "plan": {
    "summary": { "command_duration_ms": 3533.3, "layer_count": 18 },
    "layers": [
      { "layer_class": "effect_spine", "name": "nine_30047_ug_impact",
        "start_ms": 0.0, "end_ms": 66.7,
        "attachment": { "target": "SCREEN", "offset": [0, 0], "scale": 1.0,
                        "zorder": -5, "opacity": 255 },
        "official": { "cfx": { "primitive": { "raw": { "ani": "animation", "x": "0", "z": "-100" } } } } }
    ]
  },
  "timeline": { "timeline": [
      { "kind": "HIT",  "name": "action", "time_ms": 2066.7 },
      { "kind": "SHAKE", "name": "...", "time_ms": 2066.7 },
      { "kind": "CAM_MOVE", "time_ms": 0, "params": { "x": 0, "y": 0 } },
      { "kind": "COLOR_BLEND", "time_ms": 2066.7, "params": { "opacity": 0.4, "color": "..." } }
  ] }
}
```

`layer_class` 的取值与处理（这是官方语义，照抄即可）：

| `layer_class` | 处理 |
|---|---|
| `effect_spine` | 建 `SpineSprite`，`skeleton_data_res = <modid>/animations/**/<name>_skel_data.tres`，clip 取 `raw.ani`（缺省 `animation`） |
| `body_spine` | 同上，但骨架换成本体骨架，用来在画布里演角色 |
| `cutin` | 逐帧图（WEBP/PNG 序列），按 `frames[].start_ms` 切帧 |
| `particle` / `target_node` | 粒子 / 目标挂点动画，按时间曲线插值 |
| `camera` / `damage_marker` | 镜头与伤害标记，只做参考，一般忽略 |

`attachment.target` 决定图层挂在哪个节点：`SCREEN`（屏幕层）/ `TARGET`（目标站位）/ `FOR_CENTER`（阵型中心）。
坐标约定：官方数据是 **Spine 的 y 向上**，Godot 是 y 向下，所以 `spine.position = Vector2(x, -y)`。

> Tiphera 不走 JSON，而是把整条时间轴**烘进 `combat_{ug,ux}_overlay.tscn` 场景**，
> 用 `manifests/tiphera_ug_ux_resources.json` 只做「要预加载哪些资源」的清单。
> 两种都行：**数据放 JSON 更易改，放场景更抗版本**。

### 8.4.1 参数从哪来、怎么算（唯一权威来源）

> **这一节是全篇最该先读的部分。**
> 「特效不完整 / 层级混乱」这类症状，根因几乎从来不是渲染代码，
> 而是时间轴 JSON 里的 `z` 是手编的。**别再手写 z。**

#### (1) 原游戏的三层数据链

```
CNZ_Source/czn<id>/model_data/<id>.srcs   → command_sets    ux_attack / ug_attack / u1_attack …
                                                          由哪几条身体动画拼成
CNZ_Source/czn<id>/model_data/<id>.srmd   → command.<cmd>   演出时间轴本体，**是 JSON，不是二进制**
                                                          ani        身体动画 + 时长（身体相位主时钟）
                                                          effect     **组名**(file_name) + 整组变换
                                                          cutin / node_ani / hit / shake / cam_move /
                                                          cam_zoom / color_blend / bg_blend / damage …
        │  effect[].file_name 是「组名」，不是骨架名
        ↓
CNZ_Source/czn<id>/effect/<组名>.cfx       → primitive[]     **组展开：每层 { source, z, format }**
                                                          ← 层级真值就在这里
CNZ_Source/czn<id>/liveevent_data/<id>.le  → depth_infos     选人界面层序（front-to-back，Godot 要反过来）
CNZ_Source/czn<id>/model_setting/<id>.setting               render 800x640, scale 1.0, offset_y_ratio 8
```

- `.cfx` 是 **Apple plist**（XML），`plistlib.load()` 直接可读，**不是二进制**。
- `.srmd` 是纯 JSON，`json.load()` 直接可读。
- 30093 共 53 个 `.cfx`；参考 mod 的 `ux_all_attack.json`（285 KB / 72 层）
  就是从这套数据提取的，格式与本节**完全同构**。

> ⚠️ **`.cfx` 图元的空值语义：空 / 缺失 = 取默认，不是「无效」。**
> `.cfx` 里有两种写法并存：
>
> ```xml
> ① 全套 key，但值可能为空               ② 只写需要的 key，其余完全不出现
> <key>source</key><string>…ug_impact</string>   <key>source</key><string>…ug_screen_eff</string>
> <key>x</key><string />                          <key>format</key><string>spine</string>
> <key>z</key><string />
> <key>scale</key><string />   …
> ```
>
> 两种写法里 `x`/`y`/`z`/`opacity`/`scale`/`rotate`/`delay` 都可能是空，
> 语义都是「不指定 → 取默认」，**`z` 的默认是 0**。
> 真实的例子：`ug_attack_impact.cfx` 是空串写法，`ug_attack_screen.cfx` 是整条缺失写法，
> 两者的真值都是 **z = 0**。
>
> 把空 `z` 当成「无效图元」丢掉会让 UG 从 **43 层掉到 41 层**（`ug_impact`、`ug_screen_eff` 消失）。
> **真正该过滤的是粒子（`format = particle`），不是按 z 过滤。**

#### (2) 层级公式

```
final_z = group_zorder + primitive_z
```

`zorder` / `global_z` 是**组级元数据**（来自 srmd 的 effect 事件），
`primitive_z` 是**组内深度**（来自 cfx）。两者必须相加：

- Nine 全部 `zorder = 0`，所以它的 `z.computed == primitive_z`，容易误以为不用加；
- 30093 的 `ug_attack` 有 `zorder = -5000 / 0 / 200 / 300`，**必须加**，
  否则 `ug_attack_bg`（远景，zorder=-5000）会和前景层撞在同一深度。

真值举例（30093 `ux_attack`，由 `ux_attack_01.cfx` 展开）：

| 骨架 | final_z | 含义 |
|---|---|---|
| `ux_attack_03_1_1_1` | -10000 | 最深的背景 |
| `ux_attack_02_b3` | -1005 | 斩击残影底层 |
| `ux_attack_01_b1` | -991 | 斩击主体 |
| `ux_attack_03_4` | 899 | 余韵 |
| `ux_cutin_01` | 900 | **cut-in 立绘在最后面（最前）** |
| `ux_attack_02_f1` | 906 | 前景剑光 |
| `ux_attack_01_f` | 907 | 最前 |

> 手写时最容易错的就是 cut-in：直觉上会放到背景区，实际真值在 **900~905**，即最前。

#### (3) 时序公式

`from_guid` 是**播放树**，不是编辑器父子关系：

- **同父兄弟并行**起播，各用自己的 `delay`
- `wait_until_end` 只决定「本节点的子节点挂在父节点**结束**处还是**开始**处」
- `hit` 是终点标记，它的 `delay` 就是绝对毫秒（30093：ux=5266 / ug=2800），直接采信

两条独立证据支持这个读法：

1. `ug_attack` 的 5 个 effect 全是 entry 的直接子节点、`delay` 均为 0，
   而 `hit` 是同一层的兄弟、`delay = 2800` —— 若兄弟串行，hit 会被推到 `ani` 之后，
   与 srmd 记录的绝对时刻矛盾。
2. `ux_attack` 的 effect 反而是 `ani` 的子节点，于是自然从 **833.3ms**（身体动画结束）起播。

**同一份资产里两种形状并存，说明结构本身就是播放关系。**
把兄弟串行会得到 ux = 11.3 秒，与 `hit = 5266ms` 直接矛盾。

#### (4) 逐字段来源表（哪些能改、哪些不能改）

| 字段 | 来源 | 能否手改 |
|---|---|---|
| `z` | `srmd.effect.zorder` + `cfx.primitive.z` | ❌ 绝不可 |
| `scale` / `rotation` / `opacity` / `offset` | `srmd.effect`（scale 0.8/1.0/0.5 是真值，不是默认值） | ❌ 绝不可 |
| `target`（挂屏幕层还是世界层） | `srmd.effect.type`：`SCREEN`→屏幕层、`CENTER`→世界层、`SELF`→挂在角色上、`FRONT`→屏幕层、`TARGET`→世界层 | ❌ 绝不可 |
| `bone_type` | `srmd.effect.bone_type`（`ATTACH` = 挂骨骼，不是屏幕绝对坐标） | ❌ 绝不可 |
| `clip` | 骨架实测动画名（降级序 `animation` → `animation_nocam` → `loop` → `idle` → `play` → `ready` → `setup`） | ❌ 绝不可 |
| `start_ms` | 播放树推导 | ❌（单体特效表除外，见下） |
| `end_ms` | `start_ms` + 骨架实测 clip 时长 | ❌ 绝不可 |
| `duration_ms` | `max(所有层 end, hit, 身体动画结束)` | ✅ 可（只影响节奏） |
| `hit_ms` | `srmd.hit.delay` | ⚠️ 是真值，但可在 `renderer` 里覆盖做节奏 |
| `shakes[].start_ms` | `srmd.shake` 事件 | ❌ |
| `shakes[].duration/amplitude/frequency` | **人工**（见「还原不出的」） | ✅ 可 |
| `bg.alpha / fade_in_ms / fade_out_*` | **人工**（`srmd.bg_blend` 未映射到黑幕） | ✅ 可 |

> **单体技能特效表（`effects/<...>_moves.json`）是例外**：
> 它的 `start_ms`/`end_ms`/`clip` **保留原值，不要重新生成** ——
> 它们按身体动画相位对齐（ready 0.467 / play 0.667，与 srmd 的 `ani.duration` 一致），
> 且被 C# 侧 `HeidemarieAnimationSeconds` / `WithAttackerAnim` 依赖。
> 只用提取器回填 `z`，时序原样保留。

#### (5) 提取器

```powershell
$s='G:\sts2Mod\CNZ_Source\czn30093\model_data\30093.srmd'
$c='G:\sts2Mod\CNZ_Source\czn30093\effect'
$k='G:\sts2Mod\CznHeidemarie\CznHeidemarie\animations\effects'
$cam='G:\sts2Mod\CNZ_Source\czn30093-spine_4211\camera'
$v='G:\sts2Mod\tools\SpineSkeletonDataConverter.exe'
$cache='G:\sts2Mod\tools\_cinematic_duration_cache.json'

# 全屏演出（ux / ug）—— 整份重新生成
python tools\extract_cinematic_plan.py --srmd $s --cfx-dir $c --skel-dir $k `
    --camera-skel-dir $cam --converter $v --cache $cache `
    --command ux_attack --out CznHeidemarie\CznHeidemarie\cinematic\heidemarie_ux.json
python tools\extract_cinematic_plan.py --srmd $s --cfx-dir $c --skel-dir $k `
    --camera-skel-dir $cam --converter $v --cache $cache `
    --command ug_attack --out CznHeidemarie\CznHeidemarie\cinematic\heidemarie_ug.json

# 单体技能特效——只换 z，时序原样保留
python tools\extract_cinematic_plan.py --srmd $s --cfx-dir $c --command moves-z `
    --out CznHeidemarie\CznHeidemarie\effects\heidemarie_moves.json
```

产物的三段结构泾渭分明，便于日后复核：

| 段 | 内容 | 谁维护 |
|---|---|---|
| `layers[]` | 可直接渲染的参数 | **自动生成，别手改** |
| `unsupported[]` | 资产里还原不出的部分，逐条带原因 | 自动生成 |
| `missing[]` | `.cfx` 引用了但素材区没有的骨架 | 自动生成 |
| `z_collisions[]` | 两个阶段共用同一深度的层（多半是正常的分阶段演出） | 自动生成 |
| `renderer.known_gaps` | 人读的缺口清单 | 自动生成 |
| `renderer.corrections` | STS2 特有适配（黑幕、抖动、丢弃项） | 手调 |
| `official` | srmd 事件原文 + 推导规则 + 对照解 | 只读，供复核 |

#### (6) 三个 Godot 侧的硬约束（不改就白提 z）

1. **`z_as_relative` 必须关掉**（全屏演出）。
   `_stage` 与 `_screen` 是兄弟子树，默认相对模式下 **STAGE 的所有图层会整体画在
   SCREEN 的所有图层之前，z 再大也压不上去**，"前后景交织"直接退化成两段各自排序。
   > 单体特效舞台（挂在单位骨架下的那个 `Node2D`）**相对模式是对的**，保留 ——
   > 那里所有图层都是同父兄弟，相对 z 会叠加到单位的深度上。

2. **`z_index` 会被 Godot 截断到 ±4096**，而真值到 -10000（ux 最深）/ 100001
   （`u3_attack_target_flash` 组）。直接照搬会把一整片深层背景全压成 -4096，层级又乱。
   → 按**名次**线性压进 `[-4000, 4000]`（`_build_z_ranks`）：严格保序、同 z 并列。
   > 参考 mod 的 `spine.z_index = index - 100` **就是这个保序压缩**，不是随手写的；
   > 只是它的输入 z 是编的，压缩谁都一样错。

3. **黑幕必须低于最底层图层**。
   ux 真值最底层是 -10000，黑幕给 `-1000` 会被特效盖住；
   给 `-30000`（截断后 -4096）永远在最底。

#### (7) 素材本身还原不出的（不是 mod 的锅）

| 缺失项 | 说明 |
|---|---|
| `ux_attack_01_b1_3` / `_b1_4` / `_b2` / `ux_attack_02_b1_1` | 30093 的 `.cfx` 引用了，`CNZ_Source` 里**从未提取出**这 4 个骨架 |
| `ux_attack_01.pati`、`ug_attack_screen_pati1` | **CZNFX 粒子**（`.particle`），STS2 无 CZNFX 运行时 |
| `camera/*.skel` | **不是 Spine 骨架**，`SpineSkeletonDataConverter` 直接报 `Bad optional access`。抖动只能取 srmd 的**起止时刻**，时长/振幅/频率全靠人工标定 |
| `cam_move` / `cam_zoom` / `cam_spine` / `color_blend` / `node_ani` / `damage` | STS2 战斗场景无相机、无颜色分级、无对应节点 |

> **同组多图元的时序也是空的**：`30093_ug_portrait1_1..9` 与 `30093_ug_portrait2_1..3`
> 在 srmd 里同属一个 effect 事件（delay 0），`.cfx` 只给深度不给逐帧时序，
> 因此全部从 0 起播。原游戏大概率是逐帧接力（约 300ms 一张），**资产里没有依据，需进游戏标定**。

### 8.5 与游戏同步：Hook 拦牌

这是全屏动画**唯一侵入游戏逻辑**的地方，Two 处补丁：

```csharp
// 1) 卡牌开始结算前：把「等待原结果」换成「原结果 + 演出」
[HarmonyPatch(typeof(Hook), "BeforeCardPlayed")]
private static class ReleasePatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result)
        => __result = WaitForHooks(__result, () => PlayAndWaitAsync(cardPlay));
}

// 2) 卡牌结算完后：把特效尾巴挂上去，卡片不急着结束
[HarmonyPatch(typeof(Hook), "AfterCardPlayed")]
private static class TailPatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result)
    {
        Phase? phase = _phase;
        if (phase != null && phase.Play == cardPlay)
            __result = WaitForTail(__result, phase);   // await original; await phase.Completion.Finished;
    }
}
```

`PlayAndWaitAsync` 里 await 的是 `phase.Completion.Hit`（由 `hit_reached` 信号置位），
所以时间线是：

```
BeforeCardPlayed  ──await──▶ 演出 mount ──▶ hit_reached ──▶ 伤害/数值结算（游戏原生）──▶ AfterCardPlayed
                                                                                    └─ await 演出尾巴
```

要点：

- **`hit_release_seconds` 必须落在 `(0, duration]` 内且单调**：
  `duration > 0 && hit > 0 && hit <= duration && actorRelease >= hit && actorRelease <= duration`，
  不满足就整段放弃（Tiphera 就是这么校验 `get_runtime_contract()` 的）。
- 卡片「被演出盖住的等待」要能**穿透**：施法/防御/增益这些触发器的原生等待，
  在这段时间里直接返回 `phase.Completion.Hit`（Tiphera 的 `TryGetCoveredBodyWait`），
  否则会出现「演出在放，本体还在后台自己演一遍」。
- 演出期间**抑制其它演出**：`OwnsBody(player)` / `SuppressesBattleReady` 让
  出牌特写立绘、原生连击、语音都让位（Nine 会直接 `NineBattleReadyRuntime.Cleanup()`）。

### 8.6 生命周期与容错（照抄这几条就不会翻车）

| 机制 | 做法 |
|---|---|
| **token** | 每次演出递增一个 token，异步回调先比对 token，过期直接丢弃（防止上一段演出的延迟回调打到新演出） |
| **watchdog** | 每帧 `ProcessFrame` 检查：房间还在 / 玩家没死 / 设置没关 / token 还是当前；
  开场 10s（`StartMsec == 0`）或 `duration + 2s` 超时 → 强制 `End(cancelled: true)` |
| **tree_exiting** | 场景节点 `TreeExiting` 一定回调 `End(...)`，防泄漏 |
| **替换即取消** | 新演出 mount 前先 `Cancel(reason: "replacement")` |
| **收尾顺序** | 先 `Cancel(stage)` → 再恢复本体 → 再停音频 → 最后 `QueueFree()`；用 `try/finally` 保证本体一定恢复 |
| **音频尾巴** | 音频播完才释放音频节点，但**可视层先隐藏**（视觉不能等音频） |
| **全失败降级** | `TryBegin` 返回 `false` → 调用方走原生出牌演出（Tiphera 是 `TryBeginSupportRoute(compactFallbackOnly: true)`） |

### 8.7 战斗中的「全屏演出」开关（右侧圆形按钮）

参考 mod 在战斗界面**右侧（弃牌堆正上方）**放了一个开关按钮，
不用进设置就能开关自己的全屏演出。CznCore 已把这套 UI 完整搬过来：

| 组件 | 位置 | 职责 |
|---|---|---|
| `CombatCutInToggleHudService` | `CznCore/Presentation/` | 全局注册点：`Register(profile, host)` |
| `CombatCutInToggleHudPatches` | 同上 | 4 个 `NCombatUi` 补丁（`Activate` / `Enable` / `Disable` / `Deactivate`）驱动显隐 |
| `CombatCutInToggleHudRuntime` | 同上 | 建按钮 + 图标 + 悬停提示、放置到弃牌堆上方、点击切换 |
| `CombatCutInToggleHudProfile` | 同上 | 图标路径、尺寸、偏移、节点名后缀、悬停提示本地化键 |
| `ICombatCutInToggleHudHost` / `DelegateCombatCutInToggleHudHost` | 同上 | 角色侧只提供五件事：目标角色、可用性、读值、写值、节点名 |

**注册（照抄 Nine 的 `ConfigureCombatCutInToggleHud`）：**

```csharp
CombatCutInToggleHudService.Register(
    new CombatCutInToggleHudProfile(
        "res://<modid>/images/ui/cut_in_toggle_on.png",
        "res://<modid>/images/ui/cut_in_toggle_off.png",
        64f, -22f, -116f,                       // Size / OffsetRight / OffsetBottom
        "CombatCutInToggleHud", "CombatCutInToggleHudIcon",
        "characters",                            // 悬停提示本地化表
        "<SLUG>.cut_in_on.title", "<SLUG>.cut_in_on.description",
        "<SLUG>.cut_in_off.title", "<SLUG>.cut_in_off.description"),
    isTargetPlayer: player => player?.Character is MyCharacter,
    isAvailable:    () => <内容是否启用>,
    isCutInEnabled: () => settings.ShowCardPlayPresentation,
    setCutInEnabled: (enabled, persist) =>
    {
        settings.SetCombatPresentationMode(CombatPresentationMode.Custom, persist);
        settings.SetCardPlayPresentationEnabled(enabled, persist);
    },
    runtimeNodeName: suffix => "<ModId>" + suffix,
    warn: log.Warn);

foreach (Type t in CombatCutInToggleHudPatches.PatchTypes)
    harmony.CreateClassProcessor(t).Patch();
```

要点：

- **按钮读的就是设置里那一项**（`ShowCardPlayPresentation`），战斗里点一下 = 改设置；
  写值前必须同时把「战斗演出模式」拉成 `Custom`，否则下次套用预设会被整体覆盖。
- `isAvailable` 决定**按钮存不存在**（关掉「战斗特效」总开关时不该还留着按钮），
  与 `isCutInEnabled` 是两件事。
- 悬停提示键必须在 `characters` 表里，键名大小写与 `Id.Entry.ToUpperInvariant()` 一致。
- 参考 mod 另有「队友全屏演出」独立开关（设置面板里的 `teammate_cut_in_enabled`），
  那是多人专用，与这个按钮不是一回事。

### 8.8 与本工作 mod 的差异（`CznHeidemarie`）

`CznHeidemarie` 把「全屏演出 + 单体技能特效 + 战斗开关」按同一套架构落地，
适配见 `CznHeidemarie/动画与特效清单.md` 第七、八节。主要差异：

| 参考 mod | `CznHeidemarie` 的差异 | 原因 |
|---|---|---|
| 官方 cfx 时间轴 JSON | **同款提取器，但参数来源从 `.cfx` 升级到 `.srmd` + `.cfx`**（`tools/extract_cinematic_plan.py`） | 30093 的 `.srmd`（146 KB JSON）与 49 个 `.cfx`（plist）都在素材区且**可解析**，只是此前没人提取。提取规则见 8.4.1 |
| 官方 cfx 逐层占位/缩放 | **有**：`scale`（0.8/1.0/0.5）、`rotation`、`opacity`、`offset`、`type`、`bone_type` 全部取自 srmd 原值 | 8.4.1(1) 的三层数据链 |
| 逐帧接力时序 | UG 的 `portrait1_1..9` / `portrait2_1..3` 只能全部从 0 起播 | `.cfx` 只给深度不给逐帧时序，资产里没有依据 |
| Hook 拦牌（`BeforeCardPlayed` / `AfterCardPlayed`） | 不走 Hook：全屏演出订阅 `CardPlayCommittedEvent`、单体特效订阅 `CardPlayEnqueuingEvent` | 本工程演出**纯视觉叠加**，不参与数值结算时序，零侵入（与「出牌特写」同一策略）。代价是拿不到「等演出命中再结算」的同步 |
| 每个技能一套预制的 `.tscn` 特效场景 | 一个通用 `Node2D` 合成器 + 一份 JSON；舞台**挂到单位自己的骨架下**，靠继承变换免去坐标换算 | 挂到骨架上是最省事且等价的做法（相对 z 叠加到单位深度） |
| 官方 OGG 语音 / SFX | **无音频**（素材区只有 FMOD `.bank`，无法直接加载） | 留接口不接线 |

---

## 九、两个常被漏掉的扩展位

前八节讲的是「角色身上」的动画。还有两处**参考 mod 已经搭好、但往往没人接**的位置，
漏了就会出现「立绘画好了却没有剑影环绕」「选人界面立绘错位/是 default 皮肤」这类问题。

### 9.1 出牌特写的「待机特效层」（剑影）

出牌特写立绘（左侧那张）在 CznCore 里预留了一圈**环绕特效层**，接口就是
`BattleReadyOverlayProfile.IdleEffect`：

```csharp
IdleEffect: BattleReadyOverlayIdleEffectPlan.Scene(
    "res://scenes/<slug>_battle_ready_effects.tscn",
    rootPositionX: 0f, rootPositionY: 0f, rootScale: 1f)
```

工作机制（`BattleReadyOverlayRuntime.AttachBattleReadyIdleEffects`）：

```
① 加载 IdleEffect 场景
② 调 Context.DetachIdleEffectLayer(场景根, "Back") / ("Front")  ← 把两个子树摘出来
③ 场景根剩下的部分 QueueFree
④ Back  → 挂到「立绘 SpineSprite」下，ZIndex = -1，ShowBehindParent = true
   Front → 挂到「立绘 SpineSprite」下，ZIndex = +1
   容器位置 = (RootPositionX, RootPositionY)，缩放 = RootScale
⑤ 显示立绘时 SetBattleReadyIdleFireVisible(true, restartAnimation: true)
   → 立刻回调 Context.StartIdleEffects(容器)
```

五个必须知道的点：

1. **`Back` / `Front` 容器自身的 `position` / `scale` 会被运行时重置为原点** ——
   所以层与层之间的相对摆放只能靠**子节点的 `z_index`**，不能靠容器位移。
2. 若 `AttachBoneName` 留空，容器直接挂在**立绘骨架节点**下。因此只要特效骨架与
   立绘骨架用同一套坐标单位（同一角色的官方素材必然如此），`RootPosition = (0,0)`、
   `RootScale = 1` 就是对的 —— 不需要任何坐标换算。
3. `StartIdleEffects` **必须真的把骨架动画起起来**（`SetAnimation("animation", loop: true, 0)`），
   否则那几层会停在第 0 帧，看起来像没生效。
4. `ApplyIdleEffectMaterial` / `...Deferred` 用于替换预乘 alpha 材质。
   只做普通 alpha 的项目留空即可（两个参考 mod 都是空实现）。
5. **两个参考 mod 都把 `IdleEffect` 传成了 `null`、四个回调写成空方法** ——
   这个位子是「框架建好了、角色没接」。要复刻官方那种环绕效果，必须自己补。

常见素材映射（以 `animations/effects/` 的命名习惯为准）：

| 用途 | 名字特征 | 放哪层 |
|---|---|---|
| 特写后景（大刀影） | `battle_ready_eff_b` / `_b_mask` | `Back` |
| 剑刃残影（后 / 前 / 辉光） | `sword_eff_b` / `sword_eff_f` / `sword_eff_g` | `Back` / `Front` / `Front` |
| 特写前景光效 | `battle_ready_eff_f` / `_f_mask` / `_mask` | `Front` |

### 9.2 选人界面的立绘布局约定

游戏有三个背景分类（`CharacterSelectBackgroundVariant`）：
`StaticPortrait`（立绘）/ `TeamJoinPortrait`（动态立绘）/ `LiveLobby`（角色大厅）。
参考 mod 的三个变体场景结构完全一致：

```
Control（全屏、mouse_filter = 2）
├── BackgroundImage : TextureRect   ← 铺底图，anchors 全铺 + expand + cover
└── <Portrait>      : SpineSprite   ← 直接挂在 Control 下（不是 Node2D 里）
```

**布局算法**（参考 mod 的 `*_select_bg.gd`，两个 mod 一字不差）：

```gdscript
const BASE_SIZE := Vector2(1920, 1080)
var ratio = maxf(size.x / BASE_SIZE.x, size.y / BASE_SIZE.y)
portrait.position = size * 0.5 + (PORTRAIT_BASE_POSITION - BASE_SIZE * 0.5) * ratio
portrait.scale    = PORTRAIT_BASE_SCALE * ratio
```

即：场景里的 `position` / `scale` 都按 **1920×1080 设计坐标**标注，
运行时按 `cover` 比例换算成实际分辨率 —— 和全屏演出舞台的 `_layout()` 是同一个套路。

**两个致命坑：**

1. **`.tscn` 里的 `preview_skin` / `preview_animation` 只对编辑器预览生效**。
   不在运行时设皮肤，官方立绘会退化成 `default` 皮肤（人物部件错乱）；
   不在运行时起动画，就停在第一帧。参考 mod 的做法是：
   ```gdscript
   skeleton.set_skin_by_name("normal")   # 官方 face/portrait 的角色皮肤名
   skeleton.set_slots_to_setup_pose()    # 换皮肤后必须恢复 setup pose，否则残留旧附件
   animation_state.set_animation("idle", true, 0)
   ```
   并且**每帧重试**（骨架数据异步加载），成功后停掉 `_process`。
2. 官方立绘骨架在素材区是 **3.8.79 的 JSON**（`face/portrait/<id>.json`），
   项目里要用的是 4.2.11 的 `.skel`：
   ```powershell
   SpineSkeletonDataConverter.exe face\portrait\30093.json 30093.skel -v 4.2.11
   ```
   （或直接从已转换过的素材目录取 `.skel` + `.atlas` + `.png` 三件套。）

> 官方立绘骨架自带 `skeleton.width` / `skeleton.height`（setup pose 包围盒）与
> `skeleton.x` / `skeleton.y`（包围盒相对原点的偏移），是**唯一可靠的标定依据**：
> `缩放 = 目标高度 / skeleton.height`，`位置 = 目标锚点 - (skeleton.x, skeleton.y) * 缩放`。
> 别凭感觉估。

---

## 十、落地检查清单（新 mod 照此逐项打勾）

- [ ] `animations/body/<slug>_body.skel` + `.atlas` + `.png` + `_skel_data.tres` 四件套齐
- [ ] `scenes/creature_visuals/<slug>.tscn` 里 `Visuals` 节点存在，引用正确的 `_skel_data.tres`
- [ ] `scale` 已按骨架包围盒标定到 ≈275px
- [ ] `Heidemarie.cs` 式 `override GenerateAnimator`：所有游戏触发器都有映射，短动作都接了 `idle`
- [ ] 攻击卡都带 `WithAttackerAnim`；技能 / 能力卡在 `OnEnqueuePlayVfx` 触发
- [ ] 需要多段演出的卡走 `PlaySequence`
- [ ] 入场演出：订阅战斗开始事件后手动触发 `Entry`（游戏不会自动播；可先隐藏本体再用 2s 超时兜底显示）
- [ ] 商店 / 休息点 / 待机 / 受击的约定名重定向补丁就位
- [ ] 延迟秒数来自骨架实测，不是估算
- [ ] 所有动画名先 `HasAnimation` 判存在，再回退
- [ ] 全屏动画（终极技）：`cinematic/<route>.json` 时间轴 + `scenes/cinematic/<slug>_stage.tscn` 合成器 + `<Mod>CombatCinematicRuntime` 接入层齐备
- [ ] 全屏动画：`get_runtime_contract()` 的 `duration / hit / actorRelease` 单调性已在 C# 侧校验，不满足即整段放弃
- [ ] 全屏动画：token + watchdog + `TreeExiting` 三条容错都在；任一资源缺失能降级回原生出牌演出
- [ ] 全屏动画：任一层 `_skel_data.tres` 缺失时是**整段放弃**，不会半路断在半截
- [ ] 全屏动画：`z` **由 `.srmd` + `.cfx` 提取**，不是手写（`final_z = group_zorder + primitive_z`）
- [ ] 全屏动画：`scale`/`rotation`/`opacity`/`offset`/`type`/`bone_type` 取自 srmd 原值，不是默认值
- [ ] 全屏动画：合成器里 `z_as_relative = false`（`stage`/`screen` 是兄弟子树，否则前后景无法交织）
- [ ] 全屏动画：`.cfx` 真值超出 ±4096 时已做**保序压缩**（`index`→`[-4000,4000]`），不是直接照搬
- [ ] 全屏动画：黑幕深度低于最底层图层（ux 真值 -10000，黑幕给 -30000）
- [ ] 全屏动画：`hit_ms` 采信 `srmd.hit.delay`（ux=5266 / ug=2800），不是估的
- [ ] 全屏动画：产物里的 `missing` / `unsupported` / `renderer.known_gaps` 三段都读过，确认没有静默丢弃
- [ ] 单体技能特效：时间轴 `clip` 与骨架实测动画名逐条核对过（卡厄思特效骨架**跨招式复用**，写错 clip 会被静默跳过）
- [ ] 单体技能特效：舞台挂到单位骨架节点下（Node2D），而不是屏幕画布
- [ ] 单体技能特效：`z` 已按所属**身体相位**分别取真值（同一骨架在不同相位组里 z 不同，如 `eff_u1_attack_play_b` ready -100 / play -50）
- [ ] 单体技能特效：时序（`start_ms`/`end_ms`/`clip`）未被提取器覆盖——它按身体相位对齐且被 C# 依赖
- [ ] 战斗中的「全屏演出」开关：`CombatCutInToggleHudService.Register` + 4 个 `NCombatUi` 补丁 + `characters` 表里的四条悬停文案齐备
- [ ] 开关写值前先把战斗演出模式置为 `Custom`，否则会被预设覆盖
- [ ] 出牌特写的待机特效层：`IdleEffect` 不为 null，`DetachIdleEffectLayer` 能摘出 `Back` / `Front`，`StartIdleEffects` 真的起了循环动画
- [ ] 选人立绘：变体场景的 `position` / `scale` 按 1920×1080 设计坐标标注，且运行时**真的设了皮肤与动画**（`preview_*` 只对编辑器生效）
- [ ] 选人立绘：缩放按官方 `skeleton.width` / `height` 算出来，不是估的

---

## 附：Tiphera 动作名 → 触发器映射（节选）

```text
idle(loop) / b_idle(loop) / idle_to_b_idle / b_idle_to_idle
death_ready → death
hit
attack_ready → attack_play1 → idle
attack_ready → attack_play2 → idle
defense_ready → defense_play → idle
u1_buff_ready → u1_buff_play / u2_... / u4_... / u5_...
attack_ready → u3_attack_ready → u3_attack_play → u3_attack_end → idle
buff_ready → buff_play
enter_ready → enter_play → enter_end → idle
victory_ready → victory(loop)
```

对应代码：`CznTipheraCharacter/decompiled/CznTipheraCharacter.Characters.Tiphera/Tiphera.cs`。
