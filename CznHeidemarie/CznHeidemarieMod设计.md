# CznHeidemarie 增加 mod 设计

## 角色设定

| 项目   | 内容      |
| ---- | ------- |
| 名称   | 海德玛丽    |
| 种族   | 人类      |
| 生日   | 2月4日    |
| 异能   | 凝铸极光    |
| 所属势力 | 希安联邦乐园号 |

### 背景

园号的市长，大众心中的英雄。多年前的一场战斗让她被乐园号的市民铭记，却也在她的心底留下了不为人知的隐痛。自一线退下之后，她一如往昔般守护着乐园号的市民。只是那明朗的笑容之下，藏着未曾愈合的旧伤。

## 卡牌设计表

> **公共说明**：
> `代码映射` 列只写文件名，统一位于
> `Characters\Heidemarie\Cards\` 下（工程根 `CznHeidemarie\`）。
> 例：`HeidemarieStrike.cs` → `CznHeidemarie\Characters\Heidemarie\Cards\HeidemarieStrike.cs`；
> 本地化 key 为 `res://CznHeidemarie/localization/zhs/cards.json` 中的 `HEIDEMARIE_<卡名大写>`。
>
> `卡面映射` 列只写文件名，统一位于 `images\packed\card_portraits\heidemarie\` 下，
> 文件名为 `heidemarie_<slug>.png`，slug 由 C# 类名经 `StringHelper.Slugify`
> 派生（如 `HeidemarieSwordlight` → `heidemarie_swordlight`）。
> 打包后运行时路径为 `res://images/packed/card_portraits/heidemarie/heidemarie_<slug>.png`，
> 由卡牌基类 `HeidemarieCard.PortraitPath` 拼出；文件缺失时自动回退战士占位图。
>
> **稀有度取值**：`Basic` 只能来自初始卡组（奖励 / 商店 / 战斗随机生成都不出）；
> `Common` 可正常获取；`Token` 为衍生牌（不参与奖励 / 商店抽取）。
>
> **效果说明**：文本中描述为`造成{Damage:diff()}点伤害`即代表为指向性伤害牌，
> `对随机敌人造成{Damage:diff()}点伤害`则代表为随机性伤害牌。
> `对全体敌人造成{Damage:diff()}点伤害`则代表为范围性伤害牌。

