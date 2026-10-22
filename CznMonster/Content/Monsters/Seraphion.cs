using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using CznMonster.Content.Powers;
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
using CznMonster.Settings;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 塞拉菲翁（CZN 1003017）：持盾主之双怪组的首领（塞拉菲翁设计文档 §4）。
///
/// <para>阶段流转（v1.1）：</para>
/// <list type="bullet">
/// <item>登场（<c>UPRISING</c>，不进图鉴）→ P1 三拍循环（双牙×2 → 终焉回旋）；</item>
/// <item>同伴（阿克鲁斯）全灭 → 光耀增幅（力量 +2）→ 终焉四连循环到底；</item>
/// <item>被打到 0 HP：由 <see cref="SeraphionHalfDeathPower"/> 拦截至 1 HP →
/// 濒死（<c>FADING</c>，不行动 + 免疫），等待救援颂歌；</item>
/// <item>复归拍（<c>REVIVAL</c>）：起身 → 切持盾形态 → 附盾 → 回稳三连，之后回 P1 循环。</item>
/// </list>
///
/// <para><b>复归入边为何只看 <c>_revivePending</c></b>（设计文档附注里「revivePending ∥ Block&gt;0」的落地修正）：
/// 引擎在<b>玩家回合开始</b>就为敌方掷下一个行动（<c>CombatManager.cs:743 PrepareForNextTurn</c>），
/// 而敌方格挡要等到<b>敌方回合开始</b>才清零（<c>Creature.cs:696 AfterTurnStart → ClearBlock</c>）。
/// 也就是说 RollMove 时上一拍的盾还在——若把 Block&gt;0 当入边，复归拍会无限自循环。
/// 文档 §9 T4 的验收要求（「塞拉菲翁的 22 盾在其自身回合开始清零，不触发复归拍」）也否定了该入边，
/// 实践中复归只由救援颂歌解锁。</para>
/// </summary>
public sealed class Seraphion : MonsterModel
{
	// ── 数值（第 3 章基准，升华值在前；再乘章节系数，见文档 §8.1） ──

