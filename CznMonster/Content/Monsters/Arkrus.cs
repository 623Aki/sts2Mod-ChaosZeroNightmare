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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿克鲁斯（CZN 1003016）：塞拉菲翁的持盾同伴（塞拉菲翁设计文档 §4.5 / §4.7）。
///
/// <para>守护二拍定式（庇护 → 守护突进循环）与救援三拍（救援突刺 → 救援咒缚 → 救援颂歌）
/// 都是严格的三拍 / 二拍固定序列，中间不回分支；救援三拍由「塞拉菲翁处于濒死」入边启动。</para>
///
/// <para>伴生死亡不在本类实现：游戏侧没有 <c>MonsterModel.OnAfterDeath</c> 钩子，
/// 由 <see cref="SeraphionHalfDeathPower.AfterDeath"/>（hook listener）监听阿克鲁斯的死亡完成。</para>
/// </summary>
public sealed class Arkrus : MonsterModel
{
	// ── 数值（第 3 章基准，升华值在前；再乘章节系数，见文档 §8.2） ──

	/// <summary>庇护：己方全体 +22 格挡。</summary>
	private int BlockAmount => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 25, 22));

	/// <summary>救援突刺后自盾（同复归附盾档）。</summary>
	private int BigBlock => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 50, 45));

	/// <summary>守护突进：3×12。</summary>
	private int AssaultDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <summary>救援突刺：12。</summary>
	private int StrikeDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <summary>救援咒缚：玩家虚弱 2 / 脆弱 2 / 力量 −1（升华不减）。</summary>
	private const int RescueDebuffAmount = 2;

	// ── 状态 ──
	private bool _rescue;    // 救援模式（切形态动画的判断依据）
	private int _guardTurn;  // 守护二拍游标

	/// <summary>当前是否处于救援形态（图鉴外的表现字段；濒死死亡动画分支读）。</summary>
	public bool InRescueForm => _rescue;

	/// <inheritdoc />
	public override int MinInitialHp => SeraphionScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 160, 152));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/arkrus.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg（设计文档 §5.4）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "rescuestrike");

	/// <inheritdoc />
	/// <remarks>
	/// bank 里没有区分形态的死亡采样以外的独立条目：普通死 <c>normal_death</c>、
	/// 救援形态死 <c>stance1_death</c>（两个采样条目都在表里，按形态走 <see cref="DeathSfx"/>）。
	/// </remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, _rescue ? "death_rescue" : "death");

	/// <summary>登场音效（登场动画是初始 AnimState，同塞拉菲翁）。</summary>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, "enter");
	}

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		// ── 5 个行动（文档 §4.5 行动总表） ──
		MoveState ward = new("GUARD_WARD_MOVE", GuardWardMove,
			new DefendIntent(), new BuffIntent());
		MoveState assault = new("GUARD_ASSAULT_MOVE", GuardAssaultMove,
			new MultiAttackIntent(AssaultDamage, 3));
		MoveState strike = new("RESCUE_STRIKE_MOVE", RescueStrikeMove,
			new SingleAttackIntent(StrikeDamage), new DefendIntent());
		MoveState curse = new("RESCUE_CURSE_MOVE", RescueCurseMove,
			new DebuffIntent());
		MoveState song = new("RESCUE_SONG_MOVE", RescueSongMove,
			new HealIntent());

		// ── 守护分支：先加先判（救援 → 二拍循环） ──
		ConditionalBranchState guard = new("GUARD_BRANCH");
		guard.AddState(strike, () => SeraphionHalfState.IsHalfDeath(CombatState));
		guard.AddState(ward, () => _guardTurn % 2 == 0);
		guard.AddState(assault, () => true);

		// ── 连线：救援三拍严格串行；守护二拍严格串行 ──
		strike.FollowUpState = curse;
		_strikeState = strike;
		curse.FollowUpState = song;
		song.FollowUpState = guard;      // 颂歌完成 → 回守护定式
		ward.FollowUpState = assault;    // 二拍定式：庇护 → 突进
		assault.FollowUpState = ward;

		MonsterState[] states =
		{
			ward, assault, strike, curse, song, guard,
		};
		return new MonsterMoveStateMachine(states, guard);   // 开场进守护分支
	}

	// ── 行为实现 ──

	/// <summary>庇护：己方全体 +22 格挡、+1 力量（normal_buff_1，1.67s）。</summary>
	private async Task GuardWardMove(IReadOnlyList<Creature> targets)
	{
		await MaybeExitRescueForm();
		CznMonsterSfx.Play(this, "ward");
		await CreatureCmd.TriggerAnim(Creature, "GuardWard", 1.67f);

		List<Creature> team = CombatState.GetTeammatesOf(Creature)
			.Where(c => c.IsAlive)
			.ToList();
		foreach (Creature mate in team)
		{
			await CreatureCmd.GainBlock(mate, BlockAmount, ValueProp.Move, null);
		}
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), team, 1m, Creature, null);
		_guardTurn = 0;
	}

	/// <summary>守护突进：3×12（normal_attack_play1，0.67s 每跳）。</summary>
	private async Task GuardAssaultMove(IReadOnlyList<Creature> targets)
	{
		await MaybeExitRescueForm();
		await DamageCmd.Attack(AssaultDamage).WithHitCount(3).FromMonster(this)
			.WithAttackerAnim("Assault", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "assault"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		_guardTurn++;
	}

	/// <summary>
	/// 救援突刺（救援第 1 拍）：入救援形态（normal_to_stance1，1.0s）→ 突刺 12（stance1_m_attack_01，
	/// 1.83s）→ 自盾 45。救援模式启动只认「塞拉菲翁处于濒死」。
	/// </summary>
	private async Task RescueStrikeMove(IReadOnlyList<Creature> targets)
	{
		if (!_rescue)
		{
			_rescue = true;
			CznMonsterSfx.Play(this, "torescue");
			await CreatureCmd.TriggerAnim(Creature, "ToRescue", 1.00f);
		}

		await DamageCmd.Attack(StrikeDamage).FromMonster(this)
			.WithAttackerAnim("RescueStrike", 1.83f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "rescuestrike"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		await CreatureCmd.GainBlock(Creature, BigBlock, ValueProp.Move, null);
	}

	/// <summary>救援咒缚（救援第 2 拍）：玩家全体虚弱 2 / 脆弱 2 / 力量 −1（stance1_debuff_1，1.97s）。</summary>
	private async Task RescueCurseMove(IReadOnlyList<Creature> targets)
	{
		CznMonsterSfx.Play(this, "rescuecurse");
		await CreatureCmd.TriggerAnim(Creature, "RescueCurse", 1.97f);

		List<Creature> players = CombatState.PlayerCreatures.Where(c => c.IsAlive).ToList();
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(), players, RescueDebuffAmount, Creature, null);
		await PowerCmd.Apply<FrailPower>(
			new ThrowingPlayerChoiceContext(), players, RescueDebuffAmount, Creature, null);
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), players, -1m, Creature, null);
	}

	/// <summary>
	/// 救援颂歌（救援第 3 拍，文档 §4.7 / T8）：治疗塞拉菲翁 50% 最大生命 → 解濒死 Power →
	/// 置复归拍标志（stance1_unique_1，2.03s）。
	/// </summary>
	private async Task RescueSongMove(IReadOnlyList<Creature> targets)
	{
		CznMonsterSfx.Play(this, "rescuesong");
		await CreatureCmd.TriggerAnim(Creature, "RescueSong", 2.03f);

		Creature? seraphion = SeraphionHalfState.FindSeraphionCreature(CombatState);
		if (seraphion != null && SeraphionHalfState.IsHalfDeath(CombatState))
		{
			await SeraphionHalfState.BeginRevivalAsync(seraphion);
		}

		_rescue = false;   // 歌毕回守护定式（下拍播 stance1_to_normal）
	}

	/// <summary>救援形态收尾（回守护定式的下个行动前缀）：stance1_to_normal（1.0s）。</summary>
	private async Task MaybeExitRescueForm()
	{
		if (!_rescue)
		{
			return;
		}

		_rescue = false;
		CznMonsterSfx.Play(this, "toguard");
		await CreatureCmd.TriggerAnim(Creature, "ToGuard", 1.00f);
	}

	// RESCUE_STRIKE 行动状态引用（GenerateMoveStateMachine 时登记，ForceRescueMove 里用）
	private MoveState? _strikeState;

	/// <summary>
	/// 塞拉菲翁进入濒死的当事钩子（<see cref="SeraphionHalfState.NotifyArkrusRescue"/> 唯一调用方）：
	/// 立刻把下一次行动改写为救援突刺并刷新意图显示。
	/// 引擎只在玩家回合开始 RollMove（CombatManager.cs:743），濒死发生在玩家回合中间——
	/// 不即时改写的话本拍会沿用已掷好的 P1 行动（救援滞后一拍；CZN 原版条件每拍当场判）。
	/// 已在救援链 / 不在场时跳过。
	/// </summary>
	public void ForceRescueMove()
	{
		if (_strikeState == null || _rescue || !Creature.IsAlive)
		{
			return;
		}

		SetMoveImmediate(_strikeState, forceTransition: true);
	}

	// ── 动画（文档 §5.1 映射表） ──

	/// <inheritdoc />
	/// <remarks>CZN 的待机动画叫 <c>normal_idle</c>，与游戏默认的 <c>idle_loop</c> 不同，必须 override。</remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		// 引擎从不发 "Spawn" 触发器：登场动画必须是初始状态（同原生 Parafright / GasBomb）。
		AnimState enter = new("normal_enter") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState deadNormal = new("normal_death");
		AnimState ward = new("normal_buff_1") { NextState = idle };
		AnimState assault = new("normal_attack_play1") { NextState = idle };
		AnimState toRescue = new("normal_to_stance1") { NextState = idle };
		AnimState rescueStrike = new("stance1_m_attack_01") { NextState = idle };
		AnimState rescueCurse = new("stance1_debuff_1") { NextState = idle };
		AnimState rescueSong = new("stance1_unique_1") { NextState = idle };
		AnimState toGuard = new("stance1_to_normal") { NextState = idle };
		AnimState deadRescue = new("stance1_death");

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("GuardWard", ward);
		animator.AddAnyState("Assault", assault);
		animator.AddAnyState("ToRescue", toRescue);
		animator.AddAnyState("RescueStrike", rescueStrike);
		animator.AddAnyState("RescueCurse", rescueCurse);
		animator.AddAnyState("RescueSong", rescueSong);
		animator.AddAnyState("ToGuard", toGuard);
		// 先加带条件的，再加默认分支（AnimState 按插入顺序取第一个满足条件的分支）
		animator.AddAnyState("Dead", deadRescue, () => _rescue);
		animator.AddAnyState("Dead", deadNormal);
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>
	/// 图鉴展示守护定式 + 救援三拍：救援三拍本来就是 <c>stance1_*</c> clip，可直接单播。
	/// 死亡用常态骨架的 <c>normal_death</c>（救援形态死亡 <c>stance1_death</c> 需要「正在救援」这个
	/// 战斗内状态才能触发，图鉴里不存在该前置，故不单列）。
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: new CznBestiaryAction[]
			{
				new(GetBestiaryMoveName("GUARD_WARD"), "normal_buff_1", "ward"),
				new(GetBestiaryMoveName("GUARD_ASSAULT"), "normal_attack_play1", "assault"),
				new(GetBestiaryMoveName("RESCUE_STRIKE"), "stance1_m_attack_01", "rescuestrike"),
				new(GetBestiaryMoveName("RESCUE_CURSE"), "stance1_debuff_1", "rescuecurse"),
				new(GetBestiaryMoveName("RESCUE_SONG"), "stance1_unique_1", "rescuesong"),
			},
			hurtClip: "normal_hit",
			hurtSfx: null,
			dieClip: "normal_death",
			dieSfx: "death");
	}
}
