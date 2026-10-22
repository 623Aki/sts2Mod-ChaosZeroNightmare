using System;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿涅丝家族的章节缩放（设计文档 §2.2）：
/// 灾厄第 4 赛季 Boss，定位 act2 末 / act3 头目 —— 生命第 1 章 ×0.85（已核实设计稿
/// 「1/2 章 ×0.85 / 1.0」，第 3 章不再加码），伤害第 1 章 ×0.9、此后全额。
/// </summary>
internal static class AgnesScaling
{
	/// <summary>本局该 Boss 实际出现的章节（0/1/2）。由遭遇生成时写入。</summary>
	public static int ActIndex { get; set; } = 2;

	// 第 1 章 17/20、第 2/3 章 1
	private static readonly decimal[] HpMul = { 17m / 20m, 1m, 1m };

	// 第 1 章 9/10、第 2/3 章 1；伤害与格挡共用
	private static readonly decimal[] DmgMul = { 9m / 10m, 1m, 1m };

	private static int Act => Math.Clamp(ActIndex, 0, 2);

	/// <summary>按章节缩放生命值（向下取整）。</summary>
	public static int Hp(int baseHp) => (int)Math.Floor(baseHp * HpMul[Act]);

	/// <summary>按章节缩放伤害/格挡（向下取整）。</summary>
	public static int Dmg(int baseValue) => (int)Math.Floor(baseValue * DmgMul[Act]);
}
