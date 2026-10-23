# STS2 Mod 工作区 — 路由与铁律

本目录是《杀戮尖塔2》Mod 开发的**工作区根**。

本文件只回答一件事：**什么情况用什么**。操作细节一律不在这里写。

| 文档 | 管什么 | 什么时候打开 |
|---|---|---|
| **`AGENTS.md`**（本文件） | 去哪查、用哪个工具、不可违背的铁律 | 每次开工先读 |
| **`开发手册.md`** | 怎么写、怎么做：目录骨架、资源契约、C# 写法、演出组装、打包流程、验证清单 | 动手改代码/资源/打包时 |
| **`参考mod动画实现清单.md`** | 参考 mod 的动画实现对照 | 对照参考 mod 做法时 |

---

## 〇、先读这张地图

### 目录：什么情况看哪个目录

| 目录 | 性质 | 什么时候看它 |
|---|---|---|
| `SlayTheSpire2_source/` | **游戏本体反编译源码** | 查 API 真实签名、字段、加载路径 |
| `SlayTheSpire2ModdingTutorials/` | 官方开发教程（中英双份） | 查标准写法 |
| `CNZ_Source/` | 卡厄思梦境原始素材（**只读**） | 找模型、卡图、特效、语音 |
| `CznTipheraCharacter/` | ★ **参考 mod A**（Tiphera，纯资源型，含 CznCore） | 抄场景树、资源目录布局 |
| `CznNineCharacter/` | ★ **参考 mod B**（Nine，纯资源型） | 抄场景树、Spine 目录约定 |
| `CznHeidemarie/` | ★ **本工作 mod**（自研 C# 型） | 日常开发 |
| `CZN_Viewer/` | Spine 动画查看器（单骨架浏览） | 核对骨架动画名 / 时长 / 包围盒 |
| `CZN_AnimLab/` | **场景检查器**（加载 tscn → 列模型 → 勾选启停 → 改参数 → 保存） | 调全屏演出 / 技能特效，免打包 |
| `tools/` | 批量脚本（`.tres` 生成、**演出计划提取**、素材提取） | 层级真值、资源产物从这里产出 |
| `STS2-RitsuLib-docs/` | RitsuLib 文档 | 将来改用 RitsuLib 时 |

### 三份资料的优先级

```
SlayTheSpire2_source/   ← 最权威，真实代码，签名以此为准
        ↓ 找对应章节
SlayTheSpire2ModdingTutorials/
        ↓ 找素材
CNZ_Source/
```

> ⚠️ 教程与实际源码不一致时**一律以源码为准**。

### ⚠️ 重要纠正：参考 mod 与本工作 mod 不是同一种东西

查代码前先分清，否则会抄错：

| | `CznTipheraCharacter` / `CznNineCharacter` | `CznHeidemarie` |
|---|---|---|
| 形态 | **GDRE 反编译产物**（`decompiled/` 里是反编译 C#，只读不可编译） | **自研 C# 工程**（`*.csproj` 可 `dotnet build`） |
| 代码来源 | 别人写的完整 mod，从游戏 pck 里还原 | 本工作手写 |
| 用什么基础库 | `CznCore`（原作者自研框架，在 `decompiled/CznCore/`） | **不用基础库**。直接引用游戏 `sts2.dll` + `0Harmony.dll` |
| 参考价值 | **资源布局 / 场景树 / 数值标定** | C# 结构基线 |

> `CznHeidemarie.csproj` 只引用三个游戏自带 DLL
> （`sts2.dll` / `0Harmony.dll` / `GodotSharp.dll`）+
> `Alchyr.Sts2.ModAnalyzers` 静态分析器。
> **没有 BaseLib，没有 RitsuLib。** `ModHelper` 是游戏自带的
> `MegaCrit.Sts2.Core.Modding.ModHelper`，不是第三方库。
> 网上教程里大量 BaseLib/RitsuLib 写法与本工程不符，**照抄前先对照源码**。

---

## 一、铁律速查（不可违背）

每条只有结论，依据与做法见「开发手册」对应章节。

