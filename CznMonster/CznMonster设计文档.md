# CznMonster 设计文档 —— 自定义怪物 + 行动计数机制

> 版本：v1.6（设计稿）
> 日期：2026-10-23
> 状态：机制已落地（v1.2）；v1.3 增补能力映射章节（§10）；v1.4 增补原生生命值模型（§3.3）
> 修订记录：
> - v1.1 按用户裁决修订——规则④改为"循环不止（防无限流）"；基础计数改为
>   等级（首领/精英/普通/召唤物）× 意图分类全表；计数器只随主动打出递减；
>   CznCore 拷贝后移除无关代码；v1 即做计数显示 Power（§7.7）；
>   血量文本输入采用扩框架 text 控件（§4.5）；逃跑/睡眠/眩晕/隐藏类意图不纳入计数（§7.4）
> - v1.2 基数改为公式化（§7.4）：`C(章数) × Tol(档位) × GroupMul(同场数) × AscMul(进阶)`，
>   意图降级为 ±0/1/2 的修正量；随从改用原生 `Creature.IsSecondaryEnemy` 判定。
>   本节内容与 `Registration/ActionCountPatches.cs` 现有实现一致；
>   已落地且无争议的过程性章节（CznCore 裁剪清单、验收测试、风险、裁决记录）移出本文档
> - v1.3 新增 §10「CZN ↔ STS2 能力映射」：裁定 CZN 词条到原生 Power 的对应关系，
>   并单列「脆弱 / 损伤」撞名警示，后续怪物与卡牌实现一律以此为唯一查表口径
> - v1.4 新增 §3.3「原生生命值模型」：血量真源、生成流水线（进阶 → 摇值 → 联机）、
>   进阶档位口径、联机缩放公式与倍率表、原生 Boss 血量对照；同步 §3.1 指针与附录 A 索引
> - v1.5 行动计数基数二次校准（用户裁决「全程稳定压力」）：`C` 斜率 0.8 → 0.3/章；
>   夹取移到 `IntentOff` 之后（意图修正不再突破 14，也不在小基数档被放大）；同步 §7.4 公式、参数依据、产出表、对照表与多怪示例
> - v1.6 首领放置规则重做（用户裁决）：删除「首领出现位置：随机」开关与 `BossPosRandom`（含联机协议字段）；
>   CZN 首领改为按固定章节入池（美梦→第 1 章、塞拉菲翁→第 2 章、波勒米卡/阿涅丝→第 3 章）后由原生 rng 抽取；
>   保留「指定章节」开关，语义改为原生抽完后强行替换；同步 §4.3 / §4.4 / §5.3 / §6.2 / §6.3 / §8
> 工作区：`g:/sts2Mod`，本文档所有源码引用均相对于该根目录

---

## 1. 需求整理（用户原始口径 → 设计口径）

| # | 原始需求 | 设计口径 |
|---|---|---|
| R1 | 使用 CZNCore 作为框架，继承框架的设置功能，其他角色相关功能移除 | 工程内拷贝 CznCore 源码（与 `CznHeidemarie/CznCore` 同构），只使用其**设置子系统**（`SkinSettingsUi.TryInjectShared` + `SharedSettingsExtensionRegistry`）；不调用 `CharacterSkinModBootstrap`、不注册角色 Profile、不引用角色相关运行时 |
| R2 | 设置：是否启用行动计数；启用后可设范围（1 所有首领 / 2 所有首领+精英 / 3 所有怪物） | 1 个开关 + 1 个三选下拉，下拉为开关的子级（父关子禁） |
| R3 | 设置：是否启用 CZN 怪物（默认关闭，关闭时下方控件不允许修改） | 1 个开关，默认关；其下控件（首领位置、章节、血量）全部挂 `parent_id` 级联禁用 |
| R4 | CZN 首领按固定章节入池（美梦→第 1 章、塞拉菲翁→第 2 章、波勒米卡/阿涅丝→第 3 章），由原生机制随机抽取；「指定」开启时在原生抽完后强行替换 | 默认无需开关（按章入池即默认行为）；保留 1 个「指定章节」复选框 + 章节下拉 + 首领下拉（均仅"指定"勾选时可用） |
| R5 | 首领血量：默认 / 自定义（文本输入），仅用于测试 | 默认/自定义开关 + 文本输入控件（按 §4.5 方案 B 扩框架新增） |
| R6 | 非首领怪进入对应怪物池，由原生系统自行抽取 | 补丁把自定义遭遇追加进章节遭遇列表，弱/普通/精英分流由 `RoomType` + `IsWeak` 决定 |
| R7 | 行动计数：怪物产生意图时赋予计数器；玩家每打出 1 张卡计数器 −1；归零怪物施放意图，随后重新产生意图并赋予新计数器；已行动的怪计数器归零仍可再行动，循环不止（防玩家无限流） | 补丁点：`RollMove`（赋予）→ `OnPlayWrapper`（仅主动出牌递减+触发中断）→ `TakeTurn`（中断去重） |
| R8 | 基础计数按怪物等级划分（首领多、普通/召唤物少）；联机总数 = 基数 × 玩家角色数 | 基数 = `C(章数) × Tol(档位) × GroupMul(同场数) × AscMul(进阶)`，意图只做 +0/1/2 修正（§7.4）；只随主动打出的卡牌递减 |

---

## 2. 总体方案

### 2.1 mod 形态

- **独立 C# 工程** `CznMonster`（与 `CznHeidemarie` 平级，互不依赖），命名空间 `CznMonster`，mod id `CznMonster`。
- 只引用游戏自带 DLL：`sts2.dll` / `0Harmony.dll` / `GodotSharp.dll` + `Alchyr.Sts2.ModAnalyzers`（铁律 5，与 `CznHeidemarie/CznHeidemarie.csproj:28-42` 一致）。
- 入口 `[ModInitializer("Init")]`（参考 `CznHeidemarie/MainFile.cs:15`）。
- 自定义 `MonsterModel` / `EncounterModel` 子类放进本程序集，由 `ModelDb.Init()` 自动扫描实例化，**无需手动注册**（依据 `CznHeidemarie/MainFile.cs:29-38` 的结论）。
- Harmony 补丁只扫本 mod 命名空间，逐类隔离失败（铁律 6，参考 `CznHeidemarie/MainFile.cs:61-95`）。

### 2.2 与 CznCore 的关系

CznHeidemarie 工程里已经整体搬入了 CznCore 源码（393 个 `.cs`，见 `CznHeidemarie/CznCore/`）。本 mod **同一套源码再拷一份**到 `CznMonster/CznCore/`，但接入面收窄为：

| CznCore 能力 | 本 mod 是否使用 |
|---|---|
| `SkinSettingsUi.TryInjectShared`（共享设置面板注入） | ✅ 核心 |
| `SharedSettingsExtensionRegistry` + `SettingsUiExtension*`（控件协议） | ✅ 核心 |
| `SkinSettingsUiTextFactory` / `SkinSettingsUiTextRuntime`（面板文案） | ✅ 直接复用默认英文文案，key 走本地化 |
| `SettingsLocalizationStore` / `SettingsLocalizedText` | ✅ 复用（换路径） |
| `CharacterSkinModBootstrap` / `CharacterSkinProfile` / 选人界面 / 战斗演出 / 语音 | ❌ 不调用（角色功能整体移除） |
| CznCore 自带的 `[HarmonyPatch]` 补丁组 | ❌ 不注册（本 mod 只挂自己的补丁） |

> 接入范式与参考 mod 一一对应：`CznTipheraCharacter/decompiled/CznTipheraCharacter/Entry.cs:21-37` 的 `SettingsReadyPatch` / `SettingsSubmenuPatch` + `TryInjectSettingsUi`。

### 2.3 模块划分

```
CznMonster
├── MainFile.cs                    入口：Harmony + 补丁扫描 + 设置注册
├── Settings/                      设置存储、文案、扩展注册
├── Registration/                  全部 Harmony 补丁 + 行动计数运行时
└── Content/                       怪物模型、遭遇模型、（可选）计数显示 Power
```

---

## 3. 游戏本体机制调研结论（设计依据）

以下均为反编译源码实证，行号即证据。

### 3.1 首领是如何选出来的

`ActModel.GenerateRooms()` 在 `SlayTheSpire2_source/src/Core/Models/ActModel.cs:331`，其中
  `_rooms.Boss = rng.NextItem(AllBossEncounters)`（`ActModel.cs:384`）摇出首领。
- `AllBossEncounters` = `AllEncounters.Where(e => e.RoomType == RoomType.Boss)`（`ActModel.cs:144,164`）。
- `RunManager.GenerateRooms()`（`src/Core/Runs/RunManager.cs:743`）遍历 `State.Acts` 逐章生成
  （`RunManager.cs:753-766`），**之后**才跑 `ApplyDiscoveryOrderModifications`（新手教程强制首领顺序，
  `RunManager.cs:757-760` → `ActModel.cs:404`）和双首领分支（`RunManager.cs:761-764`）。
  → **替换首领必须挂在 `RunManager.GenerateRooms` 之后**，否则会被教程顺序覆盖。
- 章节定位：`ActModel.Index`（`ActModel.cs:72`，0/1/2 = 第 1/2/3 章）。注意同一 index 可能有
  备选章节（Underdocks 也是 index 0，`src/Core/Models/Acts/Underdocks.cs:44`），
  因此"指定第 N 章"要按 `State.Acts[N-1]` 定位，而不是按 ActModel 类型。
- 原生首领清单（供参照）：
  - 第 1 章 Overgrowth：Vantom / CeremonialBeast / TheKin（`Acts/Overgrowth.cs:19-24`）
  - 第 2 章 Hive：TheInsatiable / KnowledgeDemon / KaiserCrab（`Acts/Hive.cs:17-22`）
  - 第 3 章 Glory：Queen / TestSubject / Aeonglass（`Acts/Glory.cs:16-21`）
- 运行时改写首领的现成 API：`ActModel.SetBossEncounter(EncounterModel)`（`ActModel.cs:466`，
  要求 `RoomType.Boss`）；控制台命令 `MapCmd.SetBossEncounter`（`src/Core/DevConsole/ConsoleCommands/MapCmd.cs:11`）
  演示了改完后刷新地图首领图标（`BossIcon.RefreshBossIcon()` + `NMapScreen.SetMap`）。
- 首领血量：`Creature` 构造时按 `MonsterModel.MinInitialHp/MaxInitialHp` 区间摇值
  （`src/Core/Entities/Creatures/Creature.cs:378-384`），随后联机再缩放
  （`Creature.cs:386-392` → `ScaleHpForMultiplayer`，`Creature.cs:749`）。
  → **自定义血量 = 让自定义 MonsterModel 的两个 HP 属性读设置（min==max 得确定值）**。
  完整链路、进阶与联机公式见 §3.3。

### 3.2 意图与回合流程

