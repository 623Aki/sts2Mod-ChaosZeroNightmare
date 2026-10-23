using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using CznMonster.Content.Powers;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 美梦（CZN 1004009，代号 hollowfox）：单体双阶段压制型首领，无召唤物。
/// 机制 = 梦魇计数（受击递减 + 每回合偷手牌：阶段 1 = 1 张 / 阶段 2 = 2 张，v1.4 裁定）＋ HP&lt;40% 一次性的阶段切换。
/// 行动总表/摇比/阶段机见 <c>美梦设计文档.md</c> §1.3/§1.4（CZN db 实证）。
/// </summary>
public sealed class SweetDream : MonsterModel
{
	// ── 数值（第 3 章基准，升华值在参数前列；再乘章节系数）──
	// CZN DMG 90 → ×0.15 = 14（升华 -1/段 口径）；用于碎梦爪（3 段）与梦魇碾压（4 段）
	private int ClawDamage => SweetDreamScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 14));

	// CZN DMG 100 → ×0.15 = 15（升华 +1/段）；用于蚀梦刺击（2 段）、千谎之夜（3 段）、涡梦扑杀（2 段）
	private int LieDamage => SweetDreamScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 15));

	/// <inheritdoc />
	/// <remarks>自定义血量只替换基础值，仍乘章节系数（避免开关打开后第 1 章按第 3 章数值出场）。</remarks>
	public override int MinInitialHp
	{
		get
		{
			int baseHp = MonsterSettings.EffectiveCustomBossHp
				?? AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 675, 640);
			return SweetDreamScaling.Hp(baseHp);
		}
	}

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/sweet_dream.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg（设计文档 §4.3）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "attack");

	/// <inheritdoc />
	/// <remarks>CZN 采样 <c>se_1004009_normal_death</c> 由 <c>SfxCmd.PlayDeath</c> 补丁接管。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, "death");

	/// <inheritdoc />
	/// <remarks>3 条 <c>se_1004009_groan_*</c> 随机播放。</remarks>
	public override string? HurtSfx => CznMonsterSfx.Placeholder(this, "hurt");

	/// <inheritdoc />
	/// <remarks>
	/// 登场动画是初始 <see cref="AnimState"/>（引擎没有 "Spawn" 触发器），音效在这里补播；
	/// 同时在这里挂梦魇计数（原生 <see cref="AeonglassBoss"/> 判例：AfterAddedToRoom 里
	/// PowerCmd.Apply，先于玩家回合 1）——玩家裁定①：首回合抽牌后即偷牌，
	/// <c>DREAM_ENTER</c> 占位行动因此退役。
	/// </remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, "enter");
		await ApplyCountdown(SweetDreamCountdownPower.Stage1Countdown);
	}

	/// <summary>梦魇计数剩余（Power.Amount 是单一真值，不做本地字段防漂移）。</summary>
	private int CountdownRemaining => Creature.GetPower<SweetDreamCountdownPower>()?.Amount
		?? SweetDreamCountdownPower.Stage1Countdown;

	/// <summary>当前生命百分比（0..1），阈值 40% 对应 CZN cdp_hp_pct_ff_lt40（只进不出）。</summary>
	private double HpPercent => Creature.GetHpPercentRemaining();

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		// ── 7 个行动（梦魇计数已由 AfterAddedToRoom 于战斗开始挂账，无开场占位行动）──
		MoveState claw = new("CLAW_BARRAGE_MOVE", ClawBarrageMove, new MultiAttackIntent(ClawDamage, 3));
		MoveState lieStrike = new("LIE_STRIKE_MOVE", LieStrikeMove, new MultiAttackIntent(LieDamage, 2), new DebuffIntent());
		MoveState roll = new("NIGHTMARE_ROLL_MOVE", NightmareRollMove, new MultiAttackIntent(ClawDamage, 4), new BuffIntent());
		MoveState awakenLight = new("AWAKEN_LIGHT_MOVE", AwakenLightMove, new BuffIntent());
		MoveState nightLies = new("NIGHT_LIES_MOVE", NightLiesMove, new MultiAttackIntent(LieDamage, 3), new DebuffIntent());
		MoveState lieHeavy = new("LIE_STRIKE_HEAVY_MOVE", LieStrikeHeavyMove, new MultiAttackIntent(LieDamage, 2), new BuffIntent());
		MoveState awakenDeep = new("AWAKEN_DEEP_MOVE", AwakenDeepMove, new BuffIntent());

		// ── 阶段 1 主循环：① 计数归零 → 梦幕重悬（CZN priority 1）
		//                    ② HP<40% → 梦魇碾压 = pt2_00(SET 10) + 首回强制 pt2_10 合并
		//                    ③ 否则摇 pt1 池（monster_pattern_card 70/30）
		RandomBranchState p1Roll = new("P1_ROLL");
		p1Roll.AddBranch(claw, MoveRepeatType.CanRepeatForever, 70f);
		p1Roll.AddBranch(lieStrike, MoveRepeatType.CanRepeatForever, 30f);

		// ── 阶段 2 主循环：① 计数归零 → 深梦苏醒（pt2_13 priority 1）
		//                    ② 否则摇 pt2 池（30 / 40 / 30，pt2_10 因 must_turn=1 已在入场执行）
		RandomBranchState p2Roll = new("P2_ROLL");
		p2Roll.AddBranch(nightLies, MoveRepeatType.CanRepeatForever, 30f);
		p2Roll.AddBranch(lieHeavy, MoveRepeatType.CanRepeatForever, 40f);
		p2Roll.AddBranch(roll, MoveRepeatType.CanRepeatForever, 30f);

		ConditionalBranchState p1 = new("P1_BRANCH");
		p1.AddState(awakenLight, () => CountdownRemaining <= 0);
		p1.AddState(roll, () => HpPercent < 0.4);
		p1.AddState(p1Roll, () => true);

		ConditionalBranchState p2Loop = new("P2_BRANCH");
		p2Loop.AddState(awakenDeep, () => CountdownRemaining <= 0);
		p2Loop.AddState(p2Roll, () => true);

		// ── 连线（连用次数全部无限制，对应 CZN card_use_limit -1）──
		claw.FollowUpState = p1;
		lieStrike.FollowUpState = p1;
		roll.FollowUpState = p2Loop;
		awakenLight.FollowUpState = p1;   // 阶段 1 的重置行动，滚回阶段 1 主循环
		nightLies.FollowUpState = p2Loop;
		lieHeavy.FollowUpState = p2Loop;
		awakenDeep.FollowUpState = p2Loop;

		MonsterState[] states =
		{
			claw, lieStrike, roll, awakenLight, nightLies, lieHeavy, awakenDeep,
			p1, p2Loop, p1Roll, p2Roll,
		};
		return new MonsterMoveStateMachine(states, p1);   // 开场直接进阶段 1 主循环（首动即摇 70/30）
	}

	// ── 行为实现 ──

	/// <summary>碎梦爪（pt1_10）：3×14，多段 job_attack 链（db motion_type = job_attack_1）。</summary>
	private async Task ClawBarrageMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(ClawDamage).WithHitCount(3).FromMonster(this)
			.WithAttackerAnim("AttackHeavy", 0.90f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "job_attack"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
	}

	/// <summary>蚀梦刺击（pt1_11）：2×15 ＋ 目标弱化 +2；多段普通攻击链（db motion_type = attack_1）。</summary>
	private async Task LieStrikeMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(LieDamage).WithHitCount(2).FromMonster(this)
			.WithAttackerAnim("AttackBurst", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "attack"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
	}

	/// <summary>
	/// 梦魇碾压（阶段 2 入场，pt2_00＋pt2_10 合并）：阶段切换播 <c>normal_debuff_unique_1</c>
	/// （db pt2_00 motion_type = debuff_unique_1_1）；计数不重置，直接加层（v1.5 裁定：
	/// 加层 <c>SET 10</c> 层，按 <see cref="SweetDream.Scaled"/> 同一多人缩放公式执行），
	/// 随后 4×14 多段 job_attack 链。
	/// 同时把每回合偷牌量切到阶段 2 口径（v1.4 裁定：阶段 2 每回合偷 2 张）。
	/// </summary>
	private async Task NightmareRollMove(IReadOnlyList<Creature> targets)
	{
		SweetDreamStealRuntime.EnterPhase2();
		await CreatureCmd.TriggerAnim(Creature, "DebuffUnique1", 1.67f);
		CznMonsterSfx.Play(this, "debuff1");
		await AddCountdown(Scaled(SweetDreamCountdownPower.Stage2Countdown));
		await DamageCmd.Attack(ClawDamage).WithHitCount(4).FromMonster(this)
			.WithAttackerAnim("AttackHeavy", 0.90f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "job_attack"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
	}

	/// <summary>梦幕重悬（pt1_12，计数归零的重置行动）：计数重置 7 ＋ 士气 +1；db motion = debuff_unique_2_1。</summary>
	private async Task AwakenLightMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "DebuffUnique2", 1.70f);
		CznMonsterSfx.Play(this, "debuff2");
		await ResetCountdown(SweetDreamCountdownPower.Stage1Countdown);
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
	}

	/// <summary>千谎之夜（pt2_11）：3×15 ＋ 目标弱化 +2 ＋ 目标损伤 +2；多段普通攻击链（db motion_type = attack_1）。</summary>
	private async Task NightLiesMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(LieDamage).WithHitCount(3).FromMonster(this)
			.WithAttackerAnim("AttackBurst", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "attack"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
		await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), targets, 2m, Creature, null);
	}

	/// <summary>涡梦扑杀（pt2_12）：2×15 ＋ 士气 +2；特殊攻击链一气呵成。</summary>
	private async Task LieStrikeHeavyMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(LieDamage).WithHitCount(2).FromMonster(this)
			.WithAttackerAnim("AttackHeavy", 1.00f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "job_attack"))
			.OnlyPlayAnimOnce()
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);
	}

	/// <summary>深梦苏醒（pt2_13，计数归零的重置行动）：计数重置 10 ＋ 士气 +2；db motion = debuff_unique_2_1。</summary>
	private async Task AwakenDeepMove(IReadOnlyList<Creature> targets)
	{
		await CreatureCmd.TriggerAnim(Creature, "DebuffUnique2", 1.70f);
		CznMonsterSfx.Play(this, "debuff2");
		await ResetCountdown(SweetDreamCountdownPower.Stage2Countdown);
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);
	}

	// ── 计数 helper ──

	/// <summary>
	/// 受击一次（由 <see cref="SweetDreamCountdownPower"/> 钩子驱动）：计数 -1；
	/// 归零即时归还全部偷牌（CZN cs02_0250_02，状态触发；重置行动由状态机分支下一次执行）。
	/// </summary>
	public async Task OnCountdownHit()
	{
		SweetDreamCountdownPower? power = Creature.GetPower<SweetDreamCountdownPower>();
		if (power == null)
		{
			return;
		}
		int remaining = Math.Max(0, power.Amount - 1);
		power.SetAmount(remaining);
		if (remaining == 0)
		{
			await SweetDreamStealRuntime.ReturnAll(PileType.Hand);
		}
	}

	/// <summary>挂载梦魇计数 Power（初挂走 <see cref="PowerCmd.Apply"/>，多人缩放由引擎默认公式执行）。</summary>
	private async Task ApplyCountdown(int count)
	{
		await PowerCmd.Apply<SweetDreamCountdownPower>(
			new ThrowingPlayerChoiceContext(), Creature, count, Creature, null);
	}

	/// <summary>重置梦魇计数（苏醒类行动）。多人局按引擎同一公式复算满值，Set 不再二次缩放。</summary>
	private async Task ResetCountdown(int baseValue)
	{
		SweetDreamCountdownPower? power = Creature.GetPower<SweetDreamCountdownPower>();
		if (power == null)
		{
			await ApplyCountdown(baseValue);
			return;
		}
		power.SetAmount(Scaled(baseValue));
	}

	/// <summary>
	/// 在当前计数基础上加层（阶段切换，v1.5 裁定：不重置不吞牌，加 <c>SET 10</c> 层）。
	/// Power 缺失的兜底按满值初挂处理。
	/// </summary>
	private async Task AddCountdown(int delta)
	{
		SweetDreamCountdownPower? power = Creature.GetPower<SweetDreamCountdownPower>();
		if (power == null)
		{
			await ApplyCountdown(SweetDreamCountdownPower.Stage2Countdown);
			return;
		}
		power.SetAmount(power.Amount + Math.Max(0, delta));
	}

	/// <summary>
	/// 引擎多人缩放公式的内联复算（不覆盖 <see cref="PowerModel.GetScaledAmountForMultiplayer"/>，
	/// 玩家裁定口径「不要动引擎默认」）：基础值 × 玩家数 × 章节系数，与 <c>ApplyInternal</c>
	/// 的 <c>(int)amount</c> 截断一致。
	/// </summary>
	private int Scaled(int baseValue)
	{
		ICombatState combatState = Creature.CombatState;
		decimal scale = combatState.Players.Count
			* MultiplayerScalingModel.GetMultiplayerScaling(combatState.Encounter, combatState.RunState.CurrentActIndex);
		return (int)Math.Floor(baseValue * scale);
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

		// 风险 7 的 fallback 口径：AttackBurst 绑 normal_attack_play1 单段（链式 ready→loop 再对测）
		AnimState burst = new("normal_attack_play1") { NextState = idle };

		// 涡梦扑杀：normal_job_attack 是 ready → play → end 三段链
		AnimState jobEnd = new("normal_job_attack_end") { NextState = idle };
		AnimState jobPlay = new("normal_job_attack_play") { NextState = jobEnd };
		AnimState jobReady = new("normal_job_attack_ready") { NextState = jobPlay };

		AnimState debuff1 = new("normal_debuff_unique_1") { NextState = idle };
		AnimState debuff2 = new("normal_debuff_unique_2") { NextState = idle };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("AttackBurst", burst);
		animator.AddAnyState("AttackHeavy", jobReady);
		animator.AddAnyState("DebuffUnique1", debuff1);
		animator.AddAnyState("DebuffUnique2", debuff2);
		// 先加带条件的，再加默认分支（AnimState 按插入顺序取第一个满足条件的分支）
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	protected override bool ShouldShowMoveInBestiary(string moveStateId)
	{
		// 阶段转换/重置类行动不作为图鉴招式展示（同波勒米卡口径）
		return moveStateId != "AWAKEN_LIGHT_MOVE"
			&& moveStateId != "NIGHTMARE_ROLL_MOVE"
			&& moveStateId != "AWAKEN_DEEP_MOVE";
	}

	/// <inheritdoc />
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: new CznBestiaryAction[]
			{
				new(GetBestiaryMoveName("CLAW_BARRAGE"), "normal_job_attack_play", "job_attack"),
				new(GetBestiaryMoveName("LIE_STRIKE"), "normal_attack_play1", "attack"),
				new(GetBestiaryMoveName("NIGHT_LIES"), "normal_attack_play1", "attack"),
				new(GetBestiaryMoveName("LIE_STRIKE_HEAVY"), "normal_job_attack_play", "job_attack"),
			},
			hurtClip: "normal_hit",
			hurtSfx: "hurt",
			dieClip: "normal_death",
			dieSfx: "death");
	}
}
