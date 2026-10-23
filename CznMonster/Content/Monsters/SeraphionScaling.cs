using System;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 塞拉菲翁／阿克鲁斯双怪组的章节缩放（塞拉菲翁设计文档 §3.6，与 <see cref="PolemicaScaling"/> 同构）：
/// 同一组双怪 Boss 出现在第 1/2/3 章时，按对应系数缩放生命、伤害与格挡。
/// </summary>
/// <remarks>
/// 与波勒米卡各自独立登记 <see cref="ActIndex"/>：两只 Boss 分别走
/// <see cref="SeraphionEncounter.GenerateMonsters"/> 生成，由遭遇生成时写入。
/// 验收基准（文档 §9 T14，第 1 章）：塞拉菲翁 130 HP / 双牙 2×9，阿克鲁斯 101 HP / 守护突进 3×9。
/// </remarks>
internal static class SeraphionScaling
{
	/// <summary>本局该组双怪实际出现的章节（0/1/2）。由遭遇生成时写入。</summary>
	public static int ActIndex { get; set; } = 2;

	// 第 1 章 7/10、第 2 章 17/20、第 3 章 1
	// （双体总血池 274 / 333 / 392；第 3 章计入一次救援颂歌后 ≈512，对齐原生 512~636）
	private static readonly decimal[] HpMul = { 7m / 10m, 17m / 20m, 1m };

	// 第 1 章 3/4、第 2 章 9/10、第 3 章 1；伤害与格挡共用
	private static readonly decimal[] DmgMul = { 3m / 4m, 9m / 10m, 1m };

	private static int Act => Math.Clamp(ActIndex, 0, 2);

	/// <summary>按章节缩放生命值（向下取整）。</summary>
	public static int Hp(int baseHp) => (int)Math.Floor(baseHp * HpMul[Act]);

	/// <summary>按章节缩放伤害/格挡（向下取整）。</summary>
	public static int Dmg(int baseValue) => (int)Math.Floor(baseValue * DmgMul[Act]);
}