- 意图载体：`MonsterModel.NextMove`（`src/Core/Models/MonsterModel.cs:240`）是 `MoveState`，
  其 `Intents`（`src/Core/MonsterMoves/MonsterMoveStateMachine/MoveState.cs:17`）是
  `AbstractIntent` 列表，`IntentType` 枚举见 `src/Core/MonsterMoves/Intents/IntentType.cs:3`
  （Attack/Buff/Debuff/DebuffStrong/Defend/Escape/Heal/Hidden/Summon/Sleep/Stun/StatusCard/CardDebuff/DeathBlow/Unknown）。
- 摇新意图（产生意图）的唯一 choke point：`MonsterModel.RollMove(targets)`
  （`MonsterModel.cs:416-419`）。调用方有三处：
  1. 战斗初始化 `CombatManager.AfterCreatureAdded`（`src/Core/Combat/CombatManager.cs:1125-1131`）；
  2. 每回合开始 `Creature.PrepareForNextTurn`（`src/Core/Entities/Creatures/Creature.cs:547-559`，
     由 `CombatManager.cs:741-744` 对每个敌人调用）；
  3. 眩晕等强制改意图走 `MonsterModel.SetMoveImmediate`（`MonsterModel.cs:421`），**不经过 RollMove**。
- 敌人行动：`CombatManager.ExecuteEnemyTurn`（`CombatManager.cs:1402-1435`）对每个敌人
  先 `NCreature.PerformIntent()`（意图演出，`src/Core/Nodes/Combat/NCreature.cs:314`）再
  `Creature.TakeTurn()`（`Creature.cs:711-721`）→ `MonsterModel.PerformMove()`（`MonsterModel.cs:435`）。
- 回合序号：`CombatState.RoundNumber`，玩家回合开始→敌人回合→回到玩家回合时 +1
  （`src/Core/Combat/CombatState.cs:98-111`）。敌人回合内 `RoundNumber` 不变
  → 可用"中断时记录 RoundNumber == 当前 RoundNumber 则敌人回合跳过"实现防重复行动。
- 出牌流水线：`CardModel.OnPlayWrapper`（`src/Core/Models/CardModel.cs:1858-2019`）：
  `BeginCardOrPotionEffect`（`:1893`）→ 每次 play `Hook.BeforeCardPlayed`/`AfterCardPlayed`
  （`:1926,:1965`）→ `finally EndCardOrPotionEffect`（`:1975`）→ 牌堆移动/空手检查。
  → **"一张牌完全打完"的最佳观测点是 `OnPlayWrapper` 的 postfix**（此时卡牌效果深度已归零，
  不会出现"在卡牌效果中间插入怪物行动"的重入）。
- 卡牌计数参考实现：`SlowPower.AfterCardPlayed`（`src/Core/Models/Powers/SlowPower.cs:32-38`）——
  每打一张牌 +1，验证了 `AbstractModel.AfterCardPlayed` 这条路可用；但 power 钩子在卡牌效果
  **内部**触发，不适合直接表演出，故本设计不用 power 钩子做触发（详见 §5.3）。
- 联机人数：`CombatState.Players`（`CombatState.cs:81`）/ `RunState.Players`。
- 范围判定：`CombatState.Encounter`（`CombatState.cs:113`）给出当前遭遇，
  `EncounterModel.RoomType`（`src/Core/Models/EncounterModel.cs:51`）区分 Boss/Elite/Monster。
  注意事件战斗也是 `RoomType.Monster`（如 `src/Core/Models/Encounters/BattlewornDummyEventV1Encounter.cs:9`），
  需额外判 `RunState.CurrentRoom is CombatRoom` 排除。

### 3.3 原生生命值模型（血量来源 / 进阶 / 联机）

本节是 §6.4 与四份 `*Scaling.cs` 的标定依据。行号均为 `SlayTheSpire2_source/` 下的反编译源码。

#### 3.3.1 唯一真源：两个抽象属性

原生没有任何血量表或配置文件，所有怪物（含 Boss）只通过这两个属性声明血量：

```csharp
// src/Core/Models/MonsterModel.cs:56-58
public abstract int MinInitialHp { get; }
public abstract int MaxInitialHp { get; }
```

| 写法 | 结果 | 用例 |
| --- | --- | --- |
| `MaxInitialHp => MinInitialHp` | 固定血量，不随机 | 全部原生 Boss（除眷族随从） |
| Min/Max 给区间（如 `18~23`） | 区间内随机，且同一侧不重复 | 小怪、多体战 |
| 属性内叠加修正项 | 派生血量 | `Axebot` 的 `+ RespawnMaxHpBonus` |

#### 3.3.2 创建流程：进阶 → 摇值 → 联机

唯一入口是 `CombatState.CreateCreature`，开局生成、战斗中召唤、多体 Boss 的每一只都走这里：

```csharp
// src/Core/Combat/CombatState.cs:233-247
public Creature CreateCreature(MonsterModel monster, CombatSide side, string? slot)
{
    ...
    Creature creature = new Creature(monster, side, slot);
    List<Creature> creaturesOnSide = ((side == CombatSide.Player) ? _allies : _enemies);
    if (side == CombatSide.Enemy)
    {
        creature.SetUniqueMonsterHpValue(creaturesOnSide, RunState.Rng.Niche);
        creature.ScaleMonsterHpForMultiplayer(Encounter, Players.Count, RunState.CurrentActIndex);
    }
    ...
}
```

三步语义：

1. **读进阶后血量**：`Creature` 构造直接读两个属性（`Creature.cs:347-362`），若 `Min > Max` 抛
   `InvalidOperationException`（`Creature.cs:352-355`）。
2. **`SetUniqueMonsterHpValue`**（`Creature.cs:372-384`）：在 `[Min, Max]` 内取值，并剔除本侧已被占用的血量，
   保证同屏同种怪血量互不相同；候选集为空时退回纯随机。
3. **`ScaleMonsterHpForMultiplayer`**（`Creature.cs:386-393`）：仅 `playerCount != 1` 时生效，改 `MaxHp` 并把 `CurrentHp` 拉满。

两个实现细节：

- `MonsterMaxHpBeforeModification`（`Creature.cs:98,383`）保留摇值结果，UI 用它做
  「血量越高体型越大 / 色调偏移」的微随机（`src/Core/Nodes/Rooms/NCombatRoom.cs:698-707`），不参与战斗数值。
- 写回时 `Math.Min((int)amount, 999999999)`（`Creature.cs:494-502`）：**小数直接截断**，上限 9.99 亿。

调用面（均为同一入口）：`CombatRoom.cs:209`（开局布阵）、`CreatureCmd.cs:40,48`（战斗中召唤）、
`PlayerCmd.cs:238`（宠物）、`SurprisePower.cs:24`（原生召唤能力）、`EventCombatSynchronizer.cs:86`（联机事件战斗）。
→ **只要走 `CreateCreature` 就自动获得进阶与联机缩放，mod 无需自己调用。**

#### 3.3.3 进阶（Ascension）

共 11 档（0~10），枚举定义在 `src/Core/Entities/Ascension/AscensionLevel.cs:3-16`，上限 `maxAscensionAllowed = 10`
（`AscensionManager.cs:15`）：

| 值 | 枚举 | 中文 | 对血量的影响 |
| --- | --- | --- | --- |
| 0 | `None` | 无进阶 | 无 |
| 1 | `SwarmingElites` | 精英蜂拥 | 无（只改精英数量，`MapPointTypeCounts.cs:14`） |
| 2~7 | WearyTraveler / Poverty / TightBelt / AscendersBane / Inflation / Scarcity | 旅途劳顿 / 贫穷 / 收紧腰带 / 进阶之灾 / 通货膨胀 / 稀缺 | 无 |
| **8** | **`ToughEnemies`** | **强韧敌人** | **唯一加血量的进阶** |
| 9 | `DeadlyEnemies` | 致命敌人 | 无（只加伤害/格挡/力量获取） |
| 10 | `DoubleBoss` | 双重 Boss | 无（第 3 幕末多加一个 Boss 房） |

实现方式不是全局系数，而是**每只怪在自己的属性里按档位二选一**：

```csharp
// src/Core/Helpers/AscensionHelper.cs:22-29
public static int GetValueIfAscension(AscensionLevel level, int ascensionValue, int fallbackValue)
{
    if (!RunManager.Instance.HasAscension(level))
    {
        return fallbackValue;
    }
    return ascensionValue;
}
```

- 判定是「当前进阶 ≥ 该档」：`AscensionManager.HasLevel` 为 `_level >= (int)level`（`AscensionManager.cs:45-48`），
  入口 `RunManager.HasAscension`（`RunManager.cs:1707-1714`，非进行中一律 false）。
- 进阶等级是**整局共享的一个 int**（`RunState.AscensionLevel`），联机两端同一值，不存在各玩家一套。
- 血量提升是逐怪硬编码的具体数值，实测幅度约 +4%~+6%（例：Vantom 173→183、Aeonglass 512→535、
  Queen 400→419；TestSubject 每形态 +11%~+12% 属特例）。
- 进阶 10 只在最后一幕追加第二个首领遭遇，第二场血量按常规计算、不额外削弱
  （`RunManager.cs:761-765` → `ActModel.SetSecondBossEncounter`）。

#### 3.3.4 联机（多人血量缩放）

```csharp
// src/Core/Entities/Creatures/Creature.cs:749-756
public static decimal ScaleHpForMultiplayer(decimal hp, EncounterModel? encounter, int playerCount, int actIndex)
{
    if (playerCount <= 1)
    {
        return hp;
    }
    return hp * (decimal)playerCount * MultiplayerScalingModel.GetMultiplayerScaling(encounter, actIndex);
}
```

```csharp
// src/Core/Models/Singleton/MultiplayerScalingModel.cs:70-87
case 0: return 1.1m;
case 1: return 1.2m;
case 2: return (encounter != null && encounter.RoomType == RoomType.Boss) ? 1.3m : 1.2m;
default: throw new ArgumentOutOfRangeException("actIndex", actIndex, "Invalid act index for HP scaling");
```

**公式：`实际血量 = 进阶后血量 × 人数 × 幕系数`**

| 幕（actIndex） | 普通/精英房 | Boss 房 |
| --- | --- | --- |
| 第 1 幕（0） | ×1.1 | ×1.1 |
| 第 2 幕（1） | ×1.2 | ×1.2 |
| 第 3 幕（2） | ×1.2 | **×1.3** |

按人数展开的总倍率：

| 人数 | 第 1 幕 | 第 2 幕 | 第 3 幕普通/精英 | 第 3 幕 Boss |
| --- | --- | --- | --- | --- |
| 1 | ×1 | ×1 | ×1 | ×1 |
| 2 | ×2.2 | ×2.4 | ×2.4 | ×2.6 |
| 3 | ×3.3 | ×3.6 | ×3.6 | ×3.9 |
| 4 | ×4.4 | ×4.8 | ×4.8 | ×5.2 |

注意点：

