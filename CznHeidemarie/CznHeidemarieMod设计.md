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
> `卡面映射` 列只写文件名，统一位于`images\packed\card_portraits\heidemarie\` 下，
> 文件名为 `<卡名>.png`（如 `剑光` → `Swordlight.png`）。
> 打包后运行时路径为 `res://images/packed/card_portraits/heidemarie/...`，
> 由卡牌基类 `HeidemarieCard.PortraitPath` 拼出；文件缺失时自动回退占位图。
>
> **稀有度取值**：`Basic` 只能来自初始卡组（奖励 / 商店 / 战斗随机生成都不出）；
> `Common` 可正常获取；`Token` 为衍生牌（不参与奖励 / 商店抽取）。

| 名称     | CardRarity | CardType | cost | 效果                                                                  | 升级     | 特殊说明                             | 代码映射     |       卡面映射       |
| ------ | ---------- | -------- | ---- | ------------------------------------------------------------------- | :----- | :------------------------------- | -------- | :--------------: |
| 剑光     | Basic      | Attack   | 1    | 造成8点伤害。感应：下一张攻击牌伤害+50%                                              | 伤害+2   | 后续加入初始卡组                         | `HeidemarieSwordlight.cs` | `Swordlight.png` |
| 剑雨     | Common     | Attack   | 1    | 链接。造成4点伤害2次。感应：生成1张极光剑到抽牌堆                                          | 生成+1   | <br />                           | `HeidemarieSwordrain.cs` | `Swordrain.png` |
| 极光剑    | Token      | Attack   | 1    | 链接。对随机敌人造成3点伤害。守墓。                                                   | 不可升级 | 非指向（`RandomEnemy`）。衍生牌，不参与奖励/商店，且禁战斗内随机生成       | `HeidemarieAuroraSword.cs` | `AuroraSword.png` |
| 剑冢     | Common     | Skill    | 2    | 打出墓地中最多5张极光剑                                                             | cost-1 | 非指向（`TargetType.None`）；每张剑各自随机命中一名敌人     | `HeidemarieSwordTomb.cs` | `SwordTomb.png` |
| 凝锋     | Common     | Skill    | 0    | 生成2张极光剑加入手牌，赋予其回收                                                   | 生成+1   | 赋予的「回收」默认 1 层                    | `HeidemarieCondenseEdge.cs` | `CondenseEdge.png` |
| 安魂剑    | Common     | Attack   | 2    | 链接。安息。造成6点伤害2次。感应：生成2张极光剑到手牌                                        | cost-1 | 安息结算目标取随机敌人                      | `HeidemarieRequiemBlade.cs` | `RequiemBlade.png` |
| 绝响     | Rare       | Attack   | 3    | 消耗。终结。保留。唯一。对全体敌人造成20点伤害。若手牌中存在解放极光，则将其消耗，此时墓地中每有1张极光剑，即可使伤害量+100%。 | 伤害+10  | <br />                           | `HeidemarieFinalEcho.cs` | `FinalEcho.png` |
| 凝聚极光   | Rare       | Skill    | 🚫   | 无法使用。唯一。链接。回收3。移动至墓地时，极光之光1                                         | 无法升级 | 无法升级；不参与奖励/商店                    | `HeidemarieCondensedAurora.cs` | `CondensedAurora.png` |
| 解放极光   | Rare       | Attack   | 0    | 唯一。虚无。造成10点伤害。消耗墓地中的所有极光剑，每消耗1张，即可使基础伤害量+100%。焚烧：手牌中加入一张凝聚极光        | 无法升级 | 无法升级。挂载ug全屏演出。不能通过除能力极光之光外任何渠道获得 | `HeidemarieLiberatedAurora.cs` | `LiberatedAurora.png` |

> **初始卡组（当前）**：打击 ×4 + 防御 ×4（共 8 张）。
> 测试期的 8 张「专属牌」及其余占位牌已移除；「剑光」后续会加入初始卡组。

## 关键字设计表

