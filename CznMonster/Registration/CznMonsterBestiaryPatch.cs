using System;
using System.Collections.Generic;
using System.Reflection;
using CznMonster.Content.Encounters;
using CznMonster.Settings;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Rooms;

namespace CznMonster.Registration;

/// <summary>
/// 图鉴「CZN 怪物」专属区块（设计文档 §6.5）。
///
/// <para>原生图鉴按「章节 → 遭遇 → AllPossibleMonsters」收集条目，而本 mod 首领只走
/// 首领替换（<see cref="BossPlacementPatch"/>），从不进章节遭遇池，
/// 因此 6 只 CZN 怪在原生流程下永远收集不到 —— 表现为图鉴里根本没有它们。</para>
///
/// <para>做法：在图谱追加「事件遭遇」区块（<c>NBestiary.AddEvents</c>）的位置，
/// 先插入一个本 mod 的区块标题（分隔条复用 <see cref="NBestiaryLabelDivider"/>），
/// 再通过原生私有 <c>AddEntries</c> 添加条目 —— 排序、锁定态、点击信号连接全部
/// 复用原实现，保持行为一致。条目直接用 <see cref="BestiaryEntry.FromMonster"/>，
/// 不经过 <c>ShouldShowInCompendium</c> 过滤（召唤物保持该属性 false，参照原生
/// DecimillipedeSegment 的口径，只是不进章节列表）。</para>
///
/// <para>开关：只在设置里「启用 CZN 怪物」时展示区块。不改游戏的任何资源（零侵入）。</para>
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(NBestiary), "AddEvents")]
internal static class CznMonsterBestiaryPatch
{
	/// <summary>区块标题的本地化键：mod 的 localization/{lang}/bestiary.json 提供。</summary>
	private const string SectionLocKey = "CZN_MONSTERS.title";

	private const string AddEntriesMethodName = "AddEntries";

	private const string BestiaryListFieldName = "_bestiaryList";

	/// <summary>原生私有 AddEntries(List&lt;BestiaryEntry&gt;)，Invoke 前解析一次。</summary>
	private static readonly MethodInfo? _addEntries = AccessTools.Method(typeof(NBestiary), AddEntriesMethodName);

	// 原生流程反复调用 AddEvent 时本补丁每次都会执行，先记一次失败避免刷屏
	private static bool _missingMemberWarned;

	private static void Prefix()
	{
		try
		{
			if (!MonsterSettings.CznMonstersEnabled)
			{
				return;
			}
			NBestiary? bestiary = NBestiary.Instance;
			if (bestiary == null)
			{
				return;
			}
			Inject(bestiary);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[CznMonster] 图鉴 CZN 区块注入失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void Inject(NBestiary bestiary)
	{
		FieldInfo? listField = AccessTools.Field(typeof(NBestiary), BestiaryListFieldName);
		if (listField?.GetValue(bestiary) is not VBoxContainer bestiaryList)
		{
			return;
		}
		if (_addEntries == null)
		{
			if (!_missingMemberWarned)
			{
				_missingMemberWarned = true;
				GD.PushWarning($"[CznMonster] NBestiary.{AddEntriesMethodName} 不存在，图鉴区块未注入（游戏版本变更？）");
			}
			return;
		}

		// 区块标题：分隔条与原版 EVENTS 标题同构
		bestiaryList.AddChildSafely(
			NBestiaryLabelDivider.Create(new LocString("bestiary", SectionLocKey)));

		// 6 只 CZN 怪：首领 + 5 只召唤物，同表一个遭遇（Boss），条目按 RoomType 排序
		List<BestiaryEntry> entries = new();
		EncounterModel boss = ModelDb.Encounter<PolemicaBoss>();
		foreach (MonsterModel monster in boss.AllPossibleMonsters)
		{
			entries.Add(BestiaryEntry.FromMonster(monster, boss, boss.RoomType));
		}

		if (entries.Count == 0)
		{
			return;
		}

		_addEntries.Invoke(bestiary, new object[] { entries });
	}
}