- **Boss 没有专属多人系数**，唯一区别是第 3 幕 Boss 房用 1.3 而非 1.2。
- 人数取 `Players.Count`（房间内玩家总数，不是存活数）。
- 与进阶是**乘算叠加**：先进阶取值，再乘 `人数 × 幕系数`。
- 结果为 `decimal`，写回时截断为 `int`（`Creature.cs:500`）。例：Aeonglass 4 人第 3 幕 Boss 房
  = `512 × 4 × 1.3 = 2662.4` → 2662。
- **多体 Boss 是每只独立乘**，总血池随人数线性膨胀（女王 599 → 4 人 3114）。
- 与**格挡**缩放的差异：敌人格挡在 ≤2 人时只 `×人数`、且不乘幕系数，3 人以上才乘；
  且只对主要/次要敌人、来源为能力牌或怪物招式的格挡生效（`MultiplayerScalingModel.cs:52-68`）。

#### 3.3.5 原生 Boss 血量对照（A0~A7 / A8+）

第 1 幕（Overgrowth + Underdocks，两者 `Index` 都是 0）：

| Boss | 基础 | 进阶 8+ |
| --- | --- | --- |
| Vantom | 173 | 183 |
| CeremonialBeast | 252 | 262 |
| TheKin（祭司 190 + 随从 58~59 ×2） | 306~308 | 323~325 |
| WaterfallGiant | 240 | 250 |
| SoulFysh | 211 | 221 |
| LagavulinMatriarch | 222 | 233 |

第 2 幕（Hive）：

| Boss | 基础 | 进阶 8+ |
| --- | --- | --- |
| TheInsatiable | 321 | 341 |
| KnowledgeDemon | 379 | 399 |
| KaiserCrab（Crusher 209 + Rocket 199） | 408 | 428 |

第 3 幕（Glory）：

| Boss | 基础 | 进阶 8+ |
| --- | --- | --- |
| Queen（Queen 400 + TorchHeadAmalgam 199） | 599 | 630 |
| TestSubject（100 → 200 → 300 三形态） | 600 | 636 |
| Aeonglass | 512 | 535 |

血池阶梯：第 1 幕 173~308、第 2 幕 321~428、第 3 幕 512~636。**随幕增长约 ×2.3~×3.4，不是等比折减**——
这是自定义 Boss 章节系数必须按幕锚定、而不能简单乘 2/3、5/6 的原因。

#### 3.3.6 对本 mod 的约束

- 自定义 Boss 一律 `MaxInitialHp => MinInitialHp`（确定性血量），与原生 Boss 口径一致。
- 章节系数按 §3.3.5 的血池阶梯标定，四份 `*Scaling.cs` 的 `HpMul` 即该结论的落地。
- 自定义血量只替换**基础值**，仍乘章节系数（正确写法见 §6.4）；直接 `custom ?? Scaling.Hp(...)`
  会短路掉章节系数与进阶。
- 多体/召唤物的血量会各自独立吃联机倍率；若某个单位不应随人数膨胀（如用于机制计数的随从），
  参考原生 `DecimillipedeSegment.AfterAddedToRoom`（`src/Core/Models/Monsters/DecimillipedeSegment.cs:120-143`）
  在进场后用 `CreatureCmd.SetMaxAndCurrentHp` 重算的做法。

---

## 4. 设置系统设计（CznCore 框架接入）

### 4.1 注入机制

沿用参考 mod 的两处补丁 + 共享注入：

```csharp
// Registration/SettingsInjectPatch.cs
[HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
private static class SettingsReadyPatch
{
    private static void Postfix(NSettingsScreen __instance) => TryInject(__instance);
}

[HarmonyPatch(typeof(NSettingsScreen), "OnSubmenuOpened")]
private static class SettingsSubmenuPatch
{
    private static void Postfix(NSettingsScreen __instance) => TryInject(__instance);
}

private static void TryInject(NSettingsScreen screen)
{
    // 复用框架默认文案工厂；key 走 res://CznMonster/localization/{lang}/settings.json
    SkinSettingsUi.TryInjectShared(screen, MonsterSettingsUiText.Runtime);
}
```

依据：`CznHeidemarie/CznCore/Settings/SkinSettingsUi.cs:338`（`TryInjectShared` 只渲染共享扩展区，
不建角色页——正符合"移除角色相关功能"）；文案默认实现由
`CznHeidemarie/CznCore/Settings/SkinSettingsUiTextFactory.cs:7` 的 `Create(...)` 提供。

### 4.2 扩展注册协议

框架通过 AppDomain 数据接收各 mod 的设置控件（协议实现在
`CznHeidemarie/CznCore/Settings/SharedSettingsExtensionRegistry.cs`）：

- 数据键：`"CznModConfig.SharedSettingsExtensions.v1"`（`SharedSettingsExtensionRegistry.cs:21`），
  值为 `ConcurrentDictionary<string, object>`，key = providerId，value = 描述字典。
- 描述字典字段：`api_version=1`、`category_id`（不能是保留字 `general`/`multiplayer`，
  `SharedSettingsExtensionRegistry.cs:269-276`）、`category_label`、`category_order`、
  `group_label`、`group_order`、`reset`（可选 Action）、`controls`（数组）。
- 控件类型**仅三种**（`SharedSettingsExtensionRegistry.cs:104-110`）：
  - `toggle`：`get:Func<bool>` / `set:Action<bool>`
  - `choice`：`get:Func<int>` / `set:Action<int>` / `options:[{id:int,label}]`
  - `slider`：`get:Func<double>` / `set:Action<double>` / `min,max,step,suffix,display_offset`
- 每个控件都可带：`label`、`description`、`order`、`enabled:Func<bool>`（动态置灰）、
  `parent_id`（父控件；UI 会把子级渲染为缩进且在父关闭时禁用，
  依据 `CznHeidemarie/CznCore/Settings/SkinSettingsUi.cs:2057-2071`）。
- 文案 `label`/`description` 可以是 `Func<string>` 或 `string`；`Func` 形式可接本地化。

注册代码形态：

```csharp
// Settings/SettingsExtensionRegistrar.cs
// 在 MainFile.Initialize 中调用一次；设置界面打开时框架才读取，注册时机只需早于玩家打开设置
private static void Register()
{
    var providers = AppDomain.CurrentDomain.GetData(SharedSettingsExtensionRegistry.RegistryDataKey)
        as ConcurrentDictionary<string, object> ?? new ConcurrentDictionary<string, object>();
    providers["CznMonster"] = new Dictionary<string, object>
    {
        ["api_version"] = 1,
        ["category_id"] = "czn_monster",                 // 与角色 mod 的 visual/combat 分开，避免撞车
        ["category_label"] = (Func<string>)(() => Text.MonsterCategory.Current),
        ["category_order"] = 10,
        ["group_label"] = (Func<string>)(() => Text.MonsterGroup.Current),
        ["group_order"] = 0,
        ["reset"] = (Action)(() => MonsterSettings.ResetToDefaults()),
        ["controls"] = new object[] { /* 见表 4-1 */ }
    };
    AppDomain.CurrentDomain.SetData(SharedSettingsExtensionRegistry.RegistryDataKey, providers);
}
```

### 4.3 设置项 → 控件映射（表 4-1）

| 设置键 | 控件 | 标签（示意） | 默认 | parent / enabled | 说明 |
|---|---|---|---|---|---|
| `action_count_enabled` | toggle | 启用行动计数 | 关 | — | 总开关；与"启用 CZN 怪物"正交（计数可作用于原生怪） |
| `action_count_scope` | choice | 行动计数范围 | 1 所有首领 | parent=`action_count_enabled` | 选项：1=所有首领、2=所有首领+精英、3=所有怪物 |
| `czn_monsters_enabled` | toggle | 启用 CZN 怪物 | **关** | — | 关闭时下方全部禁用 |
| `boss_pos_specified` | toggle | 首领出现位置：指定章节 | 关 | parent=`czn_monsters_enabled` | 关闭（默认）= 按固定章节入池、原生 rng 抽取；开启 = 原生抽完后强行替换所选章节 |
| `boss_chapter` | choice | 指定章节 | 1 | parent=`boss_pos_specified`，enabled=`boss_pos_specified` | 选项：1=第 1 章、2=第 2 章、3=第 3 章 |
| `boss_select` | choice | 指定首领 | 0 随机 | parent=`boss_pos_specified`，enabled=`boss_pos_specified` | 选项：0=随机（默认）、1..N 按 `MonsterContent.BossCatalog` 顺序，标签取 encounters 表 title |
| `boss_hp_custom` | toggle | 首领血量：自定义 | 关 | parent=`czn_monsters_enabled` | 关=用怪物原生血量；开=用下方数值（仅测试用） |
| `boss_hp_value` | text（方案 B 新增） | 自定义血量 | — | parent=`boss_hp_custom`，enabled=`boss_hp_custom` | 整数 1–9999，非法输入不落盘 |

### 4.4 级联的实现方式

- **级联禁用**：子控件填 `parent_id` + `enabled` 谓词（读父开关）。父 toggle 的 `set` 写入后框架
  会自动重建面板（`SkinSettingsUi.cs:2105-2109` 的 `write → rebuild()`），子级 `enabled` 被重新求值，
  因此"关闭时下面控制不允许修改"零成本成立。
- **不再有互斥**：v1.6 删除「首领出现位置：随机」后，位置只剩「指定章节」一个开关，
  `boss_pos_random` / `BossPosRandom` / 联机消息字段 `bossPosRandom` 已全部移除。

### 4.5 "文本输入"控件（方案 B——扩框架 text 控件）

`SharedSettingsExtensionRegistry.cs:104-110` 只解析 `toggle/slider/choice` 三种控件，没有自由文本输入。
采用**方案 B**：在工程内 CznCore 副本新增 `"text"` 控件类型，改动集中在 3 处：

1. **新增记录** `CznCore/Settings/SettingsUiExtensionText.cs`（仿
   `CznHeidemarie/CznCore/Settings/SettingsUiExtensionStringChoice.cs`）：
   `record SettingsUiExtensionText(string Id, int Order, Func<string> Label, Func<string> Description, Func<string> Read, Action<string> Write, Func<bool> Enabled) : SettingsUiExtensionControl(...)`，
   同样支持 `ParentId`。
2. **注册表加分支**：`SharedSettingsExtensionRegistry.TryParseControl` 的 switch 加
   `"text" => TryParseText(...)`；`TryParseText` 校验 `id/label/get(Func<string>)/set(Action<string>)`，
   可选 `description/order/enabled/parent_id`（解析逻辑照抄 toggle 的
   `SharedSettingsExtensionRegistry.cs:113-132`）。
3. **UI 加行构建器**：`SkinSettingsUi` 加 `TryCreateTextSection`（仿 `SkinSettingsUi.cs:2112` 的
   `TryCreateStringChoiceSection`），用 Godot `LineEdit`（`TextSubmitted`/`FocusExited` 时
   `Write` + `rebuild()`），并在 `TryCreateSettingsExtensionControl`（`SkinSettingsUi.cs:2076-2110`）
   里加 `control is SettingsUiExtensionText` 分支。