| 名称 | 效果                                 | 实现要点                                           |
| -- | ---------------------------------- | ---------------------------------------------- |
| 感应 | 抽取该卡牌后，触发感应效果                      | `ISenseCard` + `AfterCardDrawn`                |
| 链接 | 直接使用此卡时，丢弃手牌中其他所有的连接卡牌             | `ILinkCard` 标记；出牌时 `DiscardOtherLinked`       |
| 安息 | 非回合结束丢弃此卡时，此卡效果发动。本回合内此卡不能被移出已使用牌堆 | `IRestCard` + `AfterCardDiscarded`；本回合锁定墓地 |
| 守墓 | 洗牌时，此卡不会移动至抽牌堆                     | `IGraveKeepCard` + `ModifyShuffleOrder`        |
| 回收 | 此卡牌移入墓地时返回手牌 效果发动时回收-1             | 层数默认 1；1 层显示「回收」，>1 显示「回收N」                   |
| 终结 | 使用此卡牌后立即结束本回合                      | `ITerminateCard`；`OnPlay` 末尾 `PlayerCmd.EndTurn` |
| 唯一 | 仅可持有1张拥有唯一的相同卡牌                    | `IUniqueCard`；按当前卡组去重，不可复制                    |
| 焚烧 | 本牌被消耗时触发。                          | `IBurnCard` + `AfterCardExhausted`             |

## 能力设计表

| 名称   | 类型 | 效果                                    |
| ---- | -- | ------------------------------------- |
| 极光之光 | 能力 | 当层数达到3层时，将解放极光加入手牌。同时移除卡牌凝聚极光和所有极光之光。 |

---

## 实现说明（2026-10-13 回写）

### 伤害公式

```
最终伤害 = (原基础伤害 + 固定伤害增加) × (1 + Σ百分比增伤)
```

- 固定伤害增加（如升级 `+2` / `+10`）计入**基础伤害**（`DynamicVars.Damage`）。
- 所有百分比增伤（剑光 +50%、绝响/解放极光 每剑 +100%）**相加**后作为**单一乘算因子**。
- 引擎的乘算区是**连乘**（`Hook.ModifyDamageInternal`：`num *= num3`），
  因此百分比不能各做一个 modifier，否则会变成 `1.5 × (1+N)`。
  本工程用隐藏能力 `HeidemarieDamagePercentPower`（「百分比增伤池」）汇聚，
  由其单独提供 `1 + Amount/100`，一次攻击结算后整池清空。

### 关键语言以外的约定

- **目标类型（`TargetType`）**：`极光剑` 为 `RandomEnemy`（出牌不选目标，伤害由
  `AttackCommand.TargetingRandomOpponents(CombatState!)` 随机分配，可重复命中同一敌人）；
  `剑冢` 随之改为 `TargetType.None`（非指向技能），不再向 `CardCmd.AutoPlay` 传目标 ——
  两张牌的随机目标由极光剑自身承担，`剑冢` 只负责「重放墓地里的剑」。
  其余攻击牌维持 `AnyEnemy`（`剑光` / `剑雨` / `安魂剑` / `解放极光`）。
- 词条显示（卡面金色词条 + 悬停提示）走 `CznCore.Cards.CardMechanicPresentation`，
  关键字定义在 `Characters/Heidemarie/Cards/HeidemarieKeywords.cs`。
- 「唯一」已完整拦截（`Registration/HeidemarieUniquePatch.cs`）：判定范围 = **当前卡组**；
  覆盖 **奖励 / 商店 / 事件 / 复制 / 变形 / 战斗内随机生成** 六个入口，
  并拦截 **重放（赋魔 · 结算 · 消耗）** —— 唯一牌只结算 1 次，且**不消耗**那次「额外打出」效果，
  原样留给下一张牌（详见 `卡牌关键字与能力实现清单.md` §九）。
- 「安息」的本回合墓地锁定用 `HeidemarieCard.RestLockedThisTurn`，回合开始清除。
- 演出映射：`绝响 → ux 全屏演出`、`解放极光 → ug 全屏演出`（原绑定在已移除的测试卡上）。
- 卡面：**暂不做专属卡面**，一律回退到战士的占位卡面
  （攻击牌 → `strike_ironclad`，技能牌 → `defend_ironclad`），
  由 `HeidemarieCard.PortraitPath` 在 `res://images/atlases/card_atlas.sprites/heidemarie/<卡牌ID>.tres`
  不存在时自动回退。日后只要把该文件放进对应目录即可自动启用，无需改代码。
