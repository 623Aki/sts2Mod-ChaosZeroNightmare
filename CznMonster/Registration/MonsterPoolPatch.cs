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
/// <item>非首领遭遇：两种模式都入池，按 <see cref="ICznMonsterEncounter.TargetActIndex"/> 过滤章节。</item>
/// <item>首领遭遇：只在「首领位置：随机」时入池，且只注入运行中的 mutable act ——
/// ModelDb 的 canonical act 供图鉴使用，注入会造成图鉴里同一只首领重复出现
/// （图鉴由 <see cref="CznMonsterBestiaryPatch"/> 单独补区块）。</item>
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

			// 1) 非首领遭遇：按章节追加（空清单时无副作用）
			foreach (EncounterModel encounter in MonsterContent.NonBossEncounters)
			{
				if (encounter is not ICznMonsterEncounter czn || czn.TargetActIndex < 0 || czn.TargetActIndex == __instance.Index)
				{
					extra.Add(encounter);
				}
			}

			// 2) 首领遭遇：随机模式并入原生首领池，由原生 rng.NextItem(AllBossEncounters) 抽取。
			//    指定模式不入池：那只章的首领由 BossPlacementPatch 显式替换，其余章节保持原生。
			if (MonsterSettings.EffectiveBossPosRandom && __instance.IsMutable)
			{
				foreach (EncounterModel boss in MonsterContent.BossEncounters)
				{
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
}
