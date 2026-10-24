using System;
using System.Collections.Generic;
using System.Linq;
using CznMonster.Content.Encounters;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content;

/// <summary>
/// 本 mod 的内容清单（设计文档 §5.3 / §6）：
/// 首领遭遇按各自固定章节并入原生池、由原生 rng 抽取；
/// 「指定」开启时，可在原生抽取之后被强行替换进所选章节；
/// 非首领遭遇按章节追加进遭遇池。
/// </summary>
/// <remarks>
/// 当前接入：波勒米卡首领遭遇（<see cref="PolemicaBoss"/>）、
/// 塞拉菲翁／阿克鲁斯双怪首领遭遇（<see cref="SeraphionEncounter"/>）、
/// 阿涅丝首领遭遇（<see cref="AgnesBoss"/>）与
/// 美梦首领遭遇（<see cref="SweetDreamBoss"/>）；
/// 非首领接入库勒提双卫（普通）与甲壳虫群（精英）。
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
	/// 各自按 <c>TargetActIndexes</c> 并入对应章节的原生首领池、由原生 rng 抽取；
	/// 「指定」开启时也作为替换来源。
	/// </summary>
	public static IReadOnlyList<EncounterModel> BossEncounters => _bossEncounters ??= BossCatalog
		.Select(entry => entry.Resolve())
		.ToList();

	/// <summary>
	/// 全部 CZN 非首领遭遇（普通 / 精英）。
	///
	/// <para>入池章节由 <see cref="ICznMonsterEncounter.TargetActIndexes"/> 过滤（null / 空 = 全部章节）；
	/// 普通 / 精英 / 弱的分流完全交给原生 —— <c>RoomType</c> 决定进哪个池，<c>IsWeak</c> 决定进前 3 间弱池还是常规池。</para>
	///
	/// <para>这两条遭遇复用的是波勒米卡召唤物的同一批 <c>MonsterModel</c>，
	/// 由 <see cref="CznMonster.Content.Monsters.PolemicaAddMonster.IsSummoned"/> 区分
	/// 「被召唤的次级敌人」与「地图上的正式敌人」：招式不变，数值、登场动画、
	/// 死亡采样与 MinionPower 按身份分流。</para>
	/// </summary>
	public static IReadOnlyList<EncounterModel> NonBossEncounters => _nonBossEncounters ??=
		new EncounterModel[]
		{
			ModelDb.Encounter<PolemicaLegionDuoNormal>(),   // 不屈的库勒提 ×1 + 装甲库勒提 ×1（第 1 章普通）
			ModelDb.Encounter<PolemicaInsectTrioElite>(),  // 岩壳虫 + 翼壳虫 + 壳虫（第 2 章精英）
		};

	private static IReadOnlyList<EncounterModel>? _nonBossEncounters;

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
		return ContainsEntry(BossEncounters, entryId);
	}

	/// <summary>
	/// 判断某遭遇 id 是否为本 mod 的任意遭遇（首领 + 普通/精英）。
	/// 用于「本场战斗要不要预热 CZN 采样」这类不分首领/非首领的判定。
	/// </summary>
	public static bool IsCznEncounter(string entryId)
	{
		return ContainsEntry(BossEncounters, entryId) || ContainsEntry(NonBossEncounters, entryId);
	}

	private static bool ContainsEntry(IReadOnlyList<EncounterModel> encounters, string entryId)
	{
		foreach (EncounterModel encounter in encounters)
		{
			if (encounter.Id.Entry == entryId)
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
