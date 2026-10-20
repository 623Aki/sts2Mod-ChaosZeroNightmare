using System.Collections.Generic;
using CznMonster.Content.Encounters;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content;

/// <summary>
/// 本 mod 的内容清单（设计文档 §5.3 / §6）：
/// 首领遭遇永不进池，只走首领替换逻辑；非首领遭遇按章节追加进遭遇池。
/// </summary>
/// <remarks>
/// 当前接入：波勒米卡首领遭遇（<see cref="PolemicaBoss"/>）。
/// 非首领清单仍为空，补丁侧对空清单安全。
/// </remarks>
internal static class MonsterContent
{
	/// <summary>
	/// 全部 CZN 首领遭遇：波勒米卡。
	/// </summary>
	public static IReadOnlyList<EncounterModel> BossEncounters { get; } =
		new List<EncounterModel> { ModelDb.Encounter<PolemicaBoss>() };

	/// <summary>
	/// 全部 CZN 非首领遭遇（当前为空）。
	/// TODO: 接入普通/精英/弱遭遇时填回；章节追加由 <see cref="ICznMonsterEncounter.TargetActIndex"/> 控制。
	/// </summary>
	public static IReadOnlyList<EncounterModel> NonBossEncounters { get; } = new List<EncounterModel>();

	/// <summary>
	/// 指定模式下使用的首领遭遇；清单为空时返回 null（调用方需判空）。
	/// TODO: 接入首领后此属性即为下拉/指定模式的取值来源。
	/// </summary>
	public static EncounterModel? ChosenBossEncounter => BossEncounters.Count > 0 ? BossEncounters[0] : null;

	/// <summary>判断某遭遇 id 是否为 CZN 首领。</summary>
	public static bool IsCznBoss(string entryId)
	{
		foreach (EncounterModel boss in BossEncounters)
		{
			if (boss.Id.Entry == entryId)
			{
				return true;
			}
		}
		return false;
	}
}
