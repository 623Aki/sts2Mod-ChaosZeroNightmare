using System.Collections.Generic;

namespace CznMonster.Content;

/// <summary>
/// CZN 遭遇的章节标记接口（设计文档 §5.2）：
/// <see cref="TargetActIndexes"/> 指定追加到哪几章（null / 空集合 = 全部章节）。
/// 游戏本体 <see cref="MegaCrit.Sts2.Core.Models.EncounterModel"/> 没有该字段，故用接口扩展。
/// </summary>
/// <remarks>
/// 章节用集合而不是单值，便于一条遭遇跨多章出现；当前两条独立遭遇各固定一章
/// （库勒提双卫 → 第 1 章普通池，甲壳虫群 → 第 2 章精英池），首领遭遇填默认章节。
/// </remarks>
internal interface ICznMonsterEncounter
{
	/// <summary>
	/// 可出现的章节（<see cref="MegaCrit.Sts2.Core.Models.ActModel.Index"/>）。
	/// null 或空集合表示全部章节。
	/// </summary>
	IReadOnlyList<int>? TargetActIndexes { get; }
}
