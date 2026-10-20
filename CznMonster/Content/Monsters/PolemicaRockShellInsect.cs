using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// 岩壳虫（CZN 1001019）：碎岩重击（2 层脆弱）/ 岩壳硬化（+4 力量 + 弃光手牌）/
/// 岩壳防御（9 伤害 + 15 格挡）。
/// </summary>
public sealed class PolemicaRockShellInsect : PolemicaAddMonster
{
	private int CrushDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 20, 18));

	private int GuardDamage =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9));

	private int GuardBlock =>
		PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15));

	private int StrengthGain =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 4);

	/// <inheritdoc />
	public override int MinInitialHp =>
		PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 108, 102));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/polemica_rock_shell_insect.tscn";

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState crush = new("ROCK_CRUSH_MOVE", CrushMove,
			new SingleAttackIntent(CrushDamage), new DebuffIntent());
		MoveState harden = new("ROCK_HARDEN_MOVE", HardenMove,
			new BuffIntent(), new CardDebuffIntent());
		MoveState guard = new("ROCK_GUARD_MOVE", GuardMove,
			new SingleAttackIntent(GuardDamage), new DefendIntent());

		// 精英没有明确 ratio：攻击为主，硬化 3 回合冷却，防御 2 回合冷却
		RandomBranchState branch = new("ROCK_BRANCH");
		branch.AddBranch(crush, MoveRepeatType.CanRepeatForever, 0.55f);
		branch.AddBranch(harden, cooldown: 3, MoveRepeatType.CanRepeatForever, 0.20f);
		branch.AddBranch(guard, cooldown: 2, MoveRepeatType.CanRepeatForever, 0.25f);

		crush.FollowUpState = branch;
		harden.FollowUpState = branch;
		guard.FollowUpState = branch;

		return new MonsterMoveStateMachine(
			new MonsterState[] { crush, harden, guard, branch }, crush);
	}

	private async Task CrushMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(CrushDamage).FromMonster(this)
			.WithAttackerAnim("Crush", 1.50f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		await PowerCmd.Apply<FrailPower>(
			new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
	}

	private async Task HardenMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "Harden", 0.90f);
		CznMonsterSfx.Play(this, "skill2");
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), Creature, StrengthGain, Creature, null);

		// 原始 CARD_MOVE_TO(CARD_SET_ALL → DISCARD)：弃置每个玩家的全部手牌
		foreach (Creature target in targets)
		{
			if (target.Player is { } player)
			{
				await CardCmd.Discard(
					new ThrowingPlayerChoiceContext(), PileType.Hand.GetPile(player).Cards);
			}
		}
	}

	private async Task GuardMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(GuardDamage).FromMonster(this)
			.WithAttackerAnim("Guard", 1.50f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1"))
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
		AnimState crush = new("normal_skill1") { NextState = idle };
		AnimState harden = new("normal_skill2") { NextState = idle };
		AnimState fatal = new("normal_fatal") { NextState = idle };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("Crush", crush);
		animator.AddAnyState("Guard", crush);
		animator.AddAnyState("Harden", harden);
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