mod 侧注册描述：

```text
{ "type": "text", "id": "boss_hp_value",
  "label": ..., "description": ...,
  "get":  (Func<string>)(() => MonsterSettings.BossHpValueText),
  "set":  (Action<string>)(v => MonsterSettings.BossHpValueText = v),
  "order": 8, "parent_id": "boss_hp_custom",
  "enabled": (Func<bool>)(() => MonsterSettings.CznMonstersEnabled && MonsterSettings.BossHpCustom) }
```

实现约束：

- 文本解析为整数并夹取 1–9999；非法输入不落盘，保持上次合法值；
- 失焦/回车即存（`ConfigFile` 立即写盘，§4.6）；
- 这是对 CznCore 副本的**唯一允许改动**（副本约定"只删不改"，只此一处例外）；
  与 `CznHeidemarie/CznCore` 同步时以对方为基准，重新删 + 重新加这 3 处。

### 4.6 存储与本地化

- 存储：Godot `ConfigFile`，路径 `user://CznModConfig/CznMonster/monster.cfg`
  （目录约定与参考 mod 一致，`CznTipheraCharacter/decompiled/CznTipheraCharacter/TipheraOffColorSettings.cs:12-13`）。
  写入策略：每次 `set` 立即落盘（设置量极小）。
- 设置面板文案：`SettingsLocalizationStore("res://CznMonster/localization/{0}/settings.json", warn)`
  （范式同 `CznHeidemarie/Integration/SettingsLocalization.cs:14-16`），语言目录 `zhs` / `en`。
- 游戏内本地化（怪物名、遭遇名）：`res://CznMonster/localization/<lang>/monsters.json`、
  `encounters.json`，与 `CznHeidemarie/localization/zhs/*.json` 同约定（表名 `monsters` / `encounters`，
  依据 `MonsterModel.cs:395-398`、`EncounterModel.cs:388-391`）。

---

## 5. CZN 怪物内容设计

### 5.1 怪物模型契约（`Content/Monsters/*.cs`）

每个怪物一个 `MonsterModel` 子类，必须实现：

| 成员 | 要求 | 依据 |
|---|---|---|
| `MinInitialHp` / `MaxInitialHp` | 首领款读设置（见 §6.4）；普通款写死或按 Ascension | `MonsterModel.cs:56-58` |
| `GenerateMoveStateMachine()` | 返回 `MonsterMoveStateMachine`（含 `MoveState` 与 `Intents`） | `MonsterModel.cs:408` |
| `VisualsPath` | 默认 `creature_visuals/<id小写>`；用现成素材时 override | `MonsterModel.cs:75` |
| `GenerateAnimator` | 骨架动画名与素材不一致时必须 override（铁律：角色不动/日志刷 `could not find 'idle_loop'` 的根因） | `MonsterModel.cs:464-481` |
| SFX / 死亡动画 / 缩放 | 按素材实际情况 override | `MonsterModel.cs:151-190` |

> v1 建议：怪物数量 1 只首领 + 2~3 只非首领（1 普通 + 1 精英 + 1 弱）足够验证全部机制；
> 视觉优先从 `CNZ_Source/`（只读）挑选现成骨架拷贝进工程，或复用游戏内怪物视觉换皮。

### 5.2 遭遇模型契约（`Content/Encounters/*.cs`）

| 成员 | 要求 |
|---|---|
| `RoomType` | 首领款 `Boss`；普通款 `Monster`；精英款 `Elite` |
| `IsWeak` | 弱遇敌 true（进前 3 间弱池）；普通款 false |
| `AllPossibleMonsters` / `GenerateMonsters()` | 返回怪物 + 槽位（`EncounterModel.cs:150,245`） |
| `Slots` | 多怪时给槽位名（`EncounterModel.cs:171`） |
| `TargetActIndex`（自定义字段） | 0/1/2 或 -1（全章节）；入池时过滤用 |

### 5.3 遭遇入池（R6）

补丁 `ActModel.get_AllEncounters` postfix，把本 mod 的遭遇按章节追加：

```csharp
// Registration/MonsterPoolPatch.cs
[HarmonyPatch(typeof(ActModel), "get_AllEncounters")]
private static class AllEncountersPatch
{
    private static void Postfix(ActModel __instance, ref IEnumerable<EncounterModel> __result)
    {
        if (!MonsterSettings.EffectiveCznMonstersEnabled) return;      // 总闸（联机读房主快照）
        // 非首领 + 首领：统一按 TargetActIndexes 过滤章节后入池
        //   美梦→act0、塞拉菲翁→act1、波勒米卡/阿涅丝→act2
        // 首领只注入运行中的 mutable act：canonical act 供图鉴使用，注入会重复
        foreach (var e in MonsterContent.NonBossEncounters.Concat(MonsterContent.BossEncounters))
            if (e is not ICznMonsterEncounter c || AppliesToAct(c, __instance.Index))
                extra.Add(e);
        if (extra.Any()) __result = __result.Concat(extra);
    }
}
```

- `AllWeakEncounters/AllRegularEncounters/AllEliteEncounters/AllBossEncounters/AllMonsters` 全部由
  `AllEncounters` 派生（`ActModel.cs:144-169`），**一个补丁覆盖全部池子**，弱/普通/精英/首领分流由
  `RoomType`+`IsWeak` 自动完成，后续抽取完全走原生 `GenerateRooms` 的 GrabBag / `rng.NextItem` 逻辑
  （`ActModel.cs:348-384`）。
- **首领入池只对 mutable act 生效**：图鉴（`NBestiary.AddAct`）读的是 `ModelDb.Acts` 的 canonical 实例，
  不注入则图鉴仍由 `CznMonsterBestiaryPatch` 单独补区块，不会重复。
- 首领按**固定章节**入池（v1.6）：池子里除该章 CZN 首领外还有原生首领，是否出现由原生 rng 决定，**不保证必出**；
  同局是否重复也交给原生语义（A10 第二首领从第 3 章池中排除已选者后再抽，可能是阿涅丝/波勒米卡或原生）。

---

## 6. 首领出现位置与血量（R4/R5）

### 6.1 钩子点

**`RunManager.GenerateRooms()` postfix**（`src/Core/Runs/RunManager.cs:743`）。选它的理由：
它跑在每章 `act.GenerateRooms` + 教程首领顺序（`RunManager.cs:757-760`）+ 双首领分支
（`RunManager.cs:761-764`）**全部之后**，是"本章首领已最终确定"的唯一稳定时刻。
已知缺口：控制台 `ActConsoleCmd` 单独换 act 的路径（`src/Core/DevConsole/ConsoleCommands/ActConsoleCmd.cs:50`）
不走这里——dev 专用命令，暂不覆盖（记入风险）。

### 6.2 默认：按固定章节入池（原生抽取）

默认行为**不写任何代码去改首领**：四只 CZN 首领由 §5.3 的 `MonsterPoolPatch` 按各自的
`TargetActIndexes` 并入对应章节的 mutable act（美梦→第 1 章、塞拉菲翁→第 2 章、波勒米卡/阿涅丝→第 3 章），
`AllBossEncounters` 随之包含它们，随后由原生 `ActModel.GenerateRooms` 的
`_rooms.Boss = rng.NextItem(AllBossEncounters)`（`ActModel.cs:384`）决定：

- 每层池子 = 该层原生首领 + 该层的 CZN 首领，**是否出现 CZN 首领不保证**；
- 第 3 章池子同时含波勒米卡与阿涅丝；A10 第二首领从排除已选者后的池子再抽；
- 同一只首领在同局重复出现是允许的（原生语义，不做跨章去重）；
- `state.Acts[i]` 天然处理备选章节（Underdocks/Overgrowth 都是 index 0）。

### 6.3 指定模式（原生抽完后强行替换）

```csharp
// RunManager.GenerateRooms 的 postfix：原生三章首领都已定好后
private static void ApplySpecified(RunState state)
{
    ActModel act = state.Acts.FirstOrDefault(a => a.Index == MonsterSettings.BossChapter - 1);
    // boss_select = 0 时在未出现的 CZN 首领里随机抽一只；否则用玩家指定的那只
    EncounterModel boss = MonsterContent.GetSelectedBoss(MonsterSettings.BossSelect)
        ?? state.Rng.UpFront.NextItem(pool);
    act.SetBossEncounter(boss);                          // ActModel.cs:466
}
```

- 指定模式**不改变入池范围**：三章都先走原生抽取（可能已抽到 CZN 首领），随后只覆盖所选章节；
  其余章节严格保持原生抽取结果。
- `boss_select` 下拉：0=随机（默认），1..N 对应 `MonsterContent.BossCatalog` 顺序，
  名称在渲染时取 `EncounterModel.Title.GetFormattedText()`，随语言自动切换。
- 若指定的那只已在本局其他章节出现（读档后重复应用等），跳过替换、保留原生。

### 6.4 血量覆盖（仅测试）

自定义首领的 `MonsterModel` 读设置，且**自定义值只替换基础值**，章节系数与升华照常参与：

```csharp
public override int MinInitialHp
{
    get
    {
        int baseHp = MonsterSettings.EffectiveCustomBossHp
            ?? AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 升华值, 普通值);
        return XxxScaling.Hp(baseHp);   // 仍乘章节系数
    }
}
public override int MaxInitialHp => MinInitialHp;   // 相等 → 确定值
```

- **不要写回** `custom ?? Scaling.Hp(...)`：`??` 短路会让开关打开后整段绕过章节系数与升华，
  默认值 999 会在第 1 章直接出场（原生第 1 章上限 262）。
- `Creature` 构造在 `[min,max]` 摇值（`Creature.cs:378-384`），min==max 时结果确定。
- 联机仍会走 `ScaleMonsterHpForMultiplayer`（`Creature.cs:386-392`）——与原生首领行为一致，
  自定义值语义为"单人、第 3 章基准血量"（先乘章节系数，再叠加原生多人缩放）。
- 联机读 `EffectiveCustomBossHp`（房主快照，`MonsterSettings.cs:310-320`），保证两端同值。
- 只影响 CZN 首领；原生首领不碰。

### 6.5 边界

- 双首领（Ascension 10+）：只替换第一首领，二首领维持原生。
- 读档：首领已存进 `SerializableActModel.BossId`（`src/Core/Rooms/RoomSet.cs:138`），
  `FromSave` 恢复（`ActModel.cs:510-515`），不重跑替换 → 存档天然保持一致。
- 改完首领后地图图标刷新：仅运行时热替换需要（控制台路径），走 `MapCmd` 范式
  （`src/Core/DevConsole/ConsoleCommands/MapCmd.cs:11-18`）。开局路径地图尚未生成，无需刷新。

---

## 7. 行动计数机制（R7/R8）

### 7.1 规则（设计细化）

