using System;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 波勒米卡家族的章节缩放（设计文档 §3.6）：
/// 同一只 Boss 出现在第 1/2/3 章时，按对应系数缩放生命、伤害与格挡。
/// </summary>
internal static class PolemicaScaling
{
	/// <summary>本局该 Boss 实际出现的章节（0/1/2）。由遭遇生成时写入。</summary>
	public static int ActIndex { get; set; } = 2;

	// 第 1 章 2/3、第 2 章 5/6、第 3 章 1（用分数避免 0.67 之类的舍入误差）
	private static readonly decimal[] HpMul = { 2m / 3m, 5m / 6m, 1m };

	// 第 1 章 3/4、第 2 章 9/10、第 3 章 1；伤害与格挡共用
	private static readonly decimal[] DmgMul = { 3m / 4m, 9m / 10m, 1m };

	private static int Act => Math.Clamp(ActIndex, 0, 2);

	/// <summary>按章节缩放生命值（向下取整）。</summary>
	public static int Hp(int baseHp) => (int)Math.Floor(baseHp * HpMul[Act]);

	/// <summary>按章节缩放伤害/格挡（向下取整）。</summary>
	public static int Dmg(int baseValue) => (int)Math.Floor(baseValue * DmgMul[Act]);
}
