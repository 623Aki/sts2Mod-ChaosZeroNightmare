using System.Threading.Tasks;
using CznMonster.Content.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 波勒米卡召唤物的公共基类（设计文档 §7.5）：
/// 统一挂原生 <see cref="MinionPower"/>（不阻止胜利、不掉奖励）、统一不进图鉴。
///
/// <para>音效与处决表现也在这里收口（§1.2.1 ②③ / §5.4 / §5.5）：
/// 每只召唤物只覆写自己「有哪些采样、用哪个 clip 处决」，避免 5 个子类各写一遍。</para>
/// </summary>
public abstract class PolemicaAddMonster : MonsterModel
{
	/// <inheritdoc />
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
}