| 名称    | CardRarity | CardType | cost | 效果                                                                         | 升级                 | 特殊说明                                                                                                                | 代码映射                           |                卡面映射               |
| ----- | ---------- | -------- | ---- | -------------------------------------------------------------------------- | :----------------- | :------------------------------------------------------------------------------------------------------------------ | ------------------------------ | :-------------------------------: |
| 剑光    | Common     | Attack   | 1    | 造成8点伤害。感应：下一张极光剑的伤害+50%                                                    | 伤害+2               | 感应触发时，获得能力`白昼` 1 层。                                                                                                 | `HeidemarieSwordlight.cs`      |    `heidemarie_swordlight.png`    |
| 剑雨    | Common     | Attack   | 1    | 连接。造成4点伤害2次。感应：在抽牌堆生成1张极光剑                                                 | 生成+1               | <br />                                                                                                              | `HeidemarieSwordrain.cs`       |     `heidemarie_swordrain.png`    |
| 极光剑   | Token      | Attack   | 1    | 连接。对敌人造成3点伤害。被丢弃时，对随机敌人造成3点伤害。守墓。                                          | 不可升级               | 主动打出为指向伤害（`AnyEnemy`）；「被丢弃时」为随机目标、自行实现（不用`安息`），并**视为一次极光剑结算**（吃增伤 + 消耗`白昼`）。衍生牌：不参与奖励/商店、禁战斗内随机生成，但登记在衍生牌池供控制台/图鉴查看 | `HeidemarieAuroraSword.cs`     |   `heidemarie_aurora_sword.png`   |
| 剑冢    | Uncommon   | Skill    | 2    | 重放墓地中最多5张极光剑                                                               | cost-1             | 非指向（`TargetType.None`）；每张剑各自随机命中一名敌人；**重放不算「直接使用」，不触发「连接」的连带丢弃**                                                    | `HeidemarieSwordTomb.cs`       |    `heidemarie_sword_tomb.png`    |
| 凝锋    | Common     | Skill    | 0    | 生成2张极光剑加入手牌，赋予其回收                                                          | 生成+1               | 赋予的「回收」默认 1 层                                                                                                       | `HeidemarieCondenseEdge.cs`    |   `heidemarie_condense_edge.png`  |
| 安魂剑   | Uncommon     | Attack   | 2    | 连接。安息。随机对敌人造成6点伤害2次。感应：在手牌生成2张极光剑                                          | cost-1             | <br />                                                                                                              | `HeidemarieRequiemBlade.cs`    |   `heidemarie_requiem_blade.png`  |
| 绝响    | Rare       | Attack   | 3    | 消耗。终结。保留。唯一。对全体敌人造成20点伤害。若手牌中存在解放极光，则将其消耗，此时墓地中每有1张极光剑，即可使伤害量+100%。        | 伤害+10              | <br />                                                                                                              | `HeidemarieFinalEcho.cs`       |    `heidemarie_final_echo.png`    |
| 凝聚极光  | Rare       | Skill    | 🚫   | 无法使用。唯一。连接。回收3。移动至墓地时，极光之光1                                                | 赋予固有               | 初始卡。不参与奖励/商店；仅由解放极光的焚烧生成，且生成时即为升级版（固有）                                                                              | `HeidemarieCondensedAurora.cs` | `heidemarie_condensed_aurora.png` |
| 解放极光  | Token      | Attack   | 0    | 消耗。虚无。唯一。造成10点伤害。消耗墓地中的所有极光剑，每消耗1张，即可使基础伤害量+100%。焚烧：在手牌生成1张凝聚极光（升级版），并移除此牌 | 无法升级               | 衍生牌（不参与奖励/商店）。挂载ug全屏演出。不能通过除能力极光之光外任何渠道获得                                                                           | `HeidemarieLiberatedAurora.cs` | `heidemarie_liberated_aurora.png` |
| 万众英雄  | Common     | Skill    | 1    | 安息。抽取3。本回合内赋予它们连接                                                          | 赋予连接               | <br />                                                                                                              | `HeidemarieAllHeroes.cs`       |    `heidemarie_all_heroes.png`    |
| 缀星    | Uncommon   | Skill    | 0    | 选择手牌中的1张牌赋予其连接                                                             | 数量+1               | <br />                                                                                                              | `HeidemarieBindStar.cs`        |     `heidemarie_bind_star.png`    |
| 微光    | Rare       | Skill    | 1    | 本回合内赋予手牌中费用小于等于1的卡牌连接                                                      | cost-1             | <br />                                                                                                              | `HeidemarieFaintLight.cs`      |    `heidemarie_faint_light.png`   |
| 冥渡    | Uncommon     | Skill    | 0    | 本回合内赋予手牌中安息卡牌连接                                                            | 赋予保留               | <br />                                                                                                              | `HeidemarieRequiem.cs`         |      `heidemarie_requiem.png`     |
| 撷光    | Rare       | Skill    | 0    | 消耗。从抽牌堆或弃牌堆中选择1张卡牌，赋予其连接并将其移动至手牌                                           | 数量+1               | <br />                                                                                                              | `HeidemarieRecover.cs`         |      `heidemarie_recover.png`     |
| 引锋召刃  | Rare       | Skill    | 1    | 保留。抽取两张攻击卡牌。本回合内赋予手牌中攻击卡牌连接                                                | cost-1             | <br />                                                                                                              | `HeidemarieCallBlade.cs`       |    `heidemarie_call_blade.png`    |
| 一缕光芒  | Uncommon     | Attack   | 1    | 连接。造成8点伤害。打出前手牌中每有1张连接卡牌，即可使伤害+50%                                         | 伤害提升+50%           | <br />                                                                                                              | `HeidemarieRayOfLight.cs`      |   `heidemarie_ray_of_light.png`   |
| 叠光奏浪  | Common     | Attack   | 2    | 连接。造成14点伤害。打出前手牌中连接卡牌数量大于等于3时，伤害变为3倍                                       | 伤害+8               | <br />                                                                                                              | `HeidemarieTripleGlow.cs`      |    `heidemarie_triple_glow.png`   |
| 万剑齐鸣  | Rare       | Skill    | 3    | 消耗。打出手牌中的所有连接卡牌                                                            | cost-1             | <br />                                                                                                              | `HeidemarieResonance.cs`       |     `heidemarie_resonance.png`    |
| 陨锋·坠月 | Rare       | Attack   | 2    | 连接。主动打出时，对敌人造成15点伤害2次。被丢弃时，对全体敌人造成20点伤害                                    | 打出基础伤害+8；丢弃基础伤害+10 | 「被丢弃时」自行实现（不用`安息`：不被锁墓、也不吃「冥渡」的安息判定）                                                                                | `HeidemarieSkyfall.cs`         |      `heidemarie_skyfall.png`     |
| 展开极光  | Uncommon     | Skill    | 2    | 安息。在手牌生成2张极光剑。本回合极光剑的伤害+50%                                                | 生成+1               | 打出时获得能力`极夜`                                                                                                         | `HeidemarieUnfoldAurora.cs`    |   `heidemarie_unfold_aurora.png`  |
| 北极圈   | Rare       | Power    | 2    | 回合开始时，在手牌生成2张极光剑（可叠加：N层生成2N张）                                              | 赋予此卡牌关键字`安息`       | 打出时获得能力`北极圈`                                                                                                        | `HeidemarieNorthPolar.cs`      |    `heidemarie_north_polar.png`   |
| 归墟剑骸  | Common     | Skill    | 2    | 连接。安息。在弃牌堆生成4张极光剑                                                          | 生成+3               | <br />                                                                                                              | `HeidemarieSwordCrypt.cs`      |    `heidemarie_sword_crypt.png`   |
| 万剑诀   | Uncommon     | Skill    | 2    | 连接。打出前手牌中每有1张连接，即可在手牌生成1张极光剑                                               | cost-1             | <br />                                                                                                              | `HeidemarieMirrorBlades.cs`    |   `heidemarie_mirror_blades.png`  |
| 积羽    | Rare       | Skill    | 2    | 安息。在弃牌堆生成3张极光剑。感应：下一张极光剑的伤害+100%                                           | 生成+2               | <br />                                                                                                              | `HeidemarieStoreLight.cs`      |    `heidemarie_store_light.png`   |
| 坠星    | Rare       | Skill    | 1    | 消耗。将手牌和弃牌堆的卡牌全部放入抽牌堆中洗牌。抽5张牌                                               | cost-1             | 包括含有`守墓`关键字的卡牌                                                                                                      | `HeidemarieFallenStar.cs`      |    `heidemarie_fallen_star.png`   |
| 窥星择光  | Common     | Skill    | 1    | 查看抽牌堆顶3张卡牌，选择其中1张加入手牌，其他的丢弃                                                | 查看数量+1             | 被丢弃的卡牌能够触发`安息`                                                                                                      | `HeidemarieForesee.cs`         |      `heidemarie_foresee.png`     |

