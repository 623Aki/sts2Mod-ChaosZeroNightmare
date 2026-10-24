using System.Collections.Generic;
using System.Linq;
using CznMonster.Content;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Registration;

/// <summary>
/// P2 遭遇池注入（设计文档 §5.3 / R6）：
/// 拦截 <see cref="ActModel.AllEncounters"/> getter，把本 mod 遭遇按章节 Concat 进去。
/// 弱/普通/精英/首领的分流全部走原生派生列表（<c>AllWeakEncounters</c> /
/// <c>AllRegularEncounters</c> / <c>AllEliteEncounters</c> / <c>AllBossEncounters</c>），
/// 抽取完全由原生 <c>GrabBag</c> / <c>Rng.NextItem</c> 决定。
/// </summary>
/// <remarks>
/// 注入规则：
/// <list type="bullet">
/// <item>非首领遭遇：按 <see cref="ICznMonsterEncounter.TargetActIndexes"/> 过滤章节后入池。</item>
/// <item>首领遭遇：同样按 <see cref="ICznMonsterEncounter.TargetActIndexes"/> 入池 ——
/// 每只只进自己那一章（美梦→第 1 章、塞拉菲翁→第 2 章、波勒米卡/阿涅丝→第 3 章），
/// 再与本层原生首领一起交给原生 <c>rng.NextItem(AllBossEncounters)</c> 抽取，<b>不保证必出</b>。
/// 只注入运行中的 mutable act —— ModelDb 的 canonical act 供图鉴使用，
/// 注入会造成图鉴里同一只首领重复出现（图鉴由 <see cref="CznMonsterBestiaryPatch"/> 单独补区块）。</item>
/// </list>
/// </remarks>
[HarmonyLib.HarmonyPatch(typeof(ActModel), "get_AllEncounters")]
internal static class MonsterPoolPatch
{
	private static void Postfix(ActModel __instance, ref IEnumerable<EncounterModel> __result)
	{
		try
		{
			// 联机时强制关闭：AllEncounters 的池长度会被本 patch 改变，而 GenerateRooms
			// 在 host 与 client 两侧各跑一次（Seed 相同、靠池下标抽首领），
			// 两端池长度不同 → 抽到不同 Boss → checksum 直接劈叉。
			if (!MonsterSettings.EffectiveCznMonstersEnabled)
			{
				return;
			}

			List<EncounterModel> extra = new List<EncounterModel>();

			// 1) 非首领遭遇：按章节集合追加（普通 / 精英由原生 RoomType 分流，无需在此判断）
			foreach (EncounterModel encounter in MonsterContent.NonBossEncounters)
			{
				if (encounter is not ICznMonsterEncounter czn || AppliesToAct(czn, __instance.Index))
				{
					extra.Add(encounter);
				}
			}

			// 2) 首领遭遇：按各自章节入池（美梦→第 1 章、塞拉菲翁→第 2 章、波勒米卡/阿涅丝→第 3 章），
			//    随后与本层原生首领一起由原生 rng.NextItem(AllBossEncounters) 抽取。
			//    指定模式不再改变入池范围：原生抽完之后由 BossPlacementPatch 强行替换所选章节。
			if (__instance.IsMutable)
			{
				foreach (EncounterModel boss in MonsterContent.BossEncounters)
				{
					// 章节不匹配的首领不注入本层池子
					if (boss is ICznMonsterEncounter cznBoss && !AppliesToAct(cznBoss, __instance.Index))
					{
						continue;
					}
					if (!__result.Contains(boss))
					{
						extra.Add(boss);
					}
				}
			}

			if (extra.Count == 0)
			{
				return;
			}
			__result = __result.Concat(extra);
		}
		catch (System.Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 遭遇池注入失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>章节过滤：null / 空集合 = 全部章节，否则必须在集合里。</summary>
	private static bool AppliesToAct(ICznMonsterEncounter encounter, int actIndex)
	{
		IReadOnlyList<int>? acts = encounter.TargetActIndexes;
		if (acts == null || acts.Count == 0)
		{
			return true;
		}
		for (int i = 0; i < acts.Count; i++)
		{
			if (acts[i] == actIndex)
			{
				return true;
			}
		}
		return false;
	}
}
