namespace CznMonster.Content;

/// <summary>
/// CZN 遭遇的章节标记接口（设计文档 §5.2）：
/// <see cref="TargetActIndex"/> 指定追加到哪一章（-1 = 全部章节）。
/// 游戏本体 <see cref="MegaCrit.Sts2.Core.Models.EncounterModel"/> 没有该字段，故用接口扩展。
/// </summary>
internal interface ICznMonsterEncounter
{
	/// <summary>目标章节（ActModel.Index）；-1 表示全部章节。</summary>
	int TargetActIndex { get; }
}
