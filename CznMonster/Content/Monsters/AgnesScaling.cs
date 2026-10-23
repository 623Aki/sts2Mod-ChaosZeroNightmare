using System;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿涅丝家族的章节缩放（设计文档 §2.2）：
/// 灾厄第 4 赛季 Boss，定位 act2 末 / act3 头目 —— 生命按<b>原生同幕 Boss 血池</b>标定
/// （第 3 章 640 为锚点，前两章按原生阶梯收窄），伤害第 1 章 ×0.9、此后全额。
///
/// <para><b>为什么第 1/2 章系数压到 0.40 / 0.62</b>：原生 Boss 血量随幕是阶梯式
/// （第 1 幕单体 173~262、第 2 幕 321~428、第 3 幕 512~636），并非等比折减。
/// 旧系数 0.85 / 1.0 会让第 1 章达到 544（原生上限 262 的两倍以上），
/// 再叠加余响碎片与变身回血后超模近 3 倍。</para>
/// </summary>
internal static class AgnesScaling
{
	/// <summary>本局该 Boss 实际出现的章节（0/1/2）。由遭遇生成时写入。</summary>
	public static int ActIndex { get; set; } = 2;

	// 第 1 章 2/5、第 2 章 31/50、第 3 章 1（对齐原生血池阶梯：256 / 397 / 640）
	private static readonly decimal[] HpMul = { 2m / 5m, 31m / 50m, 1m };

	// 第 1 章 9/10、第 2/3 章 1；伤害与格挡共用
	private static readonly decimal[] DmgMul = { 9m / 10m, 1m, 1m };

	private static int Act => Math.Clamp(ActIndex, 0, 2);

	/// <summary>按章节缩放生命值（向下取整）。</summary>
	public static int Hp(int baseHp) => (int)Math.Floor(baseHp * HpMul[Act]);

	/// <summary>按章节缩放伤害/格挡（向下取整）。</summary>
	public static int Dmg(int baseValue) => (int)Math.Floor(baseValue * DmgMul[Act]);
}
