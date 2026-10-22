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
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 装甲库勒提（CZN 1001010）：40% 撞角（6 伤害）/ 60% 掩护（6 伤害 + 全体己方 15 格挡）。
/// </summary>
public sealed class PolemicaLegionShield : PolemicaAddMonster
{
	private int StrikeDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6));

	private int CoverDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6));

	private int CoverBlock =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15));

	/// <inheritdoc />
	public override int MinInitialHp =>
		PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 44, 42));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/polemica_legion_shield.tscn";

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState strike = new("SHIELD_STRIKE_MOVE", StrikeMove,
			new SingleAttackIntent(StrikeDamage));
		MoveState cover = new("SHIELD_COVER_MOVE", CoverMove,
			new SingleAttackIntent(CoverDamage), new DefendIntent());

		// 原始 ratio 40/60，连用上限 1/2
		RandomBranchState branch = new("SHIELD_BRANCH");
		branch.AddBranch(strike, maxRepeats: 1, weight: 0.40f);
		branch.AddBranch(cover, maxRepeats: 2, weight: 0.60f);

		strike.FollowUpState = branch;
		cover.FollowUpState = branch;

		return new MonsterMoveStateMachine(
			new MonsterState[] { strike, cover, branch }, strike);
	}

	private async Task StrikeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(StrikeDamage).FromMonster(this)
			.WithAttackerAnim("Attack", 1.13f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
	}

	private async Task CoverMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(CoverDamage).FromMonster(this)
			.WithAttackerAnim("Cover", 1.03f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill2"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		// 原始目标 ALL_ENEMY + SHIELD_ATTR_AUTO：给己方全体加格挡
		foreach (Creature ally in CombatState.GetTeammatesOf(Creature))
		{
			if (ally.IsAlive)
			{
				await CreatureCmd.GainBlock(ally, CoverBlock, ValueProp.Move, null);
			}
		}
	}

	/// <inheritdoc />
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		AnimState summon = new("normal_summon") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState dead = new("normal_death");
		AnimState attack = new("normal_skill1") { NextState = idle };
		AnimState cover = new("normal_skill2") { NextState = idle };
		AnimState surprise = new("normal_surprise") { NextState = idle };

		CreatureAnimator animator = new(summon, controller);
		animator.AddAnyState("Attack", attack);
		animator.AddAnyState("Cover", cover);
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
	/// <remarks>撞角 <c>skill1</c> / 掩护 <c>skill2</c>。</remarks>
	protected override IReadOnlyList<CznBestiaryAction> BestiaryActions => new[]
	{
		new CznBestiaryAction(GetBestiaryMoveName("SHIELD_STRIKE"), "normal_skill1", "skill1"),
		new CznBestiaryAction(GetBestiaryMoveName("SHIELD_COVER"), "normal_skill2", "skill2"),
	};
}