	/// <summary>双牙撕咬 / 回稳三连：2×12 / 3×12。</summary>
	private int BiteDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <summary>终焉回旋（P1 第 3 拍）：3×10。</summary>
	private int CycloneDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 11, 10));

	/// <summary>回稳三连（复归拍）：3×12。</summary>
	private int ReviveDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <summary>终焉四连（终局每拍）：4×12。</summary>
	private int QuadDamage => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 12));

	/// <summary>复活附盾（发明值 = 本组 300 盾档 ×0.15；文档附注置信度低）。</summary>
	private int ReviveBlock => SeraphionScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 50, 45));

	/// <summary>光耀增幅：力量 +2（CZN cs00_0005 未解，映射裁定）。</summary>
	private const int FinaleStrengthGain = 2;

	// ── 状态 ──
	private int _p1Turn;          // P1 三拍游标（0..2）；复归完成归 0
	private bool _p2Form;         // 复归拍后处于持盾形态；下个 P1/终局拍先播 stance1_to_normal 退出
	private bool _fading;         // 濒死中（Power 的垂死免疫 / 死亡动画分支都读它）
	private bool _revivePending;  // 救援颂歌解锁 → 下一拍 REVIVAL

	/// <summary>濒死 Power 是否已挂（阿克鲁斯存活期间恒真，救援后解除）。</summary>
	public bool HalfDeathActive => Creature.GetPower<SeraphionHalfDeathPower>() != null;

	/// <summary>濒死中（垂死免疫生效开关；<see cref="SeraphionHalfDeathPower"/> 读）。</summary>
	public bool FadingActive => _fading;

	/// <summary>救援颂歌写入点（<see cref="SeraphionHalfState.BeginRevivalAsync"/> 唯一调用方）：
	/// 置复归标志 + 当场把下一次行动改为 REVIVAL 复归拍（意图立刻显示 Attack+Defend，不等待下一掷）。
	/// 安全性：下一掷从 REVIVAL 的出边走会进 P1 分支，其首谓词 <c>revivePending</c> 此时仍真 →
	/// 重新掷回 REVIVAL，复归拍不会被跳过；RevivalMove 消费标志后才回 P1 正常循环。</summary>
	internal void RequestRevival()
	{
		_revivePending = true;
		_revivalQueuedRound = CombatState?.RoundNumber ?? -1;
		if (_revivalState != null)
		{
			SetMoveImmediate(_revivalState, forceTransition: true);
		}
	}

	/// <summary>
	/// 进入濒死：<see cref="SeraphionHalfDeathPower.AfterPreventingDeath"/> 唯一调用方。
	/// 立刻把行动切到 <c>FADING</c>（意图改成 Unknown），当前如果包在旧行动里也直接覆盖。
	/// </summary>
	internal void EnterFading()
	{
		if (_fading)
		{
			return;
		}

		_fading = true;
		_p1Turn = 0;
		_p2Form = false;
		if (_fadingState != null)
		{
			SetMoveImmediate(_fadingState, forceTransition: true);
		}

		// 事件通知：阿克鲁斯当场把下一次行动改写为救援突刺（救援不滞后一拍）
		SeraphionHalfState.NotifyArkrusRescue(CombatState);
	}

	/// <summary>
	/// 退出濒死（<see cref="SeraphionHalfState.BeginRevivalAsync"/> 唯一调用方）：颂歌治疗把血量
	/// 抬回正值的当场解除濒死——对齐原数据「cur_hp≥1 → P1、濒死怪数量归 0」的时点；
	/// 死亡免疫同时停止（原版此时已可正常受击）。起身演出仍保留在复归拍。
	/// </summary>
	internal void ExitFading()
	{
		_fading = false;
	}

	// FADING 行动状态引用（GenerateMoveStateMachine 时登记，EnterFading 里用）
	private MoveState? _fadingState;

	// REVIVAL 复归拍行动状态引用（GenerateMoveStateMachine 时登记，RequestRevival 里用）
	private MoveState? _revivalState;

	// 复归拍排入时的回合号：颂歌（add1 先手）与塞拉菲翁行动在同一敌方回合内时，
	// SetMoveImmediate 强改的 NextMove 会被本回合的 PerformMove 立即执行（=起身当拍就攻击）。
	// 同回合执行时本拍退化为占位（意图保持 REVIVAL 显示），复归内容留到自己下一拍。
	private int _revivalQueuedRound = -1;

	/// <inheritdoc />
	/// <remarks>血量覆盖（上游契约 §6.4）：boss 血量自定义设置只作用于本组的主敌。</remarks>
	public override int MinInitialHp => MonsterSettings.CustomBossHp ?? SeraphionScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 206, 195));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/seraphion.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg（设计文档 §5.4）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "bite");

	/// <inheritdoc />
	/// <remarks>
	/// 濒死形态下的真死用 <c>stance3_death</c> 采样；普通死亡用 <c>normal_death</c>（T9）。
	/// </remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, _fading ? "death_fading" : "death");

	/// <inheritdoc />
	/// <remarks>受击 <c>se_1003017_normal_hit</c>。</remarks>
	public override string? HurtSfx => CznMonsterSfx.Placeholder(this, "hurt");

	/// <inheritdoc />
	/// <remarks>登场动画是初始 <see cref="AnimState"/>（引擎没有 "Spawn" 触发器），音效在这里补播；濒死 Power 在这里挂载。</remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, "enter");
		if (CombatState.GetTeammatesOf(Creature).Any(c => c.Monster is Arkrus))
		{
			await PowerCmd.Apply<SeraphionHalfDeathPower>(
				new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
		}
	}

	/// <summary>场上存活的同伴（阿克鲁斯后者其它增援）数量，不含自己。</summary>
	private int LivingAdds => CombatState.GetTeammatesOf(Creature)
		.Count(c => c != Creature && c.IsAlive);

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		// ── 7 个行动（文档 §4.4 行动总表） ──
		// 登场拍不显示意图图标（HiddenIntent 无图标无提示，避免头顶出现无意义的「?」）
		MoveState uprising = new("SOVEREIGN_UPRISING_MOVE", UprisingMove,
			new HiddenIntent());
		MoveState bite = new("SOVEREIGN_BITE_MOVE", BiteMove,
			new MultiAttackIntent(BiteDamage, 2));
		MoveState cyclone = new("FINALE_CYCLONE_MOVE", CycloneMove,
			new MultiAttackIntent(CycloneDamage, 3));
		MoveState revival = new("REVIVAL_MOVE", RevivalMove,
			new MultiAttackIntent(ReviveDamage, 3), new DefendIntent());
		MoveState finaleOpen = new("FINALE_OPEN_MOVE", FinaleOpenMove,
			new BuffIntent());
		MoveState finaleLoop = new("FINALE_LOOP_MOVE", FinaleLoopMove,
			new MultiAttackIntent(QuadDamage, 4));
		// 濒死不显示意图图标（HiddenIntent；用户裁定 2026-10-21——垂死不动即无意图）
		MoveState fading = new("FADING_MOVE", FadingMove,
			new HiddenIntent());
		_fadingState = fading;

		// ── P1 分支：先加先判（复归 → 终局 → 三拍循环） ──
		ConditionalBranchState p1 = new("P1_BRANCH");
		p1.AddState(revival, () => _revivePending);
		p1.AddState(finaleOpen, () => LivingAdds == 0);
		p1.AddState(cyclone, () => _p1Turn >= 2);
		p1.AddState(bite, () => true);

		// ── 连线 ──
		uprising.FollowUpState = p1;
		_revivalState = revival;
		bite.FollowUpState = p1;
		cyclone.FollowUpState = p1;
		revival.FollowUpState = p1;
		finaleOpen.FollowUpState = finaleLoop;
		finaleLoop.FollowUpState = finaleLoop;   // 终局无出口（同伴不再入场）
		// 濒死自环：救援颂歌置 RevivePending 前一直原地；置位后下一拍走 REVIVAL 复归拍。
		// （修复：原写法 fading 直接自环无出口，复归拍永远够不到 → 复归后意图一直是「?」）
		ConditionalBranchState fadingLoop = new("FADING_BRANCH");
		fadingLoop.AddState(revival, () => _revivePending);
		fadingLoop.AddState(fading, () => true);
		fading.FollowUpState = fadingLoop;

		MonsterState[] states =
		{
			uprising, bite, cyclone, revival, finaleOpen, finaleLoop, fading, fadingLoop, p1,
		};
		return new MonsterMoveStateMachine(states, uprising);   // 开场：主之威仪
	}

	// ── 行为实现 ──

	/// <summary>登场：动画 normal_enter（2.4s）是初始 AnimState 已经在播，此地只等它收尾。</summary>
	private async Task UprisingMove(IReadOnlyList<Creature> targets)
	{
		await Cmd.CustomScaledWait(1.2f, 1.6f);
	}

	/// <summary>双牙撕咬：2×12。</summary>
	private async Task BiteMove(IReadOnlyList<Creature> targets)
	{
		await ExitP2FormIfNeeded();
		await DamageCmd.Attack(BiteDamage).WithHitCount(2).FromMonster(this)
			.WithAttackerAnim("Bite", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "bite"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		_p1Turn++;
	}

	/// <summary>终焉回旋（P1 第 3 拍）：3×10，之后三拍循环从头开始。</summary>
	private async Task CycloneMove(IReadOnlyList<Creature> targets)
	{
		await ExitP2FormIfNeeded();
		await DamageCmd.Attack(CycloneDamage).WithHitCount(3).FromMonster(this)
			.WithAttackerAnim("Cyclone", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "cyclone"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		_p1Turn = 0;
	}

	/// <summary>
	/// 复归拍（文档 §4.7 / T8）：切持盾形态（normal_to_stance1）→ 附盾 → 回稳三连 3×12。
	/// 起身（stance3_to_normal + revival 采样）已提前到救援颂歌当场播（v1.2 用户裁定：
	/// 复活即起身 + 意图立刻显示），本拍从切形态开始。
	/// </summary>
	private async Task RevivalMove(IReadOnlyList<Creature> targets)
	{
		// 颂歌同回合排入（add1 先手时本拍会在塞拉菲翁行动前被强改）：
		// 本拍只占位（起身 + 意图已在颂歌拍演出），复归内容留到自己下一拍执行
		if (_revivalQueuedRound == (CombatState?.RoundNumber ?? -1))
		{
			_revivalQueuedRound = -1;
			await Cmd.CustomScaledWait(0.1f, 0.2f);
			return;
		}
		_revivalQueuedRound = -1;

		_revivePending = false;
		_p1Turn = 0;
		_fading = false;
		_p2Form = true;

		// ① 切持盾形态：normal_to_stance1（1.0s）
		CznMonsterSfx.Play(this, "toshield");
		await CreatureCmd.TriggerAnim(Creature, "ToShieldForm", 1.00f);

		// ② 复活附盾
		await CreatureCmd.GainBlock(Creature, ReviveBlock, ValueProp.Move, null);

		// ③ 回稳三连：stance1_attack_play1（0.67s，每跳一循环）×3
		await DamageCmd.Attack(ReviveDamage).WithHitCount(3).FromMonster(this)
			.WithAttackerAnim("ShieldStance", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "shieldstance"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
	}

	/// <summary>光耀增幅：播 stance2 起手 buff（stance2_buff_1，1.5s），力量 +2，之后终局四连循环。</summary>
	private async Task FinaleOpenMove(IReadOnlyList<Creature> targets)
	{
		await ExitP2FormIfNeeded();
		CznMonsterSfx.Play(this, "finalebuff");
		await CreatureCmd.TriggerAnim(Creature, "FinaleBuff", 1.50f);
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), Creature, FinaleStrengthGain, Creature, null);
	}

	/// <summary>终焉四连：4×12，终局每拍循环（无出口）。</summary>
	private async Task FinaleLoopMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(QuadDamage).WithHitCount(4).FromMonster(this)
			.WithAttackerAnim("FinalQuad", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "finalquad"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
	}

	/// <summary>
	/// 濒死：占位空拍（入场倒地 <c>stance3_death</c> 已由死亡拦截钩子当场播——
	/// <see cref="CznMonster.Content.Powers.SeraphionHalfDeathPower.AfterPreventingDeath"/>；本拍只做节拍等待）。
	/// 复活 / 真死由 Power 钩子驱动；无濒死入场采样（T15：濒死进入静音）。
	/// </summary>
	private async Task FadingMove(IReadOnlyList<Creature> targets)
	{
		await Cmd.CustomScaledWait(0.1f, 0.2f);
	}

	/// <summary>
	/// 退出持盾形态（复归拍后的下个行动前缀）：stance1_to_normal（1.0s）回普通待机。
	/// </summary>
	private async Task ExitP2FormIfNeeded()
	{
		if (!_p2Form)
		{
			return;
		}

		_p2Form = false;
		CznMonsterSfx.Play(this, "tonormal");
		await CreatureCmd.TriggerAnim(Creature, "ToNormal", 1.00f);
	}

	// ── 动画（文档 §5.1 映射表） ──

	/// <inheritdoc />
	/// <remarks>
	/// CZN 的待机动画叫 <c>normal_idle</c>，与游戏默认的 <c>idle_loop</c> 不同，必须 override。
	/// 三个形态的待机 / 死亡都按当前阶段切换：濒死 = stance3 组（条件分支先插，默认在后）。
	/// </remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		// 引擎从不发 "Spawn" 触发器：登场动画必须是初始状态（同原生 Parafright / GasBomb）。
		AnimState enter = new("normal_enter") { NextState = idle };
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState groggy = new("normal_groggy") { NextState = idle };
		AnimState deadNormal = new("normal_death");
		AnimState bite = new("normal_attack_play1") { NextState = idle };
		AnimState cyclone = new("normal_attack_play2") { NextState = idle };
		AnimState stance3Idle = new("stance3_idle", isLooping: true);
		AnimState toHalfDeath = new("normal_to_stance3") { NextState = stance3Idle };
		// 濒死入场倒地（死亡拦截钩子当场触发）：stance3_death 播完收进跪姿待机，
		// 与真死的 "Dead"→stance3_death（终态，无 NextState）分开注册
		AnimState enterDying = new("stance3_death") { NextState = stance3Idle };
		AnimState deadFading = new("stance3_death");
		AnimState revival = new("stance3_to_normal") { NextState = idle };
		AnimState toShield = new("normal_to_stance1") { NextState = idle };
		AnimState shieldStance = new("stance1_attack_play1") { NextState = idle };
		AnimState toNormal = new("stance1_to_normal") { NextState = idle };
		AnimState finaleBuff = new("stance2_buff_1") { NextState = idle };
		AnimState finalQuad = new("stance2_attack_play1") { NextState = idle };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("Bite", bite);
		animator.AddAnyState("Cyclone", cyclone);
		animator.AddAnyState("Revival", revival);
		animator.AddAnyState("ToShieldForm", toShield);
		animator.AddAnyState("ShieldStance", shieldStance);
		animator.AddAnyState("ToNormal", toNormal);
		animator.AddAnyState("FinaleBuff", finaleBuff);
		animator.AddAnyState("FinalQuad", finalQuad);
		animator.AddAnyState("ToHalfDeath", toHalfDeath);
		animator.AddAnyState("EnterDying", enterDying);
		// 先加带条件的，再加默认分支（AnimState 按插入顺序取第一个满足条件的分支）
		animator.AddAnyState("Dead", deadFading, () => HalfDeathActive);
		animator.AddAnyState("Dead", deadNormal);
		animator.AddAnyState("Hit", groggy, () => Creature.IsStunned);
		animator.AddAnyState("Hit", hit);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>登场与濒死是状态转换，不作为图鉴招式展示；复归回稳保留（T17）。</remarks>
	protected override bool ShouldShowMoveInBestiary(string moveStateId)
	{
		return moveStateId != "SOVEREIGN_UPRISING_MOVE"
			&& moveStateId != "FADING_MOVE";
	}

	/// <inheritdoc />
	/// <remarks>
	/// 图鉴条目固定展示「常态骨架」这套动作（<c>normal_*</c> / <c>stance2_*</c>），
	/// 死亡用 <c>normal_death</c>（濒死形态的 <c>stance3_death</c> 只在战斗中由死亡拦截钩子触发，
	/// 图鉴里没有「濒死」这个前置状态）。复归回稳借持盾形态的三连 clip，与战斗里复归拍的收尾动作同源。
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: new CznBestiaryAction[]
			{
				new(GetBestiaryMoveName("SOVEREIGN_BITE"), "normal_attack_play1", "bite"),
				new(GetBestiaryMoveName("FINALE_CYCLONE"), "normal_attack_play2", "cyclone"),
				new(GetBestiaryMoveName("REVIVAL"), "stance1_attack_play1", "shieldstance"),
				new(GetBestiaryMoveName("FINALE_OPEN"), "stance2_buff_1", "finalebuff"),
				new(GetBestiaryMoveName("FINALE_LOOP"), "stance2_attack_play1", "finalquad"),
			},
			hurtClip: "normal_hit",
			hurtSfx: "hurt",
			dieClip: "normal_death",
			dieSfx: "death");
	}
}