> **初始卡组（当前）**：打击 ×3 + 防御 ×3 + 剑光 + 剑雨 + 凝聚极光 （共 9 张）。

## 关键字设计表

| 名称 | 效果                                 | 实现要点                                                                                                                                                                                        |
| -- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 感应 | 抽取该卡牌后，触发感应效果                      | `ISenseCard` + `AfterCardChangedPiles`（抽牌堆→手牌）                                                                                                                                              |
| 连接 | 直接使用此卡时，丢弃手牌中其他所有的连接卡牌             | `ILinkCard` 标记 + 运行期临时标记；**统一**由 `HeidemarieCard.BeforeCardPlayed` 派发 `DiscardOtherLinked`（原生/临时连接的牌走同一条路）。**重放与自动打出不算「直接使用」**（剑冢、万剑齐鸣），期间由 `SuppressLinkDiscard` 抑制。**计数口径**：卡面写了「打出前」才含自身 |
| 安息 | 非回合结束丢弃此卡时，此卡效果发动。本回合内此卡不能被移出已使用牌堆 | `IRestCard` + `AfterCardDiscarded`；本回合锁定墓地                                                                                                                                                  |
| 守墓 | 洗牌时，此卡不会移动至抽牌堆                     | `IGraveKeepCard` + `ModifyShuffleOrder`                                                                                                                                                     |
| 回收 | 此卡牌移入墓地时返回手牌 效果发动时回收-1             | 层数默认 1；1 层显示「回收」，>1 显示「回收N」                                                                                                                                                                 |
| 终结 | 使用此卡牌后立即结束本回合                      | `ITerminateCard`；`OnPlay` 末尾 `PlayerCmd.EndTurn`                                                                                                                                            |
| 唯一 | 仅可持有1张拥有唯一的相同卡牌                    | `IUniqueCard`；按当前卡组去重，不可复制                                                                                                                                                                  |
| 焚烧 | 本牌被消耗时触发。                          | `IBurnCard` + `AfterCardExhausted`                                                                                                                                                          |

