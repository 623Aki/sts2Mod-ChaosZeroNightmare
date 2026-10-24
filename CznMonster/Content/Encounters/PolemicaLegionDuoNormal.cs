using System.Collections.Generic;
using CznMonster.Content.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 库勒提双卫（普通遭遇，第 1 章）：不屈的库勒提 ×1 + 装甲库勒提 ×1。
///
/// <para>与首领战第一波召唤物是同一批模型、同一套招式，只是身份与数值分开：
/// 由 <see cref="PolemicaAddMonster.IsSummoned"/> 分流 —— 这里默认 false，
/// 于是<b>不挂 MinionPower</b>（正式敌人，必须全部击杀）、走<b>普通怪数值</b>、
/// 登场动画用 <c>normal_enter</c>。</para>
///
/// <para>只进第 1 章普通池；不建场景，槽位全部传 null，站位交给原生
/// <c>PositionEnemies</c> 按包围盒自动排布。</para>
/// </summary>
public sealed class PolemicaLegionDuoNormal : EncounterModel, ICznMonsterEncounter
{
	// 目标章节：第 1 章（0）
	private static readonly int[] Acts = { 0 };

	/// <inheritdoc />
	public IReadOnlyList<int>? TargetActIndexes => Acts;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Monster;

	/// <inheritdoc />
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<PolemicaLegionSoldier>(),
		ModelDb.Monster<PolemicaLegionShield>(),
	};

	/// <inheritdoc />
	/// <remarks>两只都是小体型（包围盒 ±92 / ±118），与原生双小怪遭遇同档。</remarks>
	public override float GetCameraScaling() => 0.85f;

	/// <inheritdoc />
	/// <remarks>
	/// 不需要同步 <see cref="PolemicaScaling"/>：普通怪数值是固定值，
	/// 只有被首领召唤时才乘章节系数。
	/// </remarks>
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 槽位传 null：没有遭遇场景时不能给槽位名（NCombatRoom 会取不到 Marker2 抛异常）
		return new (MonsterModel, string?)[]
		{
			(ModelDb.Monster<PolemicaLegionSoldier>().ToMutable(), null),
			(ModelDb.Monster<PolemicaLegionShield>().ToMutable(), null),
		};
	}
}
