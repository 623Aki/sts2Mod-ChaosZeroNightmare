using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 波勒米卡召唤物的公共基类（设计文档 §7.5）：
/// 同一批模型两用 —— 被首领召唤时是次级敌人（挂原生 <see cref="MinionPower"/>，不阻止胜利、不掉奖励），
/// 在地图上作为普通/精英遭遇的正式敌人时则不挂（详见 <see cref="IsSummoned"/>）。
///
/// <para><see cref="ShouldShowInCompendium"/> 保持 false：不混进原生章节图鉴列表
/// （参照原生 DecimillipedeSegment 的口径）；它们改列在图鉴的专属「CZN 怪物」区块里，
/// 由 <see cref="CznMonster.Registration.CznMonsterBestiaryPatch"/> 直接添加条目。</para>
///
/// <para>音效与处决表现也在这里收口（§1.2.1 ②③ / §5.4 / §5.5）：
/// 每只召唤物只覆写自己「有哪些采样、用哪个 clip 处决」，避免 5 个子类各写一遍。</para>
/// </summary>
public abstract class PolemicaAddMonster : MonsterModel
{
	/// <summary>
	/// 本实例是「被首领召唤的次级敌人」（true）还是「地图遭遇里的正式敌人」（false，默认）。
	///
	/// <para><b>为什么必须由召唤方显式置位</b>：HP 是在 <c>Creature</c> 构造函数里读的
	/// （<c>_maxHp = maxInitialHp</c>），那一刻 <c>Monster.Creature</c> 和 <c>CombatState</c> 都还没挂上，
	/// 没法反查遭遇类型；只能在造怪之前把身份定好。</para>
	///
	/// <para><b>不需要持久化</b>：存档只记遭遇 id（不存战斗中的生物），读档后遭遇重新生成、
	/// 召唤物由首领重新召唤，身份会再次被正确置位。</para>
	///
	/// <para>数值、登场动画、死亡采样、<see cref="MinionPower"/> 全部按它分流。</para>
	/// </summary>
	public bool IsSummoned { get; internal set; }

	/// <inheritdoc />
	/// <remarks>召唤物不进原生的章节/事件图鉴流程；专属区块的条目不经此过滤。</remarks>
	public override bool ShouldShowInCompendium => false;

	/// <inheritdoc />
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "skill1");

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, ResolvedDeathSfxClip ?? "death");

	/// <inheritdoc />
	public override bool HasDeathSfx => ResolvedDeathSfxClip != null;

	/// <summary>死亡采样 clip；null = 该怪自己没有死亡采样（例如处决时已经播过 fatal）。</summary>
	protected virtual string? DeathSfxClip => "death";

	/// <summary>
	/// 本次死亡实际要播的采样。
	/// <para>被首领处决的召唤物已经播过 <c>fatal</c>（返回 null，不叠第二声）；
	/// 甲虫类在地图上独立出现时没有专属死亡采样，回退到自己的 <c>fatal</c> 采样，
	/// 否则独立战斗里三只甲虫死亡会完全没有声音。</para>
	/// </summary>
	private string? ResolvedDeathSfxClip
	{
		get
		{
			if (DeathSfxClip is { } clip)
			{
				return clip;
			}
			return IsSummoned ? null : "fatal";
		}
	}

	/// <summary>被首领处决时的动画触发器；默认 <c>Fatal</c>（需骨架里有 <c>normal_fatal</c>）。</summary>
	public virtual string ExecuteAnimTrigger => "Fatal";

	/// <summary>被处决时等待的秒数（跟随 <c>normal_fatal</c> 的前段）。</summary>
	public virtual float ExecuteAnimWait => 1.5f;

	/// <summary>被处决时的采样 clip；null = 该怪没有处决采样。</summary>
	public virtual string? ExecuteSfxClip => "fatal";

	/// <summary>登场（召唤）时的采样 clip。</summary>
	public virtual string EntranceSfxClip => "enter";

	/// <inheritdoc />
	/// <remarks>
	/// 被首领召唤时登场即标记为 Minion（次级敌人），进场当回合不行动由原生 SpawnedThisTurn 处理；
	/// 地图上独立遭遇时是正式敌人，绝不能挂 MinionPower（否则原生胜负判定会直接通过）。
	/// 登场动画是初始 <see cref="MegaCrit.Sts2.Core.Animation.AnimState"/>（引擎没有 "Spawn" 触发器），
	/// 音效在这里补播。
	/// </remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();

		// 被召唤出场用召唤采样，独立遭遇用进入战斗的 enter 采样（两组的两个 clip 指向同一 ogg）
		CznMonsterSfx.Play(this, IsSummoned ? EntranceSfxClip : "enter");

		if (IsSummoned)
		{
			await PowerCmd.Apply<MinionPower>(
				new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
		}
	}

	/// <summary>
	/// 图鉴里要展示的招式：子类按自己的动画 clip / 采样 clip 填。
	/// 名字统一走 <see cref="MonsterModel.GetBestiaryMoveName"/>（键式 <c>&lt;entry&gt;.moves.&lt;Id 去 _MOVE&gt;.title</c>）。
	/// </summary>
	protected virtual IReadOnlyList<CznBestiaryAction> BestiaryActions => Array.Empty<CznBestiaryAction>();

	/// <inheritdoc />
	/// <remarks>
	/// 召唤物共用一套骨架命名（<c>normal_idle / normal_hit / normal_death / normal_skill1..2</c>），
	/// 所以受伤 / 死亡在基类统一给出，招式由子类补。死亡采样按 <see cref="DeathSfxClip"/>
	/// （处决时已经播过 fatal 的怪为 null，保持与战斗一致，不叠第二声）。
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: BestiaryActions,
			hurtClip: "normal_hit",
			hurtSfx: null,
			dieClip: "normal_death",
			dieSfx: DeathSfxClip);
	}
}
