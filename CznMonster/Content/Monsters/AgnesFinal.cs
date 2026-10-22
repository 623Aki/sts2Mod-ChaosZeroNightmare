using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿涅丝·弗布尔加扎伊特（第二形态，CZN 3000503 骨架）：<b>仅图鉴展示用</b>的模型。
///
/// <para><b>为什么需要单独一个模型</b>：战斗里第二形态是 <see cref="Agnes"/> 运行时
/// <c>MegaSprite.SetSkeletonDataRes</c> 换骨换出来的，同一个模型实例、同一个 entry，
/// 图鉴只能看到 Form A 那套骨架和动作。要让「阿涅丝」与「阿涅丝·弗布尔加扎伊特」
/// 在图鉴里分开显示，就必须有第二个 <see cref="MonsterModel"/>：它有自己的
/// <c>VisualsPath</c>（<c>agnes_final.tscn</c>）、自己的 entry（<c>AGNES_FINAL</c>，
/// 本地化键已存在），于是两套骨架、两套动作、两条图鉴条目互不干扰。</para>
///
/// <para><b>不参与任何战斗</b>：<see cref="ShouldShowInCompendium"/> 为 false，也不进任何
/// <c>EncounterModel.AllPossibleMonsters</c>；只在 <c>CznMonsterBestiaryPatch</c> 里被手动
/// 添加为图鉴条目。血量 / 招式状态机只是 <see cref="MonsterModel"/> 的抽象契约占位。</para>
///
/// <para><b>死亡动画</b>：Form B 骨架有真正的 <c>stance1_death</c>（Form A 没有死亡动作，
/// 只能拿 <c>normal_groggy</c> 兜底），所以第二形态这条条目也是「死亡动画」的落点。</para>
/// </summary>
public sealed class AgnesFinal : MonsterModel
{
	/// <inheritdoc />
	/// <remarks>图鉴条目里不显示血量条（<c>SetupForBestiary</c> 会隐藏），这里与本体同族取值即可。</remarks>
	public override int MinInitialHp => AgnesScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 672, 640));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	/// <remarks>召唤物之外的第二个「展示型」条目，不进原生章节图鉴流程。</remarks>
	public override bool ShouldShowInCompendium => false;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/agnes_final.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析（<c>AGNES_FINAL</c> 归到 <c>AGNES</c> 采样组）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "b_heavy");

	/// <inheritdoc />
	/// <remarks>Form B 骨架的死亡采样 <c>se_3000503_stance1_death</c>。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, "death");

	/// <inheritdoc />
	/// <remarks>阿涅丝两个 bank 都没有受击采样（静音侧写，与本体同口径）。</remarks>
	public override string? HurtSfx => null;

	/// <inheritdoc />
	/// <remarks>图鉴会调 <c>SetUpForCombat()</c> 拿状态机，这里给一个空过拍占位（不参与战斗，永远不会执行）。</remarks>
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState idle = new("AGNES_FINAL_IDLE_MOVE", IdleMove, new HiddenIntent());
		idle.FollowUpState = idle;
		return new MonsterMoveStateMachine(new MonsterState[] { idle }, idle);
	}

	/// <summary>占位行动：图鉴不会执行它，仅满足状态机契约。</summary>
	private static async Task IdleMove(IReadOnlyList<Creature> targets)
	{
		await Cmd.CustomScaledWait(0.1f, 0.2f);
	}

	/// <inheritdoc />
	/// <remarks>
	/// 初始状态直接是 <c>stance1_idle</c>：这是 Form B 的待机，若沿用默认 <c>idle_loop</c>
	/// 会刷「could not find 'idle_loop'」并静止不动（AGENTS 手册 §3.4 的同一坑）。
	/// </remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("stance1_idle", isLooping: true);
		AnimState hit = new("stance1_hit") { NextState = idle };
		AnimState dead = new("stance1_death");

		CreatureAnimator animator = new(idle, controller);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>
	/// 第二形态的招式表：全部走 <c>stance1_*</c> clip，并各自挂上战斗里同源的采样。
	/// 死亡用 <c>stance1_death</c> + <c>death</c> 采样（本文件也是「阿涅丝死亡动画」的唯一落点）。
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "stance1_idle",
			actions: new CznBestiaryAction[]
			{
				new(GetBestiaryMoveName("AGNES_HEAVY_B"), "stance1_m_attack_01", "b_heavy"),
				new(GetBestiaryMoveName("AGNES_TRIPLE"), "stance1_attack_play1", "b_triple"),
				new(GetBestiaryMoveName("AGNES_SUPPRESS"), "stance1_debuff_1", "debuff"),
				new(GetBestiaryMoveName("AGNES_SLASH"), "stance1_s_attack_01_play", "b_slash"),
				new(GetBestiaryMoveName("AGNES_EXECUTE"), "stance1_s_attack_01_play", "b_slash"),
			},
			hurtClip: "stance1_hit",
			hurtSfx: null,
			dieClip: "stance1_death",
			dieSfx: "death");
	}
}