| # | 铁律 | 详见 |
|---|---|---|
| 1 | **mod id ↔ 资源目录名 ↔ 命名空间 ↔ 角色类名 / slug 四者同源**。类名一改 ⇒ slug 变 ⇒ 场景、图标、音效、地图标记全套路径失配 | 手册 §1 |
| 2 | 素材区 `CNZ_Source/` **只读**，不往里写 | 手册 §2.1 |
| 3 | 骨架是 **Spine 4.2.11**，**禁止再次转换或改动**；动画在 `.skel` 里，PNG 只是皮肤 | 手册 §2.3 |
| 4 | 两个参考 mod 是**只读反编译产物**，**不要改动**；只抄资源布局/场景树/数值标定 | — |
| 5 | 只用游戏自带 DLL，**不用 BaseLib / RitsuLib** | — |
| 6 | Harmony 补丁**必须限定在本 mod 命名空间**，禁止整程序集 `PatchAll()`；所有补丁先判 `instance is <本角色>`，他人 `return true` 走原实现（零侵入） | 手册 §4.1 / §4.4 |
| 7 | `.gitignore` **绝不能含 `bin/`**，否则导出时骨架资源整体丢失 | 手册 §6.3 |
| 8 | Godot 必须是 **4.5.1-stable mono**，禁止用更高版本导出 | 手册 §6.1 |
| 9 | 全屏演出的 z **不要手写**，用 `tools/` 的提取器产出 | 手册 §5 |
| 10 | **联机时 CZN 设置以房主为标准**：只有「演出开关类」同步，音量/亮度/位置等本机口味不同步；房主的值**绝不落盘**；A 类白名单三处（准入 / 导出 / 导入）+ 备份必须一致 | 联机同步规范 §三 / §四 / §五 |
| 11 | **参与战斗 / 地图生成的本地设置必须走 `EffectiveXxx`**；表现等待不得进入 ActionQueue（联机时 `TryWaitForHit`/`WaitForPresentationEnd` 必须降级）；去重账本按 `NetId` 分桶，判本地玩家禁用 `Players[0]` | 联机同步规范 §五 |

---

## 二、按场景查什么

| 情况 | 去哪 |
|---|---|
| 查游戏 API 真实签名 / 字段 / 加载路径 | `SlayTheSpire2_source/`（先 codegraph，再 Read，见最后一节） |
| 查标准写法 | `SlayTheSpire2ModdingTutorials/`；与源码冲突以源码为准 |
| 找模型 / 卡图 / 特效 / 语音素材 | `CNZ_Source/`（只读） |
| 抄场景树 / 资源目录布局 | `CznTipheraCharacter/` `CznNineCharacter/` |
| 核对骨架动画名 / 时长 / 包围盒 | `CZN_Viewer/` |
| 调全屏演出 / 技能特效（免打包） | `CZN_AnimLab/` |
| 新增角色：要补哪些文件、放哪 | 手册 §1 目录骨架 + §3.1 必备资源清单 |
| 写 `CharacterModel` 子类 / 注册补丁 / 卡牌 / SFX | 手册 §4 |
| 角色**不动**、日志刷 `could not find 'idle_loop'` | 手册 §3.4（必须 override `GenerateAnimator`） |
| 场景节点改名导致动画失效 | 手册 §3.2（`%Visuals` 必须是 SpineSprite） |
| 角色大小不对 | 手册 §3.3（缩放标定） |
| 全屏演出层级乱 / 特效少层 | 手册 §5（z 真值在素材区，别手编） |
| 本地化不生效（名字/描述不显示） | 手册 §3.5 |
| 联机设置不同步 / 要加需要同步的设置项 / 碰 `CznSettingsSyncRuntime`、`CznMonsterConfigSyncRuntime` | `联机同步规范.md`（工作区根；分类规则、操作清单、硬约束、验证清单） |
| 打包：命令怎么写 | 手册 §6.6 标准流程 |
| 打包完怎么验收 | 手册 §7 验证清单 |
| 懂 API 用哪个类 | 手册 §8 关键类清单 |
| CznCore 框架 / 设置面板 / 补丁组 | 手册 §9 |
| 当前做到哪了 | 手册 §10 当前状态 |

---

## 三、codegraph 使用规则

工作区根有 codegraph 索引（`.codegraph/codegraph.db`，5505 文件 / 107k 符号），
覆盖 `SlayTheSpire2_source/` 与各 mod 工程。

**规则：**

1. **查 C# 代码优先用 codegraph，不要一上来就 Grep/Read。**
   先 `query` 定位符号，再 `Read` 只看那几行，省 token。
2. **改完 C# 必须同步**：`codegraph sync`（增量）。换了/新增 mod 工程用
   `codegraph index`（全量重建）。
3. **只在 C# 范围内用**。以下**不属于**索引范围，用 Glob / Grep / Read：
   Godot 资源（`.tscn` / `.tres` / `.gd`）、素材文件（`.skel` / `.atlas` / `.png` / `.bank`）、
   文档（`*.md`）、脚本（`tools/*.py` / `*.ps1`）。
4. **索引可能滞后**：刚改完没 sync 就查，结论会失真。
   **codegraph 与源码冲突时，一律以源码为准**，并跑一次 `sync` 修正。
5. 查**调用关系 / 影响面**用 `callers` / `callees` / `impact`；查**整体流程**用 `explore`。
   想知道「谁会因为我改这个而受影响」→ `impact`。

**命令：**

```powershell
codegraph sync                              # 增量同步（改完 C# 后跑）
codegraph index                             # 全量重建（换/加工程后跑）
codegraph status                            # 看统计
codegraph query "GenerateAnimator"          # 搜符号
codegraph node "SlayTheSpire2_source/src/Core/Models/CharacterModel.cs"   # 看单文件符号
codegraph explore "角色注册 ModelDb"        # 看源码 + 调用路径
codegraph files --filter CznHeidemarie      # 看目录树
codegraph callers <symbol>                  # 谁调用它
codegraph callees <symbol>                  # 它调用了谁
codegraph impact <symbol>                   # 改它会影响谁
```
