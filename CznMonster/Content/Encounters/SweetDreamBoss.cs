using System.Collections.Generic;
using CznMonster.Content.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 美梦首领遭遇（美梦设计文档 §4.1）：slot0 单体、无召唤物、无伴生
/// （CZN 出场战斗 sin_00189 / base_00047 / story_00032/121 全部单槽）。
/// </summary>
public sealed class SweetDreamBoss : EncounterModel, ICznMonsterEncounter
{
	// 固定入池章节：美梦只进第 1 章首领池，由原生 rng.NextItem(AllBossEncounters) 抽取。
	// 与原生首领竞争，因此「第 1 章必出美梦」不成立；指定模式可在原生抽取后强行替换。
	private static readonly int[] Acts = { 0 };

	/// <inheritdoc />
	public IReadOnlyList<int>? TargetActIndexes => Acts;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Boss;

	/// <inheritdoc />
	/// <remarks>场景只供摆位（boss 一个 Marker2）；缺场景会在生成时报错，与 PolemicaBoss 相同规则。</remarks>
	public override bool HasScene => true;

	/// <inheritdoc />
	public override bool FullyCenterPlayers => false;

	/// <inheritdoc />
	public override IReadOnlyList<string> Slots { get; } = new[] { "boss" };

	/// <inheritdoc />
	/// <remarks>
	/// CZN 包内没有音乐文件，按实际出现的章节复用原生 Boss BGM（同 <see cref="PolemicaBoss"/>）。
	/// </remarks>
	public override string CustomBgm
	{
		get
		{
			int act = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex
				?? SweetDreamScaling.ActIndex;
			return act switch
			{
				0 => "event:/music/act1_boss_vantom",
				1 => "event:/music/act2_boss_kaiser_crab",
				_ => "event:/music/act3_boss_queen",
			};
		}
	}

	/// <inheritdoc />
	public override string BossNodePath => "res://CznMonster/images/map/sweet_dream_boss_icon";

	/// <inheritdoc />
	public override MegaSkeletonDataResource? BossNodeSpineResource => null;

	/// <inheritdoc />
	public override float GetCameraScaling() => 0.82f;

	/// <inheritdoc />
	public override Vector2 GetCameraOffset() => Vector2.Down * 35f;

	/// <inheritdoc />
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<SweetDream>(),
	};

	/// <inheritdoc />
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 读档后静态章节值会丢，这里按当前 RunState 兜底。
		// 拿不到 RunState 时回落第 1 章（0），**不能**沿用上一次写入的值：
		// 静态字段残留会让两端算出不同的血量 / 伤害，直接劈叉。
		SweetDreamScaling.ActIndex = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex ?? 0;

		return new (MonsterModel, string?)[] { (ModelDb.Monster<SweetDream>().ToMutable(), "boss") };
	}
}
