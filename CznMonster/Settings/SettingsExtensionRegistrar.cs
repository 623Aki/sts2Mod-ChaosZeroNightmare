using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CznCore.Settings;
using CznMonster.Content;

namespace CznMonster.Settings;

/// <summary>
/// 共享设置面板扩展注册（设计文档 §4.2 / §4.3 表 4-1）：
/// 通过 AppDomain 数据键把本 mod 的设置控件描述交给 CznCore 框架渲染。
/// </summary>
internal static class SettingsExtensionRegistrar
{
	/// <summary>在 <see cref="MainFile.Initialize"/> 中调用一次；设置界面打开时框架才读取。</summary>
	public static void Register()
	{
		ConcurrentDictionary<string, object> providers = AppDomain.CurrentDomain.GetData(SharedSettingsExtensionRegistry.RegistryDataKey)
			as ConcurrentDictionary<string, object> ?? new ConcurrentDictionary<string, object>();

		providers[MainFile.ModId] = new Dictionary<string, object>
		{
			["api_version"] = 1,
			// 与角色 mod 的 visual/combat 分开，避免撞车（D11）
			["category_id"] = "czn_monster",
			["category_label"] = (Func<string>)(() => SettingsLocalization.Text("czn_monster_category", "CznMonster").Current),
			["category_order"] = 10,
			["group_label"] = (Func<string>)(() => SettingsLocalization.Text("czn_monster_group", "Custom Monsters & Action Count").Current),
			["group_order"] = 0,
			["reset"] = (Action)(() => MonsterSettings.ResetToDefaults()),
			["controls"] = new object[]
			{
				// 1. 行动计数总开关
				new Dictionary<string, object>
				{
					["type"] = "toggle",
					["id"] = "action_count_enabled",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("action_count_enabled", "Enable Action Count").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("action_count_enabled_desc", "Counted monsters act after you play a number of cards, even mid-turn.").Current),
					["get"] = (Func<bool>)(() => MonsterSettings.ActionCountEnabled),
					["set"] = (Action<bool>)(v => MonsterSettings.ActionCountEnabled = v),
					["order"] = 0
				},
				// 2. 行动计数范围（父关子禁）
				new Dictionary<string, object>
				{
					["type"] = "choice",
					["id"] = "action_count_scope",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("action_count_scope", "Action Count Scope").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("action_count_scope_desc", "Which monsters get action counters.").Current),
					["get"] = (Func<int>)(() => MonsterSettings.ActionCountScope),
					["set"] = (Action<int>)(v => MonsterSettings.ActionCountScope = v),
					["order"] = 1,
					["parent_id"] = "action_count_enabled",
					["enabled"] = (Func<bool>)(() => MonsterSettings.ActionCountEnabled),
					["options"] = new object[]
					{
						new Dictionary<string, object>
						{
							["id"] = 1,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("scope_bosses", "All Bosses").Current)
						},
						new Dictionary<string, object>
						{
							["id"] = 2,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("scope_bosses_elites", "All Bosses + Elites").Current)
						},
						new Dictionary<string, object>
						{
							["id"] = 3,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("scope_all", "All Monsters").Current)
						}
					}
				},
				// 3. CZN 怪物总开关（默认关）
				new Dictionary<string, object>
				{
					["type"] = "toggle",
					["id"] = "czn_monsters_enabled",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("czn_monsters_enabled", "Enable CZN Monsters").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("czn_monsters_enabled_desc", "Adds custom CZN monsters to the run. Off keeps the original game content.").Current),
					["get"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled),
					["set"] = (Action<bool>)(v => MonsterSettings.CznMonstersEnabled = v),
					["order"] = 2
				},
				// 4. 首领位置：指定章节（原生抽完之后强行替换；默认关 = 按固定章节入池随机）
				new Dictionary<string, object>
				{
					["type"] = "toggle",
					["id"] = "boss_pos_specified",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_pos_specified", "Boss Spawn: Specified Act").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("boss_pos_specified_desc", "After the native boss roll, override the chosen act with the selected CZN boss.").Current),
					["get"] = (Func<bool>)(() => MonsterSettings.BossPosSpecified),
					["set"] = (Action<bool>)(v => MonsterSettings.BossPosSpecified = v),
					["order"] = 3,
					["parent_id"] = "czn_monsters_enabled",
					["enabled"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled)
				},
				// 5. 指定章节（仅"指定"勾选时可用）
				new Dictionary<string, object>
				{
					["type"] = "choice",
					["id"] = "boss_chapter",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_chapter", "Specified Act").Current),
					["get"] = (Func<int>)(() => MonsterSettings.BossChapter),
					["set"] = (Action<int>)(v => MonsterSettings.BossChapter = v),
					["order"] = 4,
					["parent_id"] = "boss_pos_specified",
					["enabled"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled && MonsterSettings.BossPosSpecified),
					["options"] = new object[]
					{
						new Dictionary<string, object>
						{
							["id"] = 1,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("act_1", "Act 1").Current)
						},
						new Dictionary<string, object>
						{
							["id"] = 2,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("act_2", "Act 2").Current)
						},
						new Dictionary<string, object>
						{
							["id"] = 3,
							["label"] = (Func<string>)(() => SettingsLocalization.Text("act_3", "Act 3").Current)
						}
					}
				},
				// 6. 指定首领（仅"指定"勾选时可用）：0 = 随机，1..N = BossCatalog 顺序
				new Dictionary<string, object>
				{
					["type"] = "choice",
					["id"] = "boss_select",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_select", "Specified Boss").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("boss_select_desc", "Which CZN boss to place in the chosen act. Random picks one that has not appeared this run.").Current),
					["get"] = (Func<int>)(() => MonsterSettings.BossSelect),
					["set"] = (Action<int>)(v => MonsterSettings.BossSelect = v),
					["order"] = 5,
					["parent_id"] = "boss_pos_specified",
					["enabled"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled && MonsterSettings.BossPosSpecified),
					["options"] = BuildBossSelectOptions()
				},
				// 7. 首领血量：自定义开关（仅测试用）
				new Dictionary<string, object>
				{
					["type"] = "toggle",
					["id"] = "boss_hp_custom",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_hp_custom", "Boss HP: Custom").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("boss_hp_custom_desc", "For testing only. Overrides the boss HP with the value below.").Current),
					["get"] = (Func<bool>)(() => MonsterSettings.BossHpCustom),
					["set"] = (Action<bool>)(v => MonsterSettings.BossHpCustom = v),
					["order"] = 6,
					["parent_id"] = "czn_monsters_enabled",
					["enabled"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled)
				},
				// 8. 自定义血量。
				//    共享设置页由角色 mod 内置的旧版 CznCore 渲染，旧版只支持
				//    toggle / slider / choice，不支持 text；用 text 时勾选开关会触发
				//    旧版重建，整行被静默丢弃。这里改用 slider（1–9999，步长 1），
				//    勾选/取消勾选后行都不会消失。
				new Dictionary<string, object>
				{
					["type"] = "slider",
					["id"] = "boss_hp_value",
					["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_hp_value", "Custom HP").Current),
					["description"] = (Func<string>)(() => SettingsLocalization.Text("boss_hp_value_desc", "1-9999. Only used when Custom Boss HP is on.").Current),
					["get"] = (Func<double>)(() => MonsterSettings.BossHpValue),
					["set"] = (Action<double>)(v => MonsterSettings.BossHpValue = (int)v),
					["min"] = 1.0,
					["max"] = 9999.0,
					["step"] = 1.0,
					["suffix"] = "",
					["display_offset"] = 0.0,
					["order"] = 7,
					["parent_id"] = "boss_hp_custom",
					["enabled"] = (Func<bool>)(() => MonsterSettings.CznMonstersEnabled && MonsterSettings.BossHpCustom)
				}
			}
		};

		AppDomain.CurrentDomain.SetData(SharedSettingsExtensionRegistry.RegistryDataKey, providers);
	}

	/// <summary>
	/// 「指定首领」下拉框选项：第一项固定为 0=随机，其余按 <see cref="MonsterContent.BossCatalog"/>
	/// 顺序编号 1..N。名称在渲染时才解析（<see cref="MonsterContent.BossEntry.DisplayName"/>），
	/// 避免注册阶段提前触发 <c>ModelDb</c>。
	/// </summary>
	private static object[] BuildBossSelectOptions()
	{
		List<object> options = new List<object>
		{
			new Dictionary<string, object>
			{
				["id"] = 0,
				["label"] = (Func<string>)(() => SettingsLocalization.Text("boss_select_random", "Random (Default)").Current)
			}
		};

		IReadOnlyList<MonsterContent.BossEntry> catalog = MonsterContent.BossCatalog;
		for (int i = 0; i < catalog.Count; i++)
		{
			MonsterContent.BossEntry entry = catalog[i];
			options.Add(new Dictionary<string, object>
			{
				["id"] = i + 1,
				// 首领名走游戏 encounters 表的 title，随语言自动切换
				["label"] = (Func<string>)(() => entry.DisplayName)
			});
		}

		return options.ToArray();
	}
}
