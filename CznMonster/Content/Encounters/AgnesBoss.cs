using System.Collections.Generic;
using CznMonster.Content.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 阿涅丝首领遭遇（阿涅丝设计文档 §1.1 / §6）：
/// 4 个槽位（Boss + 3 召唤位），开局只生成 Boss，余响碎片由变身 / 再变身回合铺设。
/// </summary>
public sealed class AgnesBoss : EncounterModel, ICznMonsterEncounter
{
	// 固定入池章节：阿涅丝只进第 3 章首领池（与波勒米卡同池），由原生 rng.NextItem 抽取。
	// 与原生首领竞争，A10 第二首领也从该池抽（可能是波勒米卡或原生）。
	private static readonly int[] Acts = { 2 };

	/// <inheritdoc />
	public IReadOnlyList<int>? TargetActIndexes => Acts;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Boss;

	/// <inheritdoc />
	/// <remarks>4 个单位需要槽位场景定位；缺场景会在召唤时抛 EncounterSlots is null。</remarks>
	public override bool HasScene => true;

	/// <inheritdoc />
	/// <remarks>
	/// 参考波勒米卡：玩家在左侧，首领和召唤物在右侧；设为 true 会把玩家强行放到场景中心。
	/// </remarks>
	public override bool FullyCenterPlayers => false;

	/// <inheritdoc />
	public override IReadOnlyList<string> Slots { get; } =
		new[] { "add1", "add2", "boss", "add3" };

	/// <inheritdoc />
	/// <remarks>
	/// CZN 包内没有音乐文件，按实际出现的章节复用原生 Boss BGM（波勒米卡 / 塞拉菲翁同逻辑）。
	/// </remarks>
	public override string CustomBgm
	{
		get
		{
			int act = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex
				?? AgnesScaling.ActIndex;
			return act switch
			{
				0 => "event:/music/act1_boss_vantom",
				1 => "event:/music/act2_boss_kaiser_crab",
				_ => "event:/music/act3_boss_queen",
			};
		}
	}

	/// <inheritdoc />
	public override string BossNodePath => "res://CznMonster/images/map/agnes_boss_icon";

	/// <inheritdoc />
	public override MegaSkeletonDataResource? BossNodeSpineResource => null;

	/// <inheritdoc />
	/// <remarks>
	/// 变身后骨架横向铺满（861×817），镜头系数取比波勒米卡略远的 0.78，
	/// 并向下偏置让 Boss 的头部与意图图标保持在画面内。
	/// </remarks>
	public override float GetCameraScaling() => 0.78f;

	/// <inheritdoc />
	public override Vector2 GetCameraOffset() => Vector2.Down * 45f;

	/// <inheritdoc />
	/// <remarks>图鉴「CZN 怪物」区块的条目来源（本列表 2 条 + 补丁额外补的第二形态条目，见
	/// <see cref="CznMonster.Registration.CznMonsterBestiaryPatch"/>）。</remarks>
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<Agnes>(),
		ModelDb.Monster<AgnesEcho>(),
	};

	/// <inheritdoc />
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 读档后静态章节值会丢，这里按当前 RunState 兜底。
		// 拿不到 RunState 时回落第 1 章（0），不能沿用上一次写入的值（残留会让两端劈叉）。
		AgnesScaling.ActIndex = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex ?? 0;

		// 只开局生成 Boss；余响碎片在变身回合用 CreatureCmd.Add 铺满三槽
		return new (MonsterModel, string?)[]
		{
			(ModelDb.Monster<Agnes>().ToMutable(), "boss"),
		};
	}
}