```
① 怪物产生意图（RollMove）且被计数 → 赋予计数器 = 基数公式（章数 × 档位 × 同场数 × 进阶，再加意图修正，§7.4）× 玩家数；
   逃跑/睡眠/眩晕/隐藏类意图不计数，按原生敌人回合时机处理
② 任意玩家每"主动打出"1 张卡（OnPlayWrapper 完成；自动打出的卡不计数）→ 所有在场计数怪 计数器 −1
③ 某怪计数器 ≤ 0 → 立即（当前玩家回合内）演出并施放该意图
   → 重新产生意图（PrepareForNextTurn：RollMove + 刷新意图图标）→ 赋予新计数器
④ 已通过 ③ 行动过的怪不受"一回合一次"限制：新计数器归零即再次行动，该循环一直持续到玩家停止出牌
   （设计目的：防止玩家无限流——出牌越多，怪物行动越频繁，没有免费回合）
⑤ 玩家提前结束回合而计数器未归零 → 怪物在敌人回合按原生流程执行当前意图（尚未执行过的意图）
⑥ 敌人回合不重复执行 ③ 正在排队/执行中的意图（实现层去重，见 §7.6——这是防双演，不是"休息"规则）
```

### 7.2 运行时状态

```csharp
// Registration/ActionCountRuntime.cs（静态类，无 Instance）
internal static class ActionCountRuntime
{
    // 计数器：key 用 Creature 实例（每场战斗唯一，随 CombatEnded 清理）
    private static readonly Dictionary<Creature, int> Counters = new();
    // 中断去重标记：当前意图已排队/正在被中断链执行的怪（敌人回合据此跳过，避免同一意图双演）
    private static readonly HashSet<Creature> InterruptPending = new();
    // 防重入门：中断链进行中，迟到的一张牌不再新起一波（由收尾循环统一扫）
    private static bool _draining;

    // 两个战斗生命周期事件都只做清空
    public static void OnCombatSetUp(CombatState state) => ResetCombat();
    public static void OnCombatEnded() => ResetCombat();

    // 随从判据：直接吃游戏原生的“次要敌人”语义
    public static bool IsSummon(Creature c) => c.IsSecondaryEnemy;

    public static void Assign(Creature c, int value);  // 写字典 + SyncPower（value ≤ 0 走 Clear）
    public static void Clear(Creature c);              // 删字典 + 删标记 + RemovePower
    public static List<Creature> DecrementAll();       // 全部 -1，返回本次归零者
    // MarkInterruptQueued / HasPendingInterrupt / TakeZeroedQueued / DrainAsync 见 §7.5
}
```

- 生命周期：`CombatSetUp` / `CombatEnded` 都调用 `ResetCombat`（清计数器、去重标记、关门标志）。
  两个事件均为 public（`src/Core/Combat/CombatManager.cs:245-260`）。
- **随从判定**改用 `Creature.IsSecondaryEnemy`（`Creature.cs:269`，即挂了 `MinionPower` 的单位，
  主敌全灭时会自动死亡）。不要再用"是否在开战快照里"：开局自带的真正随从
  （`GasBomb`、`KinFollower`、`Ovicopter` 的蛋）本来就在快照里，而分裂史莱姆、复活怪这类
  中途进场的普通怪反而不在快照里，两种都会判错。
- 联机确定性：计数器只在"每张主动打出的牌结算完成"这一个同步点增减，中断演出只调用走命令/行动队列的 API
  （`PerformMove`/`PrepareForNextTurn` 内部均为 `Cmd.*`），各客户端顺序一致。

### 7.3 计数赋予（patch 1）

```csharp
// Registration/ActionCountPatches.cs（P4）
[HarmonyPatch(typeof(MonsterModel), "RollMove")]
internal static class ActionCountRollMovePatch
{
    private static void Postfix(MonsterModel __instance)
    {
        if (!MonsterSettings.ActionCountEnabled) return;
        if (__instance.Creature is not { } creature) return;
        if (__instance.CombatState is not CombatState cs) return;          // NullCombatState 短路
        if (!ActionCountTables.IsCountedCreature(__instance, cs, out MonsterTier tier)) return;

        int? baseCount = ActionCountTables.CounterFor(__instance, cs, tier, __instance.NextMove);
        if (baseCount == null)
        {
            // 逃跑/睡眠/眩晕/隐藏：清掉可能残留的旧计数器与显示 Power，按原生流程走
            ActionCountRuntime.Clear(creature);
            return;
        }
        // 联机缩放放在最后：先算单人基数（含夹取），再 × 玩家数（§7.9）
        ActionCountRuntime.Assign(creature, baseCount.Value * Math.Max(1, cs.Players.Count));
    }
}
```

档位判定在 `ActionCountTables.IsCountedCreature` 里，与范围判定一起做（§7.8）：
随从 → `Creature.IsSecondaryEnemy`，其余按 `Encounter.RoomType`（Boss / Elite / Monster）。

覆盖全部"产生意图"路径：开战首摇（`CombatManager.cs:1130`）、每回合开始
（`CombatManager.cs:741-744`）、中断后自重随。`SetMoveImmediate`（眩晕）不经过这里，
被眩晕的怪本回合不计数、按原生流程行动（可接受，记入边界）。

### 7.4 基数公式（章数 × 档位 × 进阶 × 同场数）

计数器是**阈值**而不是**倒计时**：它决定玩家这一回合多打几张牌，会多换来一次怪物行动。
所以基数不能拍绝对数，必须相对"玩家一回合能打几张牌 `C`"来定。四个输入各管一段：

| 输入 | 负责什么 | 进公式的方式 |
|---|---|---|
| 章数 `RunState.CurrentActIndex` | 玩家一回合能打几张牌 `C` | 只由它决定 `C` |
| 档位 `tier` | 一次额外行动值多少（每回合允许多几次行动 `A`） | 只由它决定 `A` |
| 进阶 `RunState.AscensionLevel` | 对结果整体收税 | 乘 `AscMul` |
| 同场同档主敌数 `n` | 多怪分摊预算 | 乘 `GroupMul` |
| 意图 | 防滚雪球微调 | 加 `IntentOff` |

```text
C(act)    = 3.0 + 0.3 × actIndex              // 第 1/2/3 章 = 3.0 / 3.3 / 3.6
Tol(tier) = 1 / A(tier)                       // 首领 2.00 / 精英 1.67 / 普通 1.33 / 随从 0.85
GroupMul  = 1 + 0.4 × (min(n, 4) − 1)         // n = 同房间同档存活主敌数；随从恒 1
N0 = round(C(act) × Tol(tier) × GroupMul × AscMul)
N  = clamp(N0 + IntentOff, 2, 14) × CombatState.Players.Count
```

**参数依据**：

| 项 | 取值 | 依据 |
|---|---|---|
| `C` | 3.0 / 3.3 / 3.6 | 本体 `CharacterModel.MaxEnergy => 3`（`src/Core/Models/CharacterModel.cs:97`）、`baseHandDrawCount = 5`（`CombatManager.cs:41`）：3 能量 / 5 抽牌 / 均费 1 费 ≈ 3 张；逐章只 +0.3 —— 能量遗物的增量大多被后期均费上升（打击/防御 1 费 → 主力牌 1.2~1.5 费）吃掉，抽牌引擎只解决「手牌够」而不解决「能量够」（v1.5 校准） |
| `A` | 首领 0.50 / 精英 0.60 / 普通 0.75 / 随从 ≈1.18 | "每只怪每回合允许多出几次行动"。首领一次额外行动最贵（伤害与血量都是普通怪的数倍），所以允许次数最少 |
| `AscMul` | A0–4 ×1.00；A5–7 ×0.95；A8 ×0.90；A9–10 ×0.85 | 按本体进阶的实际影响点分档（`localization/zhs/ascension.json`）：A5 诅咒 / A6 移除涨价 / A7 少好牌 → 玩家出牌能力下降；A8 `ToughEnemies` 敌血 +4~5%；A9 `DeadlyEnemies` 伤害 +10~15%；A10 `DoubleBoss` |
| `GroupMul` | n=1→1.0；2→1.4；3→1.8；4→2.2 | 多怪必须分摊预算：3 只普通怪若各自独立计数，玩家打出第 4 张牌会同时逼出 3 次额外行动 |
| 取整 / 夹取 | `AwayFromZero`；**先加 `IntentOff` 再夹 `[2, 14]`** | 低于 2 等于"每两张牌动一次"；高于 14 只有无限流能触发。夹取放在意图修正之后：意图修正不能突破上限，也不会在随从这种小基数档被相对放大（+1 对 3 层 = +33%） |

**产出（单人、A0，未加意图修正）**：

| 档位 | 第 1 章 | 第 2 章 | 第 3 章 |
|---|---|---|---|
| 首领 | 6 | 7 | 7 |
| 精英 | 5 | 6 | 6 |
| 普通 | 4 | 4 | 5 |
| 随从 | 3 | 3 | 3 |

**产出（单人、A10，×0.85）**：

| 档位 | 第 1 章 | 第 2 章 | 第 3 章 |
|---|---|---|---|
| 首领 | 5 | 6 | 6 |
| 精英 | 4 | 5 | 5 |
| 普通 | 3 | 4 | 4 |
| 随从 | 2 | 2 | 3 |

**意图修正（`IntentOff`）**——基准与意图种类无关，只修补会滚雪球的两种：

| 意图 | 修正 | 理由 |
|---|---|---|
| `Attack`、`DeathBlow` | **+1** | 额外的一次攻击是爆发伤害，最容易直接打死玩家 |
| `Buff` | **+2** | 额外的一次 buff 是永久成长，随战斗回合数复利（防"玩家打牌 → 逼出第二次仪式"的死亡螺旋） |
| `Defend`、`Debuff`、`DebuffStrong`、`CardDebuff`、`StatusCard`、`Heal`、`Summon`、`Unknown` | 0 | 不滚雪球 |
| 多意图 | 各可计数意图修正的**平均**（四舍五入） | 例："攻击 + 减益" = (1+0)/2 = 0.5 → +1 |

加修正后的对照（单人、A0，攻击类意图）：

| 档位 | 旧表（全章固定） | 第 1 章 | 第 2 章 | 第 3 章 |
|---|---|---|---|---|
| 首领 | 7 | **7** | 8 | 8 |
| 精英 | 6 | **6** | 7 | 7 |
| 普通 | 4 | 5 | 5 | 6 |
| 随从 | 3 | 4 | 4 | 4 |

（增益类在上表基础上再 +1：首领 8 / 9 / 9，精英 7 / 8 / 8，普通 6 / 6 / 7，随从 5 / 5 / 5。）