## 能力设计表

| 名称   | 效果                                      | 说明                                                                |
| ---- | --------------------------------------- | ----------------------------------------------------------------- |
| 极光之光 | 当层数达到3层时，在手牌生成1张解放极光。同时移除卡牌凝聚极光和所有极光之光。 | 计数类型，最多三层                                                         |
| 白昼   | 使下一张极光剑的伤害提升。                           | 和极夜百分比加算。重复触发提升的百分比相加，打出极光剑后清除；极光剑「被丢弃时」的伤害同样视为一次极光剑结算（吃增伤并消耗本能力） |
| 极夜   | 本回合内极光剑的伤害提升。                           | 和白昼百分比加算。重复触发提升的百分比相加，回合结束清除                                      |
| 北极圈  | 回合开始时，在手牌生成2张极光剑（可叠加：N层生成2N张）           | <br />                                                            |

***

## 实现说明（2026-10-13 回写）

### 伤害公式

```
最终伤害 = (原基础伤害 + 固定伤害增加) × (1 + Σ百分比增伤)
```

***

## 实现说明（2026-10-14 回写：连接派发 / 衍生牌登记）

### 「连接」的统一派发（本次修订核心）

**派发点**：`HeidemarieCard.BeforeCardPlayed`（出牌钩子，在 `OnPlay` 之前、卡已离开手牌之后）。
任何 `IsLinked` 的牌被「直接使用」时，在这里丢弃手牌中其他所有连接牌。
**为什么必须在这里**：

### 衍生牌登记

新增 `HeidemarieTokenPool`（极光剑 / 凝聚极光 / 解放极光），由
`Registration/HeidemarieCompendiumPatch.cs` 追加进 `ModelDb.AllSharedCardPools`，
三张牌因此进入 `ModelDb.AllCardPools` / `ModelDb.AllCards`：
控制台 `card HEIDEMARIE_CONDENSED_AURORA` 可用（不再 not found）；
图鉴里被视作已解锁，不会显示成未知卡；
依然不参与任何角色池抽取（奖励 / 商店 / 变形不受影响）。
同一补丁兜底 Postfix 了 `ModelDb.get_AllCards`，与「首次访问即缓存」的时机无关。
卡牌图鉴补一个本角色牌池筛选按钮：实例化官方
`res://scenes/screens/card_library/library_pool_toggle.tscn` 挂进同一个 `PoolFilters` 容器，
再把 `NCardLibrary` 的私有 `_poolFilters` / `_cardPoolFilters` 补上
（顺带修掉「以本角色开局打开图鉴」时的 `KeyNotFoundException`），不改动任何游戏资源。
