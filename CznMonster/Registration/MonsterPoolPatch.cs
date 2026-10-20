using System.Collections.Generic;
using System.Linq;
using CznMonster.Content;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Registration;

/// <summary>
/// P2 遭遇池注入（设计文档 §6.3 / R6）：
/// 拦截 <see cref="ActModel.AllEncounters"/> getter，把本 mod 非首领遭遇按章节 Concat 进去。
/// 遭遇判空、IsWeak 分流、精英分流全部走原逻辑，不碰原池。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(ActModel), "get_AllEncounters")]
internal static class MonsterPoolPatch
{
	private static void Postfix(ActModel __instance, ref IEnumerable<EncounterModel> __result)
	{
		try
		{
			// 首领永远不进池，只走首领替换逻辑（§6.3）
			if (!MonsterSettings.CznMonstersEnabled)
			{
				return;
			}
			List<EncounterModel> extra = MonsterContent.NonBossEncounters
				.Where(e => e is not ICznMonsterEncounter czn || czn.TargetActIndex < 0 || czn.TargetActIndex == __instance.Index)
				.ToList();
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