- **v1.2 为什么公式化**：旧表全章固定，而玩家出牌数随章节变化，同一档位在不同章的压力会漂移；公式化后用 `C/N` 对齐，同一档位在三个章的目标压力一致。
- **v1.5 为什么二次校准**：`C` 原斜率 **+0.8/章** 假设「能量增长 = 出牌张数增长」，但后期均费上升（打击/防御 1 费 → 主力牌 1.2~1.5 费）会把能量增量吃掉，抽牌引擎只解决「手牌够」而不解决「能量够」，单回合主动出牌数实际只涨约一成。而计数器**每回合由 `RollMove` 重掷**（§7.3），只认单回合爆发、不跨回合累积 —— 基数每 +1 都是整章门槛的直接抬高：第 3 章首领要一回合打 11 张、普通怪 7 张，普通构筑几乎摸不到，机制后期存在感归零。用户裁决目标是**全程稳定压力**，故斜率压到 **+0.3/章**。
- **校准后的跨章漂移**（按 `P = C` 估算）：首领 0.43 / 0.41 / 0.45，精英 0.50 / 0.47 / 0.51，普通 0.60 / 0.66 / 0.60，随从 0.75 / 0.83 / 0.90。剩余误差来自整数取整，随从档基数最小（3~4）所以最明显。
- **绝对压力水平**由 `Tol` 与 `IntentOff` 决定，全章一致；若要整体更频繁触发，调 `Tol` 而不是调章节斜率。
- **多怪示例**：第 1 章 3 只普通怪 → `GroupMul = 1.8` → 基数 7、攻击类 8；第 3 章同配置 → 9 / 10。

**意图分类（15 种 `IntentType` 分两类：可计数 11 种，不计数 4 种）**：

可计数意图（赋予计数器、参与递减与中断）：

| 分类 | 覆盖的 IntentType | 修正 |
|---|---|---|
| 攻击类 | `Attack`、`DeathBlow` | +1 |
| 增益类 | `Buff` | +2 |
| 防御类 | `Defend` | 0 |
| 减益类 | `Debuff`、`DebuffStrong`、`CardDebuff`、`StatusCard` | 0 |
| 功能类 | `Heal`、`Summon`、`Unknown` | 0 |

**不计数意图（4 种）——不赋予计数器、不递减、不触发中断，按原生敌人回合时机处理**：

| IntentType | 不计数的理由 |
|---|---|
| `Escape` | 逃跑是脚本性/条件性行为（如 FatGremlin 被打醒后直接进 FLEE_MOVE 循环逃跑，`src/Core/Models/Monsters/FatGremlin.cs:53-55,67-77`）。若纳入计数，玩家刷 3~4 张牌就能逼逃，而逃跑怪不计入击杀、直接压低金币收益（`src/Core/Models/EncounterModel.cs:368-371`），奖励平衡被击穿 |
| `Sleep` | 睡眠怪不行动，意图只是"睡着"的状态展示；睡醒后自然换意图、重新计数 |
| `Stun` | 眩晕怪当回合空过，没有可演出的行动，提前"播完"没有意义 |
| `Hidden` | 隐藏怪不行动、不可选中，同上 |

- 若全部意图都不可计数（如纯 Escape/Stun），不赋予计数器（`CounterFor` 返回 `null`）。
- 联机计数 = `(N0 + IntentOff) × CombatState.Players.Count`（R8 原文口径）。

**等级判定**：

- 随从：`Creature.IsSecondaryEnemy`（`Creature.cs:269`，即挂了 `MinionPower` 的单位，
  主敌全灭时会自动死亡）。开局自带的随从（`GasBomb`、`KinFollower`、`Ovicopter` 的蛋）
  与战中召唤的随从都归入随从档；分裂史莱姆、复活怪这类中途进场的普通怪不再被误判；
- 其余按遭遇类型：`RoomType.Boss` → 首领档，`RoomType.Elite` → 精英档，
  `RoomType.Monster` → 普通档（实现见 `ActionCountTables.TierOf`）。

### 7.5 递减与归零执行（patch 2）

触发点：`CardModel.OnPlayWrapper`（`src/Core/Models/CardModel.cs:1858`，是 `async Task`）。
**不能用普通 postfix 直接减**：postfix 在方法第一次 `await` 前就返回，牌其实还没结算完。
实现把 `__result` 换成组合 Task：先等整张牌结算完成，再在**同一个 await 链内**完成减询
与打断收割，然后原动作（`PlayCardAction.ExecuteAction` await 的就是 `OnPlayWrapper`）
才算执行完：

```csharp
[HarmonyPatch(typeof(CardModel), "OnPlayWrapper")]
internal static class ActionCountCardPlayPatch
{
    private static void Postfix(CardModel __instance, bool isAutoPlay, ref Task __result)
    {
        if (isAutoPlay || __result == null) return;      // 自动打出（Mayhem 等）不减（R7）
        Task playTask = __result;
        __result = PlayThenSettleAsync(playTask, __instance);
    }

    private static async Task PlayThenSettleAsync(Task playTask, CardModel card)
    {
        try
        {
            await playTask;                              // 等整张牌（含消耗/丢弃/奇巧）结算完
            await ActionCountRuntime.AfterCardPlaySettledAsync(card);
        }
        catch (Exception ex)
        {
            // 吞掉并记日志：不让打断逻辑把 PlayCardAction 本身打成 faulted 而改变动作执行语义
            MainFile.Log.Warn($"CznMonster 行动计数减询失败：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

> **多人一致性（v0.4.1 修正，引发过 desync）**：
> 旧实现取 `__result` 后用 `TaskHelper.RunSafely` fire-and-forget 启动打断链。
> 打断的实际执行时机（意图动画 → 行动 → 重摇）因此会漂到回合结束校验点
> （`after player turn phase two end` 等）之后，两端谁先跑完取决于帧率/网络。
> 实测病例：回合最后一张牌恰好把计数清零，一端在握手间隙里完成了
> 额外一刀 + 重摇（计数刷新 + 多消耗一次 MonsterAi RNG），另一端停在此计数 0，
> checksum 在 #92 直接劈叉（行动计数 10 vs 0、玩家 HP/格挡不同步、MonsterAi counter 2 vs 1）。
> 组合 Task 后，打断被折进出牌动作自己的执行窗口：所有端都在同一个动作同步点完成，
> 下一个队列动作（含回合结束握手）必然先等到它结束。

`ActionCountRuntime.AfterCardPlaySettledAsync` / `DecrementAndDrainAsync`：

```csharp
public static async Task AfterCardPlaySettledAsync(CardModel card)
{
    CombatManager cm = CombatManager.Instance;
    if (!cm.IsInProgress || cm.IsOverOrEnding) return;
    if (!MonsterSettings.ActionCountEnabled) return;

    // 不能用 card.CombatState：能力牌与复制体打完后会离开战斗堆，Pile 变 null，
    // CombatState 随之返回 null（CardModel.cs:1018-1029），这类牌就会漏减。
    // CardScope 是原生兜底链：CombatState ?? Owner.Creature.CombatState ?? RunState（CardModel.cs:1038）。
    if (card.CardScope is not CombatState cs) return;

    await DecrementAndDrainAsync(cs);
}

