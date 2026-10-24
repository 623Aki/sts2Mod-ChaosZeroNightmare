using System.Collections.Generic;
using CznMonster.Content.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 甲壳虫群（精英遭遇，第 2 章）：岩壳虫 + 翼壳虫 + 壳虫。
///
/// <para>与首领战第二波召唤物是同一批模型、同一套招式，只是身份与数值分开：
/// 由 <see cref="PolemicaAddMonster.IsSummoned"/> 分流 —— 这里默认 false，
/// 于是<b>不挂 MinionPower</b>（正式敌人，三只都必须击杀）、走<b>精英数值</b>，
/// 战斗结束走原生精英奖励（遗物 + 35~45 金币）。</para>
///
/// <para>只进第 2 章精英池；不建场景，槽位全部传 null，站位交给原生
/// <c>PositionEnemies</c> 自动排布。</para>
/// </summary>
public sealed class PolemicaInsectTrioElite : EncounterModel, ICznMonsterEncounter
{
	// 目标章节：第 2 章（1）
	private static readonly int[] Acts = { 1 };

	/// <inheritdoc />
	public IReadOnlyList<int>? TargetActIndexes => Acts;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Elite;

	/// <inheritdoc />
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<PolemicaRockShellInsect>(),
		ModelDb.Monster<PolemicaWingShellInsect>(),
		ModelDb.Monster<PolemicaShellInsect>(),
	};

	/// <inheritdoc />
	/// <remarks>三只小体型（包围盒 170~172 宽），与原生多怪精英同档。</remarks>
	public override float GetCameraScaling() => 0.85f;

	/// <inheritdoc />
	/// <remarks>
	/// 不需要同步 <see cref="PolemicaScaling"/>：精英数值是固定值，
	/// 只有被首领召唤时才乘章节系数。
	/// </remarks>
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 槽位传 null：没有遭遇场景时不能给槽位名（NCombatRoom 会取不到 Marker2 抛异常）
		return new (MonsterModel, string?)[]
		{
			(ModelDb.Monster<PolemicaRockShellInsect>().ToMutable(), null),
			(ModelDb.Monster<PolemicaWingShellInsect>().ToMutable(), null),
			(ModelDb.Monster<PolemicaShellInsect>().ToMutable(), null),
		};
	}
}
