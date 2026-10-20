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
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 壳虫（CZN 1001004）：甲壳撕咬（2 层虚弱）/ 甲壳防御（9 伤害 + 15 格挡）。
/// </summary>
public sealed class PolemicaShellInsect : PolemicaAddMonster
{
	private int BiteDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 20, 18));

	private int GuardDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9));

	private int GuardBlock =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15));

	/// <inheritdoc />
	public override int MinInitialHp =>
		PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 111, 105));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/polemica_shell_insect.tscn";

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState bite = new("SHELL_BITE_MOVE", BiteMove,
			new SingleAttackIntent(BiteDamage), new DebuffIntent());
		MoveState guard = new("SHELL_GUARD_MOVE", GuardMove,
			new SingleAttackIntent(GuardDamage), new DefendIntent());

		// 原始卡池三张（攻击/硬化/防御）共享；本设计只保留攻击与防御，避免三只精英同时弃手牌
		RandomBranchState branch = new("SHELL_BRANCH");
		branch.AddBranch(bite, MoveRepeatType.CanRepeatForever, 0.65f);
		branch.AddBranch(guard, MoveRepeatType.CannotRepeat, 0.35f);

		bite.FollowUpState = branch;
		guard.FollowUpState = branch;

		return new MonsterMoveStateMachine(
			new MonsterState[] { bite, guard, branch }, bite);
	}

	private async Task BiteMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(BiteDamage).FromMonster(this)
			.WithAttackerAnim("Attack", 1.50f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
	}

	private async Task GuardMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(GuardDamage).FromMonster(this)
			.WithAttackerAnim("Guard", 1.50f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill2"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		await CreatureCmd.GainBlock(Creature, GuardBlock, ValueProp.Move, null);
	}

	/// <inheritdoc />
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		AnimState enter = new("normal_enter") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState dead = new("normal_death");
		AnimState attack = new("normal_skill1") { NextState = idle };
		AnimState guard = new("normal_skill2") { NextState = idle };
		AnimState fatal = new("normal_fatal") { NextState = idle };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("Attack", attack);
		animator.AddAnyState("Guard", guard);
		animator.AddAnyState("Fatal", fatal);
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>处决时已经播过 <c>fatal</c> 采样，死亡不再叠一条。</remarks>
	protected override string? DeathSfxClip => null;
}