public static async Task DecrementAndDrainAsync(CombatState cs)
{
    List<Creature> zeroed = ActionCountRuntime.DecrementAll();   // 每张主动打出的卡 −1
    if (zeroed.Count == 0) return;
    ActionCountRuntime.MarkInterruptQueued(zeroed);              // 同步打标记（防双演，§7.6）
    await ActionCountRuntime.DrainAsync(cs);                     // 同步收割，不入后台
}
```

`DrainAsync` 内部循环（对每个归零怪，执行前重新校验；仅在结算动作窗内被 await 调用）：

```csharp
public static async Task DrainAsync(CombatState cs)
{
    if (_draining) return;                 // 已有中断链在跑：由它的收尾循环统一收扫
    _draining = true;
    try
    {
        while (true)                       // §7.1④：一直循环到没有归零怪（防无限流）
        {
            List<Creature> ready = TakeZeroedQueued();   // 计数 ≤ 0 且带排队标记
            if (ready.Count == 0) break;
            if (!cm.IsInProgress || cm.IsOverOrEnding) break;
            foreach (Creature c in ready)
            {
                if (!IsActionable(c, cs)) { InterruptPending.Remove(c); continue; }
                // 演出：意图高亮（与敌人回合同款，NCreature.cs:314）
                Task? intentTask = NCombatRoom.Instance?.GetCreatureNode(c)?.PerformIntent();
                if (intentTask != null) await intentTask;
                // 施放当前意图
                await c.Monster.PerformMove();          // MonsterModel.cs:435
                InterruptPending.Remove(c);             // 本次执行完成，清标记
                if (!cm.IsInProgress || cm.IsOverOrEnding) break;
                if (!IsActionable(c, cs)) continue;
                c.PrepareForNextTurn(cs.PlayerCreatures);   // 重摇 → RollMove 补丁赋新计数
            }
        }
    }
    finally { _draining = false; }
}
```

要点：

- **打断必须在动作窗内完成（多人铁律）**：打断链由出牌动作的组合 Task await 到结束，
  绝不允许 fire-and-forget 漂在校验点之后（v0.4.1 起修正，参见上文 desync 病例）。
- **计数 Power 同步**：`Assign` 与 `DecrementAll` 内部顺带 `SetAmount` 刷新显示层（§7.7），不新增补丁。
- **循环不止**：`while` 的条件是"还有归零且带标记的怪"——某怪刚演完重随出的新计数器若在同一波里
  已被后续出牌打到归零，会继续演，对应 §7.1④（防玩家无限流）。
- **多怪同张牌归零**：串行 awaited，顺序确定；收尾循环保证"关门期间到达的归零"最终被扫到。
- **牌堆为空的能力牌**：必须走 `CardScope` 取状态，否则能力牌打出后计数器不递减（已实测）。
- **玩家/怪物中途死亡或换阶段**：每步前查 `IsOverOrEnding` / `IsActionable`
  （`IsAlive && Monster != null && CombatState != null && cs.ContainsCreature`）；
  `PrepareForNextTurn` 另包 try/catch——异蛙寄生虫死亡生成扭动虫、瀑布巨兽换阶段时，
  `NCreature.RefreshIntents` 会访问已为 null 的 `Entity.CombatState`。
- **不计数的两张牌**：药水（不走 `OnPlayWrapper`）；自动打出的卡（`isAutoPlay == true`，
  R7 只认主动打出）。

### 7.6 敌人回合去重（patch 3）

```csharp
[HarmonyPatch(typeof(Creature), "TakeTurn")]
private static class TakeTurnPatch
{
    // 该怪的当前意图已排队/正在被中断链执行 → 跳过（Harmony prefix 返回 false 即跳过原方法）。
    // 注意：这不是"一回合只能动一次"的休息规则，只是防止同一意图被执行两次——
    // 中断链保证会把该意图演完，随后重随出的新意图仍会在之后的敌人回合正常执行。
    private static bool Prefix(Creature __instance) =>
        !ActionCountRuntime.HasPendingInterrupt(__instance);
}
```

- **覆盖的竞态**：玩家在"计数器归零"与"中断链真正开跑"之间立刻结束回合。此时若放行 `TakeTurn`，
  敌人回合会和中断链各演一次同一意图。§7.5 步骤②同步打上标记后，由中断链保证执行，`TakeTurn` 跳过。
- **正常路径无感**：中断链在玩家打出下一张牌之前就跑完，标记已清除；敌人回合正常执行
  "最后重随出、尚未演过"的意图（§7.1⑤）。
- **不存在休息限制**：按 §7.1④，本回合已通过 ③ 行动过的怪，新计数器归零会在玩家回合内继续行动；
  敌人回合执行的是当时尚未演过的最新意图，两者互不屏蔽。
- 未计数的怪（范围外/眩晕改意图）没有标记，行为与原生一致。

### 7.7 计数显示（Power，v1 即做）

被计数的怪身上挂一个自定义 Power，用 `Amount` 显示剩余计数并随计数器实时刷新——
玩家不翻日志也能直接看到"再打几张它就要动"。

```csharp
// Content/Powers/CznActionCountPower.cs
public sealed class CznActionCountPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;        // 用 Buff：Debuff 会被 Artifact 挡掉，显示层不能被打断
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool ShouldScaleInMultiplayer => false;  // 联机倍率已在计数器里乘过
}
```

运行时同步点（全部挂在已有补丁里，不新增补丁）：

| 时机 | 动作 |
|---|---|
| RollMove 后赋予计数器 | 怪没有该 Power 则 `ModelDb.Power<CznActionCountPower>().ToMutable().ApplyInternal(creature, value)`（`PowerModel.cs:555,564`，public，可同步调用；**不能写 `ToMutable(value)`**：`ToMutable` 设 Amount 时 Owner 仍为 null，会 NRE）；已挂过则直接 `power.SetAmount(counter)`（`PowerModel.cs:542`，触发 `DisplayAmountChanged` 刷 UI） |
| OnPlayWrapper 递减后 | 对每个计数怪 `power?.SetAmount(新值)` |
| 中断后重随 | 同第一行（RollMove 后自然覆盖） |
| 意图转为不计数（逃跑/睡眠/眩晕/隐藏） | `Clear`：移除计数器、排队标记与显示 Power（`PowerModel.cs:575`） |
| 战斗结束 | Power 随怪/战斗自动清除，无需手动移除 |

注意：

- 直接 `ApplyInternal`/`SetAmount` 绕过命令队列，但本 Power **纯显示、无任何钩子副作用**，
  且各客户端在同一同步点执行同样代码，端上结果一致（需联机实测确认）。
- 战斗中关闭设置：已挂的 Power 保留到战斗结束（值停更），可接受；下次 RollMove 重新赋值时自动覆盖。
- 怪中途死亡：Power 随怪移除。

#### 7.7.1 图标与文案契约

- 图标三件套（256 大图 + 64 小图 + 图集条目），路径约定同 CznHeidemarie 的能力图标
  （`CznHeidemarie/build.ps1:198-221`）：`images/powers/czn_action_count.png`、
  `images/packed/powers/czn_action_count.png`、`images/atlases/power_atlas.sprites/czn_action_count.tres`。
- 本地化（`powers` 表）：`czn_action_count.title` / `czn_action_count.description`，
  description 用 `{Amount}` 占位（`PowerModel` 会自动注入 Amount）。

### 7.8 范围判定

```csharp
// Registration/ActionCountPatches.cs（ActionCountTables）
public static bool IsCountedCreature(MonsterModel monster, CombatState cs, out MonsterTier tier)
{
    tier = MonsterTier.Normal;
    Creature? creature = monster.Creature;
    if (creature == null) return false;
    if (cs.RunState?.CurrentRoom is not CombatRoom) return false;   // 排除事件战斗，仅地图战斗房间

    RoomType roomType = cs.Encounter?.RoomType ?? RoomType.Monster;
    int scope = Math.Clamp(MonsterSettings.ActionCountScope, 1, 3);
    if (!ScopeAllows(roomType, scope)) return false;                // 1=首领 / 2=+精英 / 3=所有

    if (ActionCountRuntime.IsSummon(creature))                      // 随从：IsSecondaryEnemy
    {
        tier = MonsterTier.Summon;
        return true;
    }
    switch (roomType)
    {
        case RoomType.Boss:    tier = MonsterTier.Boss;   return true;
        case RoomType.Elite:   tier = MonsterTier.Elite;  return true;
        case RoomType.Monster: tier = MonsterTier.Normal; return true;
        default: return false;
    }
}
```

### 7.9 联机

- 计数 = 单人基数 × `CombatState.Players.Count`（R8）。例：第 1 章首领攻击意图单人 7 →
  2 人 14、3 人 21；第 3 章首领攻击意图单人 11 → 2 人 22、3 人 33。
- 倍率放在公式最后一步，**不参与 `[2,14]` 夹取**（先算单人值并夹取，再乘人数），
  否则多人局的基数会被上限吃掉。这和本体的多人缩放口径一致：
  `MultiplayerScalingModel.GetMultiplayerScaling`（`src/Core/Models/Singleton/MultiplayerScalingModel.cs:70`）
  对敌方格挡同样是 `玩家数 × 1.1~1.3` 的线性思路。
- 递减发生在每个客户端的同一同步点（牌局动作全网执行），中断链只调命令队列 API，
  不引入本地独有状态 → 确定性成立。此为前提假设，需联机实测验收。

### 7.10 边界清单

| 场景 | 行为 |
|---|---|
| 玩家提前结束回合、计数未归零 | 敌人在敌人回合正常行动一次；下回合开始重随 + 新计数（⑤） |
| 一回合内多次归零 | 每次归零立即行动一次并重随，循环到玩家停止出牌（④）；回合结束后敌人回合只演最后尚未演过的意图 |
| 自动打出的卡（Mayhem 等） | 不递减计数器（只认主动打出） |
| 逃跑/睡眠/眩晕/隐藏意图 | 不赋予计数器、不递减、不触发中断；按原生敌人回合时机处理（§7.4） |
| 从可计数意图换成不计数意图 | RollMove 时 `Clear` 清掉残留计数器与显示 Power，不会意外触发中断 |
| 归零后立刻结束回合 | 敌人回合跳过该意图（去重标记），由中断链完成执行——该意图照演一次，不双演 |
| 一张牌打死计数怪 | 下一步前 `IsActionable` 跳过；`PerformMove` 内死亡移除逻辑兜底 |
| 打断中玩家被怪打死 | 循环内 `IsOverOrEnding` 熔断 |
| 召唤物（战中新增怪） | 出场当回合不摇意图（`CreatureCmd` 以 `rollNewMove:false` 建怪，`src/Core/Commands/CreatureCmd.cs:74`），下回合开始随大流计数；档位由 `IsSecondaryEnemy` 判为随从（§7.4） |
| 同场多怪 | 基数按同档存活主敌数放大（`GroupMul`），随从恒按 1 只算；怪死掉后下一次 RollMove 自动回落（§7.4） |
| 战斗中换章/读档 | `actIndex` 与 `AscensionLevel` 每次赋值时现读，不做缓存 |
| 眩晕（StunInternal → SetMoveImmediate） | 不计数，敌人回合按原生行动；眩晕解除后回主循环 |
| 事件战斗（木桩等） | 不计数（非 CombatRoom） |
| 战斗结束/Reset | `CombatEnded` 清空计数器 |

---

## 8. 补丁清单总表

| # | 目标 | 类型 | 作用 | 依据 |
|---|---|---|---|---|
| P1 | `NSettingsScreen._Ready` / `OnSubmenuOpened` | Postfix | 注入共享设置面板 | `Entry.cs:21-37` 范式 |
| P2 | `ActModel.get_AllEncounters` | Postfix（改 `__result`） | CZN 遭遇按固定章节入池（含首领，仅 mutable act） | `ActModel.cs:144-169` |
| P3 | `RunManager.GenerateRooms` | Postfix | 指定模式：原生抽完后强行替换所选章节首领 | `RunManager.cs:743-767` |
| P4 | `MonsterModel.RollMove` | Postfix | 赋予计数器（基数公式 §7.4） | `MonsterModel.cs:416` |
| P5 | `CardModel.OnPlayWrapper` | Postfix（捕获 `__result` Task） | 仅主动出牌递减；等牌结算完再减；归零则打标记并触发中断链 | `CardModel.cs:1858` |
| P6 | `Creature.TakeTurn` | Prefix（可跳过） | 去重：当前意图已排队/正在被中断链执行时跳过，防同一意图双演 | `Creature.cs:711` |

全部补丁挂在 `CznMonster*` 命名空间下的嵌套类里，由入口按命名空间扫描、逐类隔离
（铁律 6）。**不**对 CznCore 副本调用 `PatchAll`。

---

## 9. 工程骨架与文件清单

```
CznMonster/
├── CznMonster.csproj              # 同 CznHeidemarie.csproj：sts2/0Harmony/GodotSharp + ModAnalyzers，net9.0
├── CznMonster.json                # 清单：id=CznMonster，has_dll=true，affects_gameplay=true
├── MainFile.cs                    # [ModInitializer("Init")]：Harmony + ApplyModPatches + 设置注册
├── build.ps1                      # 打包脚本（v1 可只留 dll 链：dotnet build + post-build 复制；有 Spine 资源后再补 pck 链）
├── export_presets.cfg             # 有 pck 时才需要（Godot 4.5.1-stable mono，铁律 8）
├── CznCore/                       # 自 CznHeidemarie/CznCore 拷贝，只保留共享设置面板链路
├── Settings/
│   ├── MonsterSettings.cs         # ConfigFile 存储 + 默认值 + Reset
│   ├── MonsterSettingsText.cs     # ISkinSettingsUiText（SkinSettingsUiTextFactory.Create 默认文案）
│   ├── SettingsLocalization.cs    # res://CznMonster/localization/{0}/settings.json
│   └── SettingsExtensionRegistrar.cs
├── Registration/
│   ├── SettingsInjectPatch.cs     # P1
│   ├── MonsterPoolPatch.cs        # P2
│   ├── BossPlacementPatch.cs      # P3 + UsedBossIds
│   ├── ActionCountPatches.cs      # P4/P5/P6
│   └── ActionCountRuntime.cs      # 计数器状态机
├── Content/
│   ├── Monsters/                  # MonsterModel 子类（首领 + 普通/精英）
│   ├── Encounters/                # EncounterModel 子类
│   └── Powers/                    # CznActionCountPower（v1 即做，见 §7.7）
└── localization/zhs/*.json        # monsters / encounters / powers / settings
```

新增内容（模型类）无需手动注册：`ModelDb.Init()` 扫描本程序集 `AbstractModel` 子类
（依据 `CznHeidemarie/MainFile.cs:29-38`）。

---

## 10. CZN ↔ STS2 能力映射

### 10.1 口径

CZN（卡厄思梦境）的状态词条与 STS2 的 Power 表**不同名、且部分语义边界不同**。本章是唯一裁定来源：

```
怪物/卡牌/遗物文案里的 CZN 词条  →  查 §10.2 得到 STS2 载体  →  直接调用原生 Power
```

三条硬性约定：

1. **只换载体，不造同类**：已有等价原生 Power 时，**禁止**为 CZN 词条新建语义重复的 Power
   （否则驱散、免疫、HoverTip、AI 判定会分叉成两套）。
2. **零侵入**：不继承、不修改原生 Power 的数值与钩子（铁律 6），差异一律用「施加层数」吸收（§10.4）。
3. **文案与机制分离**：玩家可见文案写 CZN 原名（§10.6），底层仍是原生 Power；
   没有对应词的 STS2 独有效果，文案**不得**冒用 CZN 词条。

### 10.2 已裁定映射表

| # | CZN 词条 | CZN 语义 | STS2 对应 | 实现载体 | 备注 |
|---|---|---|---|---|---|
| M1 | **士气** | 攻击伤害按层加算提升 | 力量 | `StrengthPower` | 敌我通用；己方全体用 `CombatState.GetTeammatesOf(Creature)` |
| M2 | **脆弱** | 受到的伤害提升 | **易伤** | `VulnerablePower` | ⚠ 与 STS2 脆弱撞名，见 §10.3 |
| M3 | **弱化** | 造成的伤害降低 | 虚弱 | `WeakPower` | 直接对应 |
| M4 | **损伤** | 获得的格挡降低 | **脆弱** | `FrailPower` | ⚠ 与 CZN 脆弱反着来，见 §10.3 |
| M5 | **无敌** | 抵消接下来 N 次生命流失 | 缓冲 | `BufferPower` | 每触发一次 −1 层（`BufferPower.cs:29-32`） |
| M6 | **成长** | 每回合永久叠加攻击力 | 恶魔形态 | `DemonFormPower` | 每个己方回合开始 +`Amount` 层力量（`DemonFormPower.cs:58-66`） |

六条均为**机制等价、仅换名**，层数 1:1 直接传入，不需要中间换算表。

### 10.3 撞名警示（最容易写错的地方）

同一份 CZN 文案里「脆弱」和「损伤」在 STS2 侧**交换了名字**：

| 读到的 CZN 词条 | 机制 | 必须用的类 | 用错会怎样 |
|---|---|---|---|
| CZN **脆弱** | 受伤增加 | `VulnerablePower`（易伤） | 误用 `FrailPower` → 变成减格挡，威胁方向完全相反 |
| CZN **弱化** | 输出降低 | `WeakPower`（虚弱） | 误用 `VulnerablePower` → 玩家反而更痛 |
| CZN **损伤** | 格挡减少 | `FrailPower`（STS2 脆弱） | 误用 `VulnerablePower` → 变成加伤 debuff |

速记：**CZN 的「脆弱」= STS2 的「易伤」；STS2 的「脆弱」= CZN 的「损伤」。**

### 10.4 数值与叠层口径

- **层数 1:1**，不做比例换算（v1 定论）。若后续核对到 CZN 士气为「每层 +X% 伤害」而非固定值，
  在此处补一张换算表，改动只发生在施加侧，不影响 Power 选择。
- **递减时机**：STS2 的易伤/虚弱/脆弱在**持有者回合结束** −1 层；CZN 多为「持续 N 次/回合」。
  怪物技能给 N 层时按「覆盖 N 个敌方回合」取同值，**不额外补偿**。
- **多段攻击**：力量与易伤在 STS2 按**每段**结算；CZN 同理，无需折算。
- **无敌**：`BufferPower` 只挡生命流失（HP loss），不挡格挡；CZN 无敌若限定伤害类型，
  需自建 Power 并在 §10.7 登记，不得复用 `BufferPower` 硬改。
- **联机**：`BufferPower` 自带多人缩放（`BufferPower.cs:34-37`），不要重复乘玩家数；
  力量/易伤/虚弱/脆弱原生按人数不变，照常施加。

### 10.5 实现范式

```csharp
// CZN「脆弱」（= STS2 易伤）：给玩家上 2 层
await PowerCmd.Apply<VulnerablePower>(
    new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);

// CZN「士气」（= STS2 力量）：己方全体 +1
await PowerCmd.Apply<StrengthPower>(
    new ThrowingPlayerChoiceContext(), CombatState.GetTeammatesOf(Creature), 1m, Creature, null);

// CZN「成长」（= STS2 恶魔形态）：自身每回合涨力量
await PowerCmd.Apply<DemonFormPower>(
    new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);
```

现有实现可对照：`CznMonster/Content/Monsters/PolemicaShellInsect.cs:77`（易伤）、
`CznMonster/Content/Monsters/PolemicaRockShellInsect.cs:102`（虚弱）、
`CznMonster/Content/Monsters/PolemicaWingShellInsect.cs:77`（脆弱）、
`CznMonster/Content/Monsters/PolemicaLegionSoldier.cs:70`（己方全体力量）、
`CznMonster/Content/Monsters/Agnes.cs:530`（自身力量）。

约定：

- 一律走 `PowerCmd.Apply<T>`（命令队列，联机确定性），**不用** `ApplyInternal`；
  直接改写显示层时才用 `SetAmount`（同 §7.7 的例外口径）。
- CZN 原文的「己方全体」= **施法者所在阵营**：怪物施放时取 `CombatState.GetTeammatesOf(Creature)`
  （自身 + 随从），对玩家群体施放时取技能传入的 `targets`。
- 原生 Power 的图标、名称、描述不改；需要 CZN 风味时只在 `localization/<lang>/powers.json`
  里加**本 mod 自有**的键（如未来的复合 Power），不要覆盖原生键。

### 10.6 反向查表（STS2 → CZN）

写怪物技能时若先想到的是 STS2 效果，按此列回推 CZN 词条：

| STS2 | CZN |
|---|---|
| 力量 `StrengthPower` | 士气 |
| 易伤 `VulnerablePower` | 脆弱 |
| 虚弱 `WeakPower` | 弱化 |
| 脆弱 `FrailPower` | 损伤 |
| 缓冲 `BufferPower` | 无敌 |
| 恶魔形态 `DemonFormPower` | 成长 |

表外的 STS2 Power（中毒 `PoisonPower`、荆棘 `ThornsPower`、无实体 `IntangiblePower`、
人工制品 `ArtifactPower`、镀层 `PlatingPower` 等）**CZN 无对应词条**：
实现可以借用原生 Power，但技能文案只能描述效果本身，不能写成 CZN 状态名。

### 10.7 待裁定清单

以下槽位暂未核对到 CZN 原始词条，实现前需先补齐左侧名称，再决定映射类型
（走原生 / 需要自建 Power）。补齐后并入 §10.2，不要长期留在本节：

| 用途 | 预期 STS2 载体 | 状态 |
|---|---|---|
| 格挡类增益（护盾/铁壁） | 一次性 `CreatureCmd.GainBlock`；持续型 `PlatingPower`（回合结束获得格挡并递减，`PlatingPower.cs:61-68`） | 待核对 CZN 词条名与递减规则 |
| 持续伤害（中毒类） | `PoisonPower` | 待核对层数语义（CZN 多为按次触发） |
| 反伤类 | `ThornsPower` | 待核对触发时机（受击前/后） |
| 减伤/免伤类（非次数制） | `IntangiblePower` | 需与「无敌/缓冲」区分口径 |
| 嘲讽/仇恨类 | STS2 侧无对应 Power（`Taunt` 只是卡牌 `src/Core/Models/Cards/Taunt.cs`） | 若 CZN 确有该机制则需自建 Power |
| 每回合抽牌 / 能量类 | `DrawCardsNextTurnPower` / `EnergyNextTurnPower` | 待核对 |

---

## 附录 A：关键源码索引

| 主题 | 位置 |
|---|---|
| 章节/首领生成 | `SlayTheSpire2_source/src/Core/Models/ActModel.cs:331,384,466` |
| Run 房间总生成 | `SlayTheSpire2_source/src/Core/Runs/RunManager.cs:743-767` |
| 遭遇模型 | `SlayTheSpire2_source/src/Core/Models/EncounterModel.cs:51,150,245` |
| 怪物模型 | `SlayTheSpire2_source/src/Core/Models/MonsterModel.cs:56,240,416,435` |
| 意图类型 | `SlayTheSpire2_source/src/Core/MonsterMoves/Intents/IntentType.cs:3` |
| 进阶判定 | `SlayTheSpire2_source/src/Core/Entities/Ascension/AscensionLevel.cs`、`AscensionManager.cs`、`src/Core/Helpers/AscensionHelper.cs` |
| 血量真源 | `SlayTheSpire2_source/src/Core/Models/MonsterModel.cs:56-58`（`MinInitialHp` / `MaxInitialHp`） |
| 血量生成流水线 | `SlayTheSpire2_source/src/Core/Combat/CombatState.cs:233-247`、`src/Core/Entities/Creatures/Creature.cs:372-393`（摇值 + 去重 + 联机缩放） |
| 联机血量公式 | `SlayTheSpire2_source/src/Core/Entities/Creatures/Creature.cs:749-756`、`src/Core/Models/Singleton/MultiplayerScalingModel.cs:70-87`（幕系数） |
| 原生 Boss 血量数值 | `src/Core/Models/Monsters/{Vantom,CeremonialBeast,KinPriest,KinFollower,WaterfallGiant,SoulFysh,LagavulinMatriarch,TheInsatiable,KnowledgeDemon,Crusher,Rocket,Queen,TorchHeadAmalgam,TestSubject,Aeonglass}.cs`（汇总表见 §3.3.5） |
| 随从标记 | `SlayTheSpire2_source/src/Core/Entities/Creatures/Creature.cs:269`（`IsSecondaryEnemy`）、`src/Core/Models/Powers/MinionPower.cs:13` |
| 多人缩放 | `SlayTheSpire2_source/src/Core/Models/Singleton/MultiplayerScalingModel.cs:70` |
| 敌人回合 | `SlayTheSpire2_source/src/Core/Combat/CombatManager.cs:1402-1435` |
| 出牌流水线 | `SlayTheSpire2_source/src/Core/Models/CardModel.cs:1858-2019` |
| 计数参考（Slow） | `SlayTheSpire2_source/src/Core/Models/Powers/SlowPower.cs:32` |
| CznCore 共享设置 | `CznHeidemarie/CznCore/Settings/SkinSettingsUi.cs:292,338` |
| CznCore 扩展协议 | `CznHeidemarie/CznCore/Settings/SharedSettingsExtensionRegistry.cs` |
| 参考 mod 入口范式 | `CznTipheraCharacter/decompiled/CznTipheraCharacter/Entry.cs:21-130` |
| 本工作 mod 入口范式 | `CznHeidemarie/MainFile.cs`、`CznHeidemarie/Integration/HeidemarieIntegration.cs` |
| CZN 能力映射载体（§10） | `SlayTheSpire2_source/src/Core/Models/Powers/StrengthPower.cs`、`VulnerablePower.cs`、`WeakPower.cs`、`FrailPower.cs`、`BufferPower.cs`、`DemonFormPower.cs` |
| CZN 能力施加范式 | `CznMonster/Content/Monsters/PolemicaShellInsect.cs:77`、`PolemicaLegionSoldier.cs:70`、`Agnes.cs:530` |
