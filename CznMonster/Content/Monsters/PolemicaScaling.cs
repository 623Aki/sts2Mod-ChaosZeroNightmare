using System;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 波勒米卡家族的章节缩放（设计文档 §3.6）：
/// 同一只 Boss 出现在第 1/2/3 章时，按对应系数缩放生命、伤害与格挡。
/// </summary>
internal static class PolemicaScaling
{
	/// <summary>本局该 Boss 实际出现的章节（0/1/2）。由遭遇生成时写入。</summary>
	public static int ActIndex { get; set; } = 2;

	// 第 1 章 11/20、第 2 章 3/4、第 3 章 1
	// （含第一波召唤物的总血池：310 / 424 / 568，对齐原生 306~308 / 408~428 / 512~636）
	private static readonly decimal[] HpMul = { 11m / 20m, 3m / 4m, 1m };

	// 第 1 章 3/4、第 2 章 9/10、第 3 章 1；伤害与格挡共用
	private static readonly decimal[] DmgMul = { 3m / 4m, 9m / 10m, 1m };

	private static int Act => Math.Clamp(ActIndex, 0, 2);

	/// <summary>
	/// 按当前 RunState 同步章节。只服务于「首领 + 它召唤出来的召唤物」：
	/// 召唤物数值 = 第 3 章基准 × 当前章节系数，所以在首领遭遇生成时写一次。
	/// 地图上的独立遭遇用固定数值，不读这里，也就不需要调用。
	/// </summary>
	/// <remarks>
	/// 读档后静态值会丢，不能沿用上一次写入的值（残留会让两端劈叉）。
	/// </remarks>
	public static void SyncActIndex()
	{
		// 拿不到 RunState 时回落第 1 章（0），不要沿用旧值
		ActIndex = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex ?? 0;
	}

	/// <summary>按章节缩放生命值（向下取整）。</summary>
	public static int Hp(int baseHp) => (int)Math.Floor(baseHp * HpMul[Act]);

	/// <summary>按章节缩放伤害/格挡（向下取整）。</summary>
	public static int Dmg(int baseValue) => (int)Math.Floor(baseValue * DmgMul[Act]);
}
