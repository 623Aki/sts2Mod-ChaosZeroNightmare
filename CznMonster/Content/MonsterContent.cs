using System;
using System.Collections.Generic;
using System.Linq;
using CznMonster.Content.Encounters;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content;

/// <summary>
/// 本 mod 的内容清单（设计文档 §5.3 / §6）：
/// 首领遭遇既可由原生池抽出（随机模式），也可被显式替换进指定章节（指定模式）；
/// 非首领遭遇按章节追加进遭遇池。
/// </summary>
/// <remarks>
/// 当前接入：波勒米卡首领遭遇（<see cref="PolemicaBoss"/>）、
/// 塞拉菲翁／阿克鲁斯双怪首领遭遇（<see cref="SeraphionEncounter"/>）、
/// 阿涅丝首领遭遇（<see cref="AgnesBoss"/>）与
/// 美梦首领遭遇（<see cref="SweetDreamBoss"/>）。
/// 非首领清单仍为空，补丁侧对空清单安全。
/// </remarks>
internal static class MonsterContent
{
	/// <summary>
	/// 首领目录：一项对应一只「首领级」遭遇。
	/// <para>延迟解析（<see cref="BossEntry.Resolve"/>）是刻意的：设置面板注册时机早于
	/// <c>ModelDb.Init()</c>，此时 <c>ModelDb.Encounter&lt;T&gt;()</c> 还取不到实例。</para>
	/// </summary>
	public static IReadOnlyList<BossEntry> BossCatalog { get; } = new BossEntry[]
	{
		new(() => ModelDb.Encounter<PolemicaBoss>()),
		new(() => ModelDb.Encounter<SeraphionEncounter>()),
		new(() => ModelDb.Encounter<AgnesBoss>()),
		new(() => ModelDb.Encounter<SweetDreamBoss>()),
	};

	private static IReadOnlyList<EncounterModel>? _bossEncounters;

	/// <summary>
	/// 全部 CZN 首领遭遇（顺序与 <see cref="BossCatalog"/> 一致）。
	/// 随机模式下并入原生首领池由原生抽取；指定模式下用于替换。
	/// </summary>
	public static IReadOnlyList<EncounterModel> BossEncounters => _bossEncounters ??= BossCatalog
		.Select(entry => entry.Resolve())
		.ToList();

	/// <summary>
	/// 全部 CZN 非首领遭遇（当前为空，是已知缺口）。
	/// TODO: 接入普通/精英/弱遭遇时填回；章节追加由 <see cref="ICznMonsterEncounter.TargetActIndex"/> 控制。
	/// </summary>
	public static IReadOnlyList<EncounterModel> NonBossEncounters { get; } = new List<EncounterModel>();

	/// <summary>
	/// 按设置里的「指定首领」取值解析具体首领：
	/// 0（或越界）= 随机，返回 null；1..N = <see cref="BossEncounters"/> 下标 +1。
	/// </summary>
	public static EncounterModel? GetSelectedBoss(int select)
	{
		if (select <= 0)
		{
			return null;
		}
		IReadOnlyList<EncounterModel> list = BossEncounters;
		int index = select - 1;
		return index < list.Count ? list[index] : null;
	}

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

	/// <summary>
	/// 首领条目：只持有「怎么取到遭遇」的委托，不持有实例。
	/// </summary>
	public sealed class BossEntry
	{
		internal BossEntry(Func<EncounterModel> resolve)
		{
			Resolve = resolve;
		}

		/// <summary>取 canonical 遭遇实例（需要 ModelDb 已初始化）。</summary>
		internal Func<EncounterModel> Resolve { get; }

		/// <summary>设置下拉框显示的本地化名称（取 encounters 表的 title）。</summary>
		public string DisplayName => Resolve().Title.GetFormattedText();
	}
}
