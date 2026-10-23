using System.Collections.Generic;
using CznMonster.Content.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 波勒米卡首领遭遇（设计文档 §4.2）：
/// 4 个槽位（Boss + 3 召唤位），初始只生成 Boss，召唤物由 Boss 的行动创建。
/// </summary>
public sealed class PolemicaBoss : EncounterModel, ICznMonsterEncounter
{
	/// <inheritdoc />
	public int TargetActIndex => 2;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Boss;

	/// <inheritdoc />
	/// <remarks>4 个单位需要槽位场景定位；缺场景会在召唤时抛 EncounterSlots is null。</remarks>
	public override bool HasScene => true;

	/// <inheritdoc />
	/// <remarks>
	/// 参考女皇：玩家在左侧，首领和召唤物在右侧。
	/// 之前设为 true 会把玩家强行放到场景中心，和 x=960 的首领重叠。
	/// </remarks>
	public override bool FullyCenterPlayers => false;

	/// <inheritdoc />
	public override IReadOnlyList<string> Slots { get; } =
		new[] { "add1", "add2", "boss", "add3" };

	/// <inheritdoc />
	/// <remarks>
	/// CZN 包内没有音乐文件，按实际出现的章节复用原生 Boss BGM。
	/// 原来固定写第 3 章女王 BGM，放到第 1 章时该 FMOD 事件未加载，
	/// 日志会报 cannot find music path，并可能导致进入战斗时中断。
	/// </remarks>
	public override string CustomBgm
	{
		get
		{
			int act = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex
				?? PolemicaScaling.ActIndex;
			return act switch
			{
				0 => "event:/music/act1_boss_vantom",
				1 => "event:/music/act2_boss_kaiser_crab",
				_ => "event:/music/act3_boss_queen",
			};
		}
	}

	/// <inheritdoc />
	public override string BossNodePath => "res://CznMonster/images/map/polemica_boss_icon";

	/// <inheritdoc />
	public override MegaSkeletonDataResource? BossNodeSpineResource => null;

	/// <inheritdoc />
	public override float GetCameraScaling() => 0.82f;

	/// <inheritdoc />
	public override Vector2 GetCameraOffset() => Vector2.Down * 35f;

	/// <inheritdoc />
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<Polemica>(),
		ModelDb.Monster<PolemicaLegionSoldier>(),
		ModelDb.Monster<PolemicaLegionShield>(),
		ModelDb.Monster<PolemicaRockShellInsect>(),
		ModelDb.Monster<PolemicaWingShellInsect>(),
		ModelDb.Monster<PolemicaShellInsect>(),
	};

	/// <inheritdoc />
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 读档后静态章节值会丢，这里按当前 RunState 兜底。
		// 拿不到 RunState 时回落第 1 章（0），不能沿用上一次写入的值（残留会让两端劈叉）。
		PolemicaScaling.ActIndex = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex ?? 0;

		return new (MonsterModel, string?)[] { (ModelDb.Monster<Polemica>().ToMutable(), "boss") };
	}
}
