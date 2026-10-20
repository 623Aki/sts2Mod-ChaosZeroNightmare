using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Powers;
using CznMonster.Content.Vfx;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 波勒米卡（CZN 1001013）：召唤型首领。
/// 前半场「召集 → 冲锋 → 3 拍换防」；HP&lt;50% 进入「甲壳壁垒 → 精锐孵化 → 终局」。
/// 设计细节见 <c>波勒米卡设计文档.md</c>。
/// </summary>
public sealed class Polemica : MonsterModel
{
	// ── 数值（第 3 章基准，升华值在参数前列；再乘章节系数） ──
	private int AttackDamage => PolemicaScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 32, 30));

	private int HeavyDamage => PolemicaScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 26, 24));

	private int BlockAmount => PolemicaScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 50, 45));

	// ── 状态 ──
	private bool _summonedOnce;   // 对应 cs02_0338 ≥ 1：是否已召集过第一波
	private int _brood;           // 对应 cs02_0339：孵化仪式 0..3

	/// <inheritdoc />
	public override int MinInitialHp => MonsterSettings.CustomBossHp ?? PolemicaScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 475, 450));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/polemica.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg（设计文档 §5.4）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "skill1_3");

	/// <inheritdoc />
	/// <remarks>CZN 采样 <c>se_1001013_normal_death</c> 由 <c>SfxCmd.PlayDeath</c> 补丁接管。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, "death");

	/// <inheritdoc />
	/// <remarks>3 条 <c>se_1001013_groan_*</c> 随机播放。</remarks>
	public override string? HurtSfx => CznMonsterSfx.Placeholder(this, "hurt");

	/// <inheritdoc />
	/// <remarks>登场动画是初始 <see cref="AnimState"/>（引擎没有 "Spawn" 触发器），音效在这里补播。</remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, "enter");
	}

	/// <summary>场上存活的召唤物数量（不含自己）。</summary>
	private int LivingAdds => CombatState.GetTeammatesOf(Creature)
		.Count(c => c != Creature && c.IsAlive);

	/// <summary>当前生命百分比（0..1）。</summary>
	private double HpPercent => Creature.GetHpPercentRemaining();

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		// ── 9 个行动 ──
		MoveState broodCall = new("BROOD_CALL_MOVE", BroodCallMove,
			new SummonIntent(), new BuffIntent());
		MoveState legionCharge = new("LEGION_CHARGE_MOVE", LegionChargeMove,
			new SingleAttackIntent(AttackDamage), new BuffIntent());
		MoveState soloStrike = new("SOLO_STRIKE_MOVE", SoloStrikeMove,
			new SingleAttackIntent(HeavyDamage));
		MoveState rebrood = new("REBROOD_MOVE", RebroodMove,
			new SummonIntent(), new BuffIntent());
		MoveState shell = new("SHELL_MOVE", ShellMove,
			new DefendIntent());
		MoveState eliteHatch = new("ELITE_HATCH_MOVE", EliteHatchMove,
			new SummonIntent(), new BuffIntent());
		MoveState eliteRebrood = new("ELITE_REBROOD_MOVE", EliteRebroodMove,
			new SummonIntent(), new BuffIntent());
		MoveState secondWind = new("SECOND_WIND_CHARGE_MOVE", SecondWindChargeMove,
			new SingleAttackIntent(AttackDamage), new BuffIntent());
		MoveState finalStrike = new("FINAL_STRIKE_MOVE", FinalStrikeMove,
			new SingleAttackIntent(HeavyDamage));

		// ── 四个条件分支：先加先判，顺序与原版条件 0→1→2 一致 ──
		ConditionalBranchState p1 = new("P1_BRANCH");
		p1.AddState(soloStrike, () => _summonedOnce && LivingAdds == 0);
		p1.AddState(rebrood, () => _brood >= PolemicaBroodPower.Threshold);
		p1.AddState(shell, () => HpPercent < 0.5);
		p1.AddState(legionCharge, () => true);

		ConditionalBranchState p3 = new("P3_BRANCH");
		p3.AddState(rebrood, () => _brood >= PolemicaBroodPower.Threshold);
		p3.AddState(broodCall, () => LivingAdds >= 2);
		p3.AddState(shell, () => HpPercent < 0.5);
		p3.AddState(soloStrike, () => true);

		ConditionalBranchState p5 = new("P5_BRANCH");
		p5.AddState(finalStrike, () => LivingAdds == 0);
		p5.AddState(eliteRebrood, () => _brood >= PolemicaBroodPower.Threshold);
		p5.AddState(secondWind, () => true);

		ConditionalBranchState p7 = new("P7_BRANCH");
		p7.AddState(eliteRebrood, () => _brood >= PolemicaBroodPower.Threshold);
		p7.AddState(secondWind, () => LivingAdds >= 2);
		p7.AddState(finalStrike, () => true);

		// ── 连线 ──
		broodCall.FollowUpState = p1;
		legionCharge.FollowUpState = p1;
		soloStrike.FollowUpState = p3;
		rebrood.FollowUpState = p1;
		shell.FollowUpState = eliteHatch;          // P4 只停 1 回合
		eliteHatch.FollowUpState = p5;
		secondWind.FollowUpState = p5;
		eliteRebrood.FollowUpState = p5;
		finalStrike.FollowUpState = p7;

		MonsterState[] states =
		{
			broodCall, legionCharge, soloStrike, rebrood, shell,
			eliteHatch, eliteRebrood, secondWind, finalStrike,
			p1, p3, p5, p7,
		};
		return new MonsterMoveStateMachine(states, broodCall);   // 开场：军团召集
	}

	// ── 行为实现 ──

	/// <summary>军团召集：补满第一波（兵蚁×2 + 盾蚁×1），仪式 +1。</summary>
	private async Task BroodCallMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "Summon", 2.43f);
		CznMonsterSfx.Play(this, "skill2_3");
		await SummonWave1();
		_summonedOnce = true;
		await AddBrood(1);
	}

	/// <summary>军团冲锋：200% 伤害 + 全体己方 +1 力量，仪式 +1。</summary>
	private async Task LegionChargeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(AttackDamage).FromMonster(this)
			.WithAttackerAnim("AttackHeavy", 1.70f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1_3"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), CombatState.GetTeammatesOf(Creature), 1m, Creature, null);
		await AddBrood(1);
	}

	/// <summary>首领突袭：160% 伤害，仪式 +1。</summary>
	private async Task SoloStrikeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(HeavyDamage).FromMonster(this)
			.WithAttackerAnim("AttackFast", 1.03f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill4"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		await AddBrood(1);
	}

	/// <summary>全军换防：处决全部召唤物、按处决数获得力量、重召第一波、仪式清零。</summary>
	private async Task RebroodMove(IReadOnlyList<Creature> targets)
	{
		int executedCount = await ExecuteAdds();
		if (executedCount > 0)
		{
			await PowerCmd.Apply<StrengthPower>(
				new ThrowingPlayerChoiceContext(), Creature, 2m * executedCount, Creature, null);
		}
		await ResetBrood();
		await SummonWave1();
	}

	/// <summary>甲壳壁垒：处决全部召唤物、45 格挡、仪式清零；下一回合进入 P5。</summary>
	private async Task ShellMove(IReadOnlyList<Creature> targets)
	{
		// 同一回合还要播 Shell，不再叠加全屏处决特效（§5.5，避免 4 秒双演出）
		await ExecuteAdds(playVfx: false);
		await ResetBrood();
		await CreatureCmd.TriggerAnim(Creature, "Shell", 2.60f);
		CznMonsterSfx.Play(this, "skill3");
		await CreatureCmd.GainBlock(Creature, BlockAmount, ValueProp.Move, null);
	}

	/// <summary>精锐孵化：补满第二波（岩壳虫 + 翼壳虫 + 壳虫），仪式 +1。</summary>
	private async Task EliteHatchMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "Summon", 2.43f);
		CznMonsterSfx.Play(this, "skill2_3");
		await SummonWave2();
		await AddBrood(1);
	}

	/// <summary>精锐换防：处决全部召唤物、按处决数获得力量、重召第二波、仪式清零。</summary>
	private async Task EliteRebroodMove(IReadOnlyList<Creature> targets)
	{
		int executedCount = await ExecuteAdds();
		if (executedCount > 0)
		{
			await PowerCmd.Apply<StrengthPower>(
				new ThrowingPlayerChoiceContext(), Creature, 2m * executedCount, Creature, null);
		}
		await ResetBrood();
		await SummonWave2();
	}

	/// <summary>垂死冲锋：200% 伤害 + 全体己方 +1 力量，仪式 +1。</summary>
	private async Task SecondWindChargeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(AttackDamage).FromMonster(this)
			.WithAttackerAnim("AttackHeavy", 1.70f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill1_3"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);

		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), CombatState.GetTeammatesOf(Creature), 1m, Creature, null);
		await AddBrood(1);
	}

	/// <summary>终局突刺：160% 伤害，仪式 +1。</summary>
	private async Task FinalStrikeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(HeavyDamage).FromMonster(this)
			.WithAttackerAnim("AttackFast", 1.03f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "skill4"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		await AddBrood(1);
	}

	// ── 召唤 / 处决 / 计数 helper ──

	/// <summary>第一波：1001009 ×2 + 1001010 ×1（只填空槽）。</summary>
	private async Task SummonWave1()
	{
		await SummonOne<PolemicaLegionSoldier>();
		await SummonOne<PolemicaLegionShield>();
		await SummonOne<PolemicaLegionSoldier>();
	}

	/// <summary>第二波：1001019 + 1001018 + 1001004（只填空槽）。</summary>
	private async Task SummonWave2()
	{
		await SummonOne<PolemicaRockShellInsect>();
		await SummonOne<PolemicaWingShellInsect>();
		await SummonOne<PolemicaShellInsect>();
	}

	/// <summary>
	/// 找一个空槽。不能直接用 Encounter.GetNextSlot：它只比对 SlotName、不看 IsAlive，
	/// 行动计数中断在玩家回合内时，刚死但尚未清理的召唤物会占着槽位。
	/// </summary>
	private string? FindFreeSlot()
	{
		EncounterModel? encounter = CombatState.Encounter;
		if (encounter == null)
		{
			return null;
		}
		foreach (string slot in encounter.Slots)
		{
			bool occupied = CombatState.Enemies.Any(c => c.SlotName == slot && c.IsAlive);
			if (!occupied)
			{
				return slot;
			}
		}
		return null;
	}

	/// <summary>召唤一只到下一个空槽；槽位满则跳过。</summary>
	private async Task SummonOne<T>() where T : MonsterModel
	{
		string? slot = FindFreeSlot();
		if (string.IsNullOrEmpty(slot))
		{
			return;
		}

		// MinionPower 由 PolemicaAddMonster.AfterAddedToRoom 统一挂载，这里不重复添加
		await CreatureCmd.Add<T>(CombatState, slot);
	}

	/// <summary>
	/// 处决全部召唤物（原版 KILL ALL_ENEMY_EXCEPT_CASTER），返回本次处决数量。
	/// </summary>
	/// <param name="playVfx">是否叠加 §5.5 的全屏三层处决特效（SHELL 回合传 false）。</param>
	private async Task<int> ExecuteAdds(bool playVfx = true)
	{
		List<Creature> adds = CombatState.GetTeammatesOf(Creature)
			.Where(c => c != Creature && c.IsAlive)
			.ToList();
		if (adds.Count == 0)
		{
			return 0;
		}

		if (playVfx)
		{
			PolemicaFatalVfx.Play(Creature);
		}

		// 首领与全部召唤物同时起手：处决是「一拍」，不是逐个播。
		CznMonsterSfx.Play(this, "fatal");
		Task bossAnim = CreatureCmd.TriggerAnim(Creature, "Fatal", 1.5f);

		List<Task> addAnims = new(adds.Count);
		foreach (Creature add in adds)
		{
			if (add.Monster is not PolemicaAddMonster model)
			{
				continue;
			}
			if (model.ExecuteSfxClip is { } executeClip)
			{
				CznMonsterSfx.Play(model, executeClip);
			}
			addAnims.Add(CreatureCmd.TriggerAnim(add, model.ExecuteAnimTrigger, model.ExecuteAnimWait));
		}

		await Task.WhenAll(addAnims);
		await bossAnim;
		await CreatureCmd.Kill(adds, force: true);

		// 不要在这里再手动 CombatState.RemoveCreature(add)。
		// CreatureCmd.Kill 已经会按原生流程移除战斗状态并异步播放死亡动画/释放节点；
		// 提前摘掉 CombatState 会让死亡节点的 HoverTips / 后续清理访问空 CombatState，
		// 表现为死亡贴图不消失、悬停/聚焦时刷 NullReferenceException。
		// FindFreeSlot 只把“存活且同名槽位”视为占用，尸体不会挡住下一波召唤。
		return adds.Count;
	}

	/// <summary>仪式 +N（上限 3），同步显示 Power。</summary>
	private async Task AddBrood(int amount)
	{
		_brood = Math.Min(PolemicaBroodPower.Threshold, _brood + amount);

		PolemicaBroodPower? power = Creature.GetPower<PolemicaBroodPower>();
		if (power == null)
		{
			await PowerCmd.Apply<PolemicaBroodPower>(
				new ThrowingPlayerChoiceContext(), Creature, _brood, Creature, null);
		}
		else
		{
			power.SetAmount(_brood);
		}
	}

	/// <summary>仪式清零（换防 / 护盾）。</summary>
	private async Task ResetBrood()
	{
		_brood = 0;
		await PowerCmd.Remove<PolemicaBroodPower>(Creature);
	}

	/// <inheritdoc />
	/// <remarks>CZN 的待机动画叫 <c>normal_idle</c>，与游戏默认的 <c>idle_loop</c> 不同，必须 override。</remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		// 引擎从不发 "Spawn" 触发器：登场动画必须是初始状态（同原生 Parafright / GasBomb）。
		AnimState enter = new("normal_enter") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState dead = new("normal_death");
		AnimState heavy = new("normal_skill1_3") { NextState = idle };
		AnimState fast = new("normal_skill4") { NextState = idle };
		AnimState summon = new("normal_skill2_3") { NextState = idle };
		AnimState shell = new("normal_skill3") { NextState = idle };
		AnimState fatal = new("normal_fatal") { NextState = idle };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("AttackHeavy", heavy);
		animator.AddAnyState("AttackFast", fast);
		animator.AddAnyState("Summon", summon);
		animator.AddAnyState("Shell", shell);
		animator.AddAnyState("Fatal", fatal);
		// 先加带条件的，再加默认分支（AnimState 按插入顺序取第一个满足条件的分支）
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	protected override bool ShouldShowMoveInBestiary(string moveStateId)
	{
		// 换防与护盾是阶段转换，不作为图鉴招式展示
		return moveStateId != "REBROOD_MOVE"
			&& moveStateId != "ELITE_REBROOD_MOVE"
			&& moveStateId != "SHELL_MOVE";
	}
}
