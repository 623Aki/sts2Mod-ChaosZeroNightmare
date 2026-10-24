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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 不屈的库勒提（CZN 1001009）：60% 战吼突刺（全体己方 +1 力量）/ 40% 撕咬（1 层易伤）。
/// </summary>
public sealed class PolemicaLegionSoldier : PolemicaAddMonster
{
	// ── 数值（同一只怪两套口径：被首领召唤 / 地图上的第 1 章普通怪）──
	// 被召唤：第 3 章基准 6（升华 7），再乘章节系数
	// 普通怪：固定 6（升华 7）；遭遇固定在第 1 章，不再乘系数
	private int AttackDamage => IsSummoned
		? PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

	/// <inheritdoc />
	/// <remarks>被召唤走「38（升华 40）× 章节系数」；作为第 1 章普通怪固定 36（升华 38）。</remarks>
	public override int MinInitialHp => IsSummoned
		? PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 38))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 38, 36);

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/polemica_legion_soldier.tscn";

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState rally = new("SOLDIER_RALLY_MOVE", RallyMove,
			new SingleAttackIntent(AttackDamage), new BuffIntent());
		MoveState harass = new("SOLDIER_HARASS_MOVE", HarassMove,
			new SingleAttackIntent(AttackDamage), new DebuffIntent());

		// 原始 card_select_ratio 60/40，连用上限 2/1
		RandomBranchState branch = new("SOLDIER_BRANCH");
		branch.AddBranch(rally, maxRepeats: 2, weight: 0.60f);
		branch.AddBranch(harass, MoveRepeatType.CannotRepeat, 0.40f);

		rally.FollowUpState = branch;
		harass.FollowUpState = branch;

		return new MonsterMoveStateMachine(
			new MonsterState[] { rally, harass, branch }, rally);
	}

	private async Task RallyMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(AttackDamage).FromMonster(this)
			.WithAttackerAnim("RallyAttack", 1.13f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		// 原始目标 ALL_ENEMY：按「己方全体」处理，给自己与波勒米卡加力量
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), CombatState.GetTeammatesOf(Creature), 1m, Creature, null);
	}

	private async Task HarassMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(AttackDamage).FromMonster(this)
			.WithAttackerAnim("HarassAttack", 1.03f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill2"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);

		await PowerCmd.Apply<VulnerablePower>(
			new ThrowingPlayerChoiceContext(), targets, 1m, Creature, null);
	}

	/// <inheritdoc />
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		// 被首领召唤出场用 normal_summon（1.57 s）；地图上的独立遭遇用进入战斗的 normal_enter
		AnimState summon = new("normal_summon") { NextState = idle };
		AnimState enter = new("normal_enter") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState dead = new("normal_death");
		AnimState rally = new("normal_skill1") { NextState = idle };
		AnimState harass = new("normal_skill2") { NextState = idle };
		AnimState surprise = new("normal_surprise") { NextState = idle };

		CreatureAnimator animator = new(IsSummoned ? summon : enter, controller);
		animator.AddAnyState("RallyAttack", rally);
		animator.AddAnyState("HarassAttack", harass);
		// 没有 normal_fatal，处决反应改用 normal_surprise（0.53 s）
		animator.AddAnyState("Surprise", surprise);
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	public override string ExecuteAnimTrigger => "Surprise";

	/// <inheritdoc />
	public override float ExecuteAnimWait => 0.55f;

	/// <inheritdoc />
	public override string? ExecuteSfxClip => null;

	/// <inheritdoc />
	protected override string? DeathSfxClip => "skill2";

	/// <inheritdoc />
	public override string EntranceSfxClip => "summon";

	/// <inheritdoc />
	/// <remarks>两个索敌招式各一条采样（战吼突刺 <c>skill1</c> / 撕咬 <c>skill2</c>）。</remarks>
	protected override IReadOnlyList<CznBestiaryAction> BestiaryActions => new[]
	{
		new CznBestiaryAction(GetBestiaryMoveName("SOLDIER_RALLY"), "normal_skill1", "skill1"),
		new CznBestiaryAction(GetBestiaryMoveName("SOLDIER_HARASS"), "normal_skill2", "skill2"),
	};
}
