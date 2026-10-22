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
/// 统一挂原生 <see cref="MinionPower"/>（不阻止胜利、不掉奖励）。
///
/// <para><see cref="ShouldShowInCompendium"/> 保持 false：召唤物不混进原生章节图鉴列表
/// （参照原生 DecimillipedeSegment 的口径）；它们改列在图鉴的专属「CZN 怪物」区块里，
/// 由 <see cref="CznMonster.Registration.CznMonsterBestiaryPatch"/> 直接添加条目。</para>
///
/// <para>音效与处决表现也在这里收口（§1.2.1 ②③ / §5.4 / §5.5）：
/// 每只召唤物只覆写自己「有哪些采样、用哪个 clip 处决」，避免 5 个子类各写一遍。</para>
/// </summary>
public abstract class PolemicaAddMonster : MonsterModel
{
	/// <inheritdoc />
	/// <remarks>召唤物不进原生的章节/事件图鉴流程；专属区块的条目不经此过滤。</remarks>
	public override bool ShouldShowInCompendium => false;

	/// <inheritdoc />
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "skill1");

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg；<see cref="DeathSfxClip"/> 为 null 时不发声。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, DeathSfxClip ?? "death");

	/// <inheritdoc />
	public override bool HasDeathSfx => DeathSfxClip != null;

	/// <summary>死亡采样 clip；null = 该怪没有死亡采样（例如处决时已经播过 fatal）。</summary>
	protected virtual string? DeathSfxClip => "death";

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
	/// 召唤物登场即标记为 Minion；进场当回合不行动由原生 SpawnedThisTurn 处理。
	/// 登场动画是初始 <see cref="MegaCrit.Sts2.Core.Animation.AnimState"/>（引擎没有 "Spawn" 触发器），
	/// 音效在这里补播。
	/// </remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, EntranceSfxClip);
		await PowerCmd.Apply<MinionPower>(
			new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
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
