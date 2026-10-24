using System.Collections.Generic;
using CznMonster.Content.Monsters;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Content.Encounters;

/// <summary>
/// 塞拉菲翁／阿克鲁斯双怪首领遭遇（塞拉菲翁设计文档 §7.5）：
/// 2 个槽位（boss 塞拉菲翁 + add1 阿克鲁斯），开局全部生成。
/// </summary>
public sealed class SeraphionEncounter : EncounterModel, ICznMonsterEncounter
{
	// 固定入池章节：塞拉菲翁只进第 2 章首领池，由原生 rng.NextItem(AllBossEncounters) 抽取。
	// 与原生首领竞争，因此「第 2 章必出塞拉菲翁」不成立；指定模式可在原生抽取后强行替换。
	private static readonly int[] Acts = { 1 };

	/// <inheritdoc />
	public IReadOnlyList<int>? TargetActIndexes => Acts;

	/// <inheritdoc />
	public override RoomType RoomType => RoomType.Boss;

	/// <inheritdoc />
	public override bool HasScene => true;

	/// <inheritdoc />
	/// <remarks>
	/// 参考女皇：玩家在左侧，双怪在右侧；设为 true 会把玩家强行放到场景中心，与 x=1680 的首领重叠。
	/// </remarks>
	public override bool FullyCenterPlayers => false;

	/// <inheritdoc />
	public override IReadOnlyList<string> Slots { get; } = new[] { "add1", "boss" };

	/// <inheritdoc />
	/// <remarks>
	/// CZN 包内没有音乐文件，按实际出现的章节复用原生 Boss BGM
	/// （<see cref="PolemicaBoss.CustomBgm"/> 同逻辑：未加载的 FMOD 事件会 404 / 中断战斗）。
	/// </remarks>
	public override string CustomBgm
	{
		get
		{
			int act = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex
				?? SeraphionScaling.ActIndex;
			return act switch
			{
				0 => "event:/music/act1_boss_vantom",
				1 => "event:/music/act2_boss_kaiser_crab",
				_ => "event:/music/act3_boss_queen",
			};
		}
	}

	/// <inheritdoc />
	public override string BossNodePath => "res://CznMonster/images/map/seraphion_boss_icon";

	/// <inheritdoc />
	public override MegaSkeletonDataResource? BossNodeSpineResource => null;

	/// <inheritdoc />
	/// <remarks>双怪体型接近皇帝战（塞拉菲翁 Boss 档 520–600 px，文档 §5.0）。</remarks>
	public override float GetCameraScaling() => 0.80f;

	/// <inheritdoc />
	public override Vector2 GetCameraOffset() => Vector2.Down * 35f;

	/// <inheritdoc />
	/// <remarks>图鉴「CZN 怪物」区块的条目来源（2 条，见 <see cref="CznMonster.Registration.CznMonsterBestiaryPatch"/>）。</remarks>
	public override IEnumerable<MonsterModel> AllPossibleMonsters => new MonsterModel[]
	{
		ModelDb.Monster<Seraphion>(),
		ModelDb.Monster<Arkrus>(),
	};

	/// <inheritdoc />
	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		// 读档后静态章节值会丢，这里按当前 RunState 兜底。
		// 拿不到 RunState 时回落第 1 章（0），不能沿用上一次写入的值（残留会让两端劈叉）。
		SeraphionScaling.ActIndex = RunManager.Instance?.DebugOnlyGetState()?.CurrentActIndex ?? 0;

		// 塞拉菲翁先加入（boss 槽绘制层在后）；阿克鲁斯 add1 在前。
		// 敌方行动按槽位名排序：add1 < boss，阿克鲁斯先于塞拉菲翁行动。
		return new (MonsterModel, string?)[]
		{
			(ModelDb.Monster<Seraphion>().ToMutable(), "boss"),
			(ModelDb.Monster<Arkrus>().ToMutable(), "add1"),
		};
	}
}
