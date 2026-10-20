using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
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
/// 军团兵蚁（CZN 1001009）：60% 战吼突刺（全体己方 +1 力量）/ 40% 撕咬（1 层易伤）。
/// </summary>
public sealed class PolemicaLegionSoldier : PolemicaAddMonster
{
	private int AttackDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <inheritdoc />
	public override int MinInitialHp =>
		PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 38));

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
		// 兵蚁只有被召唤出场，登场用 normal_summon（1.57 s）；进入房间的 normal_enter 不作初始状态
		AnimState summon = new("normal_summon") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState dead = new("normal_death");
		AnimState rally = new("normal_skill1") { NextState = idle };
		AnimState harass = new("normal_skill2") { NextState = idle };
		AnimState surprise = new("normal_surprise") { NextState = idle };

		CreatureAnimator animator = new(summon, controller);
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
}
