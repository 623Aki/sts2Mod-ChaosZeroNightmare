using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 岩壳虫（CZN 1001019）：碎岩重击（9 伤害 + 2 层虚弱）/ 岩壳硬化（+4 力量 + 抽牌堆最高稀有度 1 张牌进弃牌堆）/
/// 岩壳防御（9 伤害 + 15 格挡）。
/// </summary>
public sealed class PolemicaRockShellInsect : PolemicaAddMonster
{
	// ── 数值（同一只怪两套口径：被首领召唤 / 地图上的第 2 章精英）──
	// 被召唤：第 3 章基准（攻击 9 / 格挡 15，升华 11·10 / 17），再乘章节系数
	// 精英：固定攻击 8（升华 10）、格挡 12（升华 14）；遭遇固定在第 2 章，不再乘系数
	private int CrushDamage => IsSummoned
		? PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 11, 9))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

	private int GuardDamage => IsSummoned
		? PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

	private int GuardBlock => IsSummoned
		? PolemicaScaling.Dmg(AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 12);

	private int StrengthGain =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 4);

	/// <summary>
	/// 岩壳硬化的取牌优先级：从高稀有度到低依次尝试，取第一个非空的档位。
	/// 最后一条为兜底，保证抽牌堆非空时一定能选出 1 张（诅咒/状态等也在此档）。
	/// </summary>
	private static readonly Func<CardModel, bool>[] RarityPriorities =
	{
		c => c.Rarity is CardRarity.Rare or CardRarity.Ancient,
		c => c.Rarity == CardRarity.Uncommon,
		c => c.Rarity == CardRarity.Common,
		_ => true,
	};

	/// <inheritdoc />
	/// <remarks>被召唤走「102（升华 108）× 章节系数」；作为第 2 章精英固定 62（升华 68）。</remarks>
	public override int MinInitialHp => IsSummoned
		? PolemicaScaling.Hp(AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 108, 102))
		: AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 68, 62);

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

		// 碎岩重击：2 层虚弱
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
	}

	private async Task HardenMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "Harden", 0.90f);
		CznMonsterSfx.Play(this, "skill2");
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), Creature, StrengthGain, Creature, null);

		// 原始 CARD_MOVE_TO(CARD_SET_HIGHEST_RARITY → DISCARD)：
		// 每个玩家的抽牌堆中挑 1 张稀有度最高的牌，移入弃牌堆
		foreach (Creature target in targets)
		{
			// 宠物/召唤物的归属者也要处理：先取 Player，没有则取 PetOwner
			Player? player = target.Player ?? target.PetOwner;
			if (player == null)
			{
				continue;
			}

			List<CardModel> drawPile = PileType.Draw.GetPile(player).Cards.ToList();
			if (drawPile.Count == 0)
			{
				continue;
			}

			// 参照原版 ThievingHopper._stealPriorities：从高到低依次尝试档位，
			// 取第一个有结果的档位；最后一条为兜底，保证抽牌堆非空时必有候选
			List<CardModel> candidates = drawPile;
			foreach (Func<CardModel, bool> priority in RarityPriorities)
			{
				List<CardModel> filtered = drawPile.Where(priority).ToList();
				if (filtered.Count > 0)
				{
					candidates = filtered;
					break;
				}
			}

			// 使用怪物自己的战斗抽牌随机流（与原版怪物一致，联机同源）
			CardModel? chosen = RunRng.CombatCardGeneration.NextItem(candidates);
			if (chosen == null)
			{
				continue;
			}

			// 抽牌堆属于隐藏信息：丢弃时给出预览，让玩家看清是哪张牌
			CardPileAddResult result = await CardPileCmd.Add(chosen, PileType.Discard);
			CardCmd.PreviewCardPileAdd(result);
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

	/// <inheritdoc />
	/// <remarks>碎岩重击与岩壳防御共用 <c>normal_skill1</c>（同战斗动画），硬化用 <c>normal_skill2</c>。</remarks>
	protected override IReadOnlyList<CznBestiaryAction> BestiaryActions => new[]
	{
		new CznBestiaryAction(GetBestiaryMoveName("ROCK_CRUSH"), "normal_skill1", "skill1"),
		new CznBestiaryAction(GetBestiaryMoveName("ROCK_HARDEN"), "normal_skill2", "skill2"),
		new CznBestiaryAction(GetBestiaryMoveName("ROCK_GUARD"), "normal_skill1", "skill1"),
	};
}
