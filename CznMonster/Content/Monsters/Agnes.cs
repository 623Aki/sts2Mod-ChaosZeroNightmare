using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using CznMonster.Content.Powers;
using Godot;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 阿涅丝（CZN 3000504）：双形态变身型首领（阿涅丝设计文档 §1.1 / §1.2）。
///
/// <para>形态流转（v0.7 重构：共鸣从「每拍消耗的乘区」改为「只增的威胁刻度，唯一释放口 = 处决」）：</para>
/// <list type="bullet">
/// <item>开场直接进入集约期（Form A）：<b>连击 → 增益 → 重击 → 压制</b> 四拍固定循环
/// （v0.7b：无登场空过拍、四拍里两次攻击）。
/// 一阶段<b>不挂共鸣</b>、也<b>不召唤碎片</b>（v0.7a：碎片与共鸣一并在变身拍首次登场），
/// 招式数值固定、意图零热更；</item>
/// <item>被打到 HP&lt;50% → 变身·壹：播 <c>normal_model_transform_to_stance1</c>
/// （2.0s）→ <b>运行时换骨</b>（<c>MegaSprite.SetSkeletonDataRes</c>，引擎地图 Boss 点 / 宝箱动画
/// 的原生用法）→ 名称升格「阿涅丝·弗布尔加扎伊特」→ 回复 20% 已损失生命 → 召唤余响碎片 ×3
/// → 共鸣首次铺 3 层 → 力量 +3 → 播 <c>stance1_model_transform_end</c>（3.67s）落地；</item>
/// <item>绽放期（Form B）：每轮 = 3 个随机行动（袋抽不重复）+ 第 4 拍固定<b>处决</b>；
/// 随机池 = 重击·贰 / 斩击 / 压制 / 三连（压制无伤害，但蓄力 +2 共鸣）；</item>
/// <item>处决：收割全部碎片（不再回馈层数）→ 倾泻共鸣的一刀 → 重召 ×3 → 共鸣清零重铺 3；</item>
/// <item>循环至死亡。Form A 无死亡动作（CZN 无 death/fatal）→ 死亡触发器落 <c>normal_groggy</c>
/// 兜底；Form B 用 <c>stance1_death</c>。</item>
/// </list>
///
/// <para><b>共鸣（v0.7 重构）</b>：只增不减——普通攻击既不吃也不消耗它，
/// 因此「意图显示的数字」与「实际挨的伤害」天然一致，不存在中途被清空的争议。
/// 增长源只有两个：变身 / 处决后重铺 3 层、压制拍（蓄力）+2（上限 10）。</para>
///
/// <para><b>处决（v0.7 唯一释放口）</b>：
/// <c>伤害 = 处决基础 × (1 + 共鸣层数 + 2 × 场上存活碎片数)</c>，随后共鸣清零重铺 3。
/// 公式每一项都是玩家肉眼可见的量（Power 层数 + 场上碎片数），委托实时读取，
/// 玩家在回合内打碎一只碎片会让图标数字当场变化（-1 系数即 -1 倍基础伤害），
/// 因此处决意图同样满足「看到什么就是什么」。</para>
///
/// <para>换骨安全性：<c>SetSkeletonDataRes</c> 走 spine-godot 的 <c>set_skeleton_data_res</c>
/// 原生接口，动画状态随后按<b>clip 名</b>重新解析——自定义 <c>GenerateAnimator</c> 里全部
/// 触发器都是语义名（<c>AddAnyState</c>），Form A/Form B 的触发器互不冲突（连击/重击各注册
/// 两分支，谓词按 <c>_formB</c> 关闭另一侧），换骨后旧骨架的 NextState 链不再被触发，
/// 新链从 <c>TransformEnd → idleB</c> 起接手。</para>
/// </summary>
public sealed class Agnes : MonsterModel
{
	// ── 数值（第 2/3 章基准，升华值在前；再乘章节系数，见 AgnesScaling） ──

	/// <summary>集约期连击（Form A）：每击 9，固定 3 段（v0.7：不再吃共鸣）。</summary>
	private int ComboDamage => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9));

	/// <summary>重击（Form A m_attack 取中位 26;Form B m_attack 升格 30）。</summary>
	private int HeavyDamage => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 29, 26));

	/// <summary>重击（Form B m_attack）：30。</summary>
	private int HeavyDamageB => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 33, 30));

	/// <summary>三连（Form B）：3 × 8。</summary>
	private int TripleDamage => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8));

	/// <summary>斩击（Form B s_attack）：34。</summary>
	private int SlashDamage => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 37, 34));

	/// <summary>增益（Form A buff_1）：全体己方力量 +2。</summary>
	private const int FaStrengthGain = 2;

	/// <summary>变身·壹：形态升格附赠力量 +3。</summary>
	private const int TransformStrengthGain = 3;

	/// <summary>压制（Form B debuff_1）：虚弱 2 + 脆弱 2（v0.7：不再附带「无碎片则 +2 力量」，
	/// 避免与「清场 = 降处决伤害」的决策面相冲突）。</summary>
	private const int DebuffAmount = 2;

	/// <summary>Form B 每轮的随机行动拍数（第 4 拍固定处决，一拍一循环）。</summary>
	private const int FbRandomBeats = 3;

	/// <summary>压制拍（Form B 唯一的蓄力拍）给予的共鸣层数。</summary>
	private const int SuppressCharge = 2;

	/// <summary>
	/// 处决基础伤害：处决 = 基础 × (1 + 共鸣 + 2×场上存活碎片)。
	///
	/// <para>标定（v0.7b 下调 20 → 9，并取消「击杀碎片 +1 共鸣」）：
	/// 系数区间 = 4（全清、无蓄力）~ 12（碎片 3 只 + 蓄力），基础 9 之后为
	/// <b>36 / 54 / 72 / 90 / 108</b>。每清一只碎片系数 −2 ⇒ <b>少挨 18 点</b>，
	/// 恰好等于一只碎片的血量——「花 18 点输出换 18 点减伤」是等价交换，
	/// 玩家可以按手牌自由决定清几只（v0.6 的 +1 共鸣会让这个交换变成负收益，故删除）。</para>
	/// </summary>
	private int ExecuteDamageBase => AgnesScaling.Dmg(
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9));

	/// <summary>
	/// 处决时每只存活碎片折算的系数（2 ⇒ 每只等于 2 倍基础值 = 18 点，与碎片 18 HP 等价）。
	/// 与 <see cref="AgnesResonancePower"/> 的层数无关：碎片的价值只在这里体现。
	/// </summary>
	private const int EchoFuelWeight = 2;

	/// <summary>连击固定段数（v0.7：段数回归固定，共鸣不再追加段数）。</summary>
	private const int ComboHitsBase = 3;

	/// <summary>三连固定段数（v0.7 同款口径，固定 3 段）。</summary>
	private const int TripleHitsBase = 3;

	/// <summary>共鸣上限（v0.7：蓄力 +2 / 玩家击杀 +1 汇流处的安全阀，正常一轮只到 5~9）。</summary>
	private const int ResonanceCap = 10;

	/// <summary>变身·壹：回复已损失生命的 20%（v0.6 半血触发配套；向下取整）。</summary>
	private const decimal TransformHealRatio = 0.20m;

	// ── 状态 ──
	private bool _formB;    // 变身完成后 = true（名称 / 换骨 / 动画分支的判定源）
	private int _faTurn;    // Form A 行动拍游标（enter 不计）
	private int _fbTurn;    // Form B 行动拍游标

	/// <summary>共鸣层数（显示在 <see cref="AgnesResonancePower"/> 上的同步字段）。</summary>
	private int _resonance;

	/// <summary>当前是否处于第二形态（「阿涅丝·弗布尔加扎伊特」）。</summary>
	public bool IsSecondForm => _formB;

	/// <summary>变身换骨用的第二形态骨架资源（tres 三件套，与场景同源生成）。</summary>
	private const string FormBSkeletonRes = "res://CznMonster/animations/monsters/agnes_final/agnes_final_skel_data.tres";

	/// <summary>
	/// Form B 换骨后的标定（二轮实测重算，采样自转换后骨架 idle 并集）：
	/// 骨架 idle bounds = x[-400.8, 483.0]（中心 +41）、y[-598.1, +370.6]——
	/// 大半身体在原点（血条锚线）**下方**，原 0.9 缩放会把 333px 的裙摆压到
	/// 血条下面的手牌区，且意图图标位 (0,-800) 直接飞出屏顶。
	/// 缩放 0.70 + 整体上移 200：头顶落在血条线上方约 619 局部 px（≈屏顶留 35px），
	/// 裙摆收在血条下方约 59 局部 px；水平再 -30 让躯干中心对准槽位。
	/// </summary>
	private const float VisualScaleB = 0.70f;

	/// <summary>Form B 换骨后的 SpineSprite 位偏（局部 px，相对实体锚点，「负 y = 上」）。</summary>
	private static readonly Vector2 BodyOffsetB = new(-30f, -200f);

	/// <summary>Form B 意图图标的头顶位置（头顶上方约 71 局部 px）。</summary>
	private static readonly Vector2 IntentPosB = new(0f, -690f);

	/// <summary>Form A 意图图标位置（场景默认值，同 <c>agnes.tscn</c>）。</summary>
	private static readonly Vector2 IntentPosA = new(0f, -500f);

	/// <inheritdoc />
	/// <remarks>变身前显示「阿涅丝」，变身后显示「阿涅丝·弗布尔加扎伊特」；
	/// 引擎在头像 / 日志 / 图鉴读取 <see cref="MonsterModel.Title"/> 时动态取值（override 即换名）。</remarks>
	public override LocString Title => _formB
		? L10NMonsterLookup("AGNES_FINAL.name")
		: base.Title;

	// 战斗内 Form A / Form B 共用一个 entry（换骨 + 动态 Title）；
	// 图鉴里这条只展示 Form A，第二形态由 AgnesFinal 单独登记成一条（各自一套骨架与动作）。

	/// <inheritdoc />
	/// <remarks>血量覆盖（上游契约 §6.4）：boss 血量自定义设置只作用于本组的主敌。
	/// 640 = 波勒米卡 450 × 1.4 双形态系数（阿涅丝设计文档 §2.1）。</remarks>
	public override int MinInitialHp => MonsterSettings.CustomBossHp ?? AgnesScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 672, 640));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/agnes.tscn";

	/// <inheritdoc />
	/// <remarks>占位路径由 <see cref="CznMonsterSfx"/> 解析成本地 ogg（设计文档 §5.4）。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, _formB ? "b_heavy" : "combo");

	/// <inheritdoc />
	/// <remarks>Form A 无死亡采样（bank 5 条仅 enter/attack/buff）；Form B 用 <c>stance1_death</c>。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, "death");

	/// <inheritdoc />
	/// <remarks>变身后才有死亡演出采样（stance1_death 命中 <c>DeathSfx</c> 同一路径）。</remarks>
	public override bool HasDeathSfx => _formB;

	/// <inheritdoc />
	/// <remarks>两个 bank 均无受击采样：静音侧写（同塞拉菲翁 T15 口径）。</remarks>
	public override string? HurtSfx => null;

	/// <inheritdoc />
	/// <remarks>登场动画是初始 <see cref="AnimState"/>（引擎没有 "Spawn" 触发器），音效在这里补播。
	/// <para>v0.7：一阶段<b>不挂共鸣</b>——共鸣在变身拍首次铺 3 层（<see cref="RebaseResonance"/>），
	/// 「变身 = 新机制登场」，一阶段招式数值固定、无资源记账。</para></remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		CznMonsterSfx.Play(this, "enter");
	}

	/// <summary>
	/// 共鸣 +N（显示同步；吃 <see cref="ResonanceCap"/> 上限；只在 Boss 存活时生效）。
	/// Power 可能被引擎中途摘除（如负向清层），摘除后走 <see cref="PowerCmd.Apply"/> 重挂载。
	/// <para>v0.7：仅二阶段生效（一阶段不挂共鸣）；v0.7b 起唯一调用方是 Form B 的压制拍（蓄力 +2）
	/// ——碎片死亡不再产生层数，所以一阶段打碎碎片不会亮起图标，也不会被变身重铺冲掉。</para>
	/// </summary>
	internal async Task AddResonance(int amount)
	{
		if (amount <= 0 || !_formB || Creature is not { IsDead: false })
		{
			return;
		}

		_resonance = Math.Min(_resonance + amount, ResonanceCap);
		AgnesResonancePower? power = Creature.GetPower<AgnesResonancePower>();
		if (power != null)
		{
			power.SetAmount(_resonance);
			return;
		}

		if (_resonance > 0)
		{
			await PowerCmd.Apply<AgnesResonancePower>(
				new ThrowingPlayerChoiceContext(), Creature, _resonance, Creature, null);
		}
	}

	/// <summary>
	/// 处决伤害系数（v0.7）：<c>1 + 共鸣层数 + 2 × 场上存活碎片数</c>。
	/// 三项全部是玩家可见的量（Power 层数 + 场上碎片数），意图委托与招式方法体同源取值，
	/// 因此处决图标上的数字永远等于实际伤害——回合内打碎一只碎片会让它当场降一档。
	/// </summary>
	private int ExecuteCoefficient => 1 + _resonance + EchoFuelWeight * LivingEchoes;

	/// <summary>处决实际伤害（意图委托与招式方法体共用同一算式）。</summary>
	private int ExecuteDamage => ExecuteDamageBase * ExecuteCoefficient;

	/// <summary>共鸣清零后重铺到 N（变身 / 处决两个唯一入口）。</summary>
	private async Task RebaseResonance(int amount)
	{
		_resonance = amount;
		AgnesResonancePower? power = Creature.GetPower<AgnesResonancePower>();
		if (power != null)
		{
			power.SetAmount(amount);
			return;
		}

		await PowerCmd.Apply<AgnesResonancePower>(
			new ThrowingPlayerChoiceContext(), Creature, amount, Creature, null);
	}

	/// <summary>场上存活的余响碎片数量。</summary>
	private int LivingEchoes => CombatState.GetTeammatesOf(Creature)
		.Count(c => c != Creature && c.IsAlive && c.Monster is AgnesEcho);

	/// <inheritdoc />
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		// ── 行动（阿涅丝设计文档 §1.2 / §1.4；除处决外意图全部为静态常量，零热更、零显示偏差） ──
		// v0.7b：删除「登场空过拍」——初始状态直接挂分支，第 1 个玩家回合面板上就是真实意图，
		// Boss 第一个行动拍立刻出招（登场动画由 Animator 的初始 AnimState 负责，不占行动拍）。
		MoveState combo = new("AGNES_COMBO_MOVE", ComboMove,
			new MultiAttackIntent(ComboDamage, ComboHitsBase));
		MoveState buff = new("AGNES_BUFF_MOVE", BuffMove, new BuffIntent());
		// v0.7a：一阶段第三拍由「召唤」改为「压制」——碎片不打人、不喂层、变身又会重铺，
		// 一阶段铺碎片是纯死拍还会诱导玩家误伤；碎片与共鸣改为一并在变身拍首次登场。
		MoveState suppressA = new("AGNES_SUPPRESS_A_MOVE", SuppressMoveA, new DebuffIntent());
		MoveState heavy = new("AGNES_HEAVY_MOVE", HeavyMove,
			new SingleAttackIntent(HeavyDamage));
		MoveState transform = new("AGNES_TRANSFORM_MOVE", TransformMove,
			new SummonIntent(), new BuffIntent());
		MoveState heavyB = new("AGNES_HEAVY_B_MOVE", HeavyBMove,
			new SingleAttackIntent(HeavyDamageB));
		MoveState triple = new("AGNES_TRIPLE_MOVE", TripleMove,
			new MultiAttackIntent(TripleDamage, TripleHitsBase));
		MoveState debuff = new("AGNES_SUPPRESS_MOVE", SuppressMove, new DebuffIntent());
		MoveState slash = new("AGNES_SLASH_MOVE", SlashMove,
			new SingleAttackIntent(SlashDamage));
		// 处决：唯一带委托的意图——三个入参（基础值 / 共鸣 / 碎片数）全对玩家可见，
		// 委托与 ExecuteMove 同源取值，故图标数字即实伤（设计方案 v0.7）。
		MoveState execute = new("AGNES_EXECUTE_MOVE", ExecuteMove,
			new AgnesExecuteIntent(() => (decimal)ExecuteDamage));

		// ── Form A 分支（v0.7b：四拍一轮，攻击密度翻倍——原三拍只有 1 次攻击，
		//    一阶段实际输出不足）：连击 → 增益 → 重击 → 压制 ──
		ConditionalBranchState fa = new("AGNES_FA_BRANCH");
		fa.AddState(transform, () => !_formB && Creature.GetHpPercentRemaining() < 0.5);
		fa.AddState(combo, () => _faTurn % 4 == 0);
		fa.AddState(buff, () => _faTurn % 4 == 1);
		fa.AddState(heavy, () => _faTurn % 4 == 2);
		fa.AddState(suppressA, () => true);

		// ── Form B 分支：3 个随机行动（袋抽不重复）+ 第 4 拍固定处决 ──
		AgnesFormBPoolState fb = new(
			"AGNES_FB_POOL_STATE",
			new[] { heavyB.Id, slash.Id, debuff.Id, triple.Id },
			execute.Id,
			() => _fbTurn);

		// ── 连线 ──
		combo.FollowUpState = fa;
		buff.FollowUpState = fa;
		suppressA.FollowUpState = fa;
		heavy.FollowUpState = fa;
		transform.FollowUpState = fb;
		heavyB.FollowUpState = fb;
		triple.FollowUpState = fb;
		debuff.FollowUpState = fb;
		slash.FollowUpState = fb;
		execute.FollowUpState = fb;

		MonsterState[] states =
		{
			combo, buff, suppressA, heavy, transform,
			heavyB, triple, debuff, slash, execute, fa, fb,
		};
		// 开场状态直接是 Form A 分支：第 1 个玩家回合就能看到真实意图，无「白让一回合」。
		return new MonsterMoveStateMachine(states, fa);
	}

	/// <summary>
	/// Form B 的随机行动袋（v0.7）：每轮独立重填、3 抽不重复、第 4 拍固定处决。
	///
	/// <para>抽签发生在引擎「意图锁定」时（<c>Creature.PrepareForNextTurn →
	/// MonsterMoveStateMachine.FindNextMoveState</c>），一次 RollMove 只调一次
	/// <see cref="GetNextState"/>（命中 MoveState 即停），所以抽到的招与随后显示/执行的招
	/// 必然是同一个，不会「显示一招打另一招」。</para>
	///
	/// <para>袋子按轮清空（判到第 4 拍时 <c>_bag.Clear()</c>），下一轮第一次抽签时重填——
	/// 因此每轮的 3 个行动都是「池内不重复」的独立三重抽样。</para>
	/// </summary>
	private sealed class AgnesFormBPoolState : MonsterState
	{
		private readonly List<string> _bag = new();
		private readonly IReadOnlyList<string> _pool;
		private readonly string _executeStateId;
		private readonly Func<int> _beatsTaken;

		public AgnesFormBPoolState(
			string stateId, IReadOnlyList<string> pool, string executeStateId, Func<int> beatsTaken)
		{
			PoolId = stateId;
			_pool = pool;
			_executeStateId = executeStateId;
			_beatsTaken = beatsTaken;
		}

		public string PoolId { get; }

		/// <inheritdoc />
		public override string Id => PoolId;

		/// <inheritdoc />
		/// <remarks>分支节点本身不进战斗日志（与 <c>ConditionalBranchState</c> 同口径）。</remarks>
		public override bool ShouldAppearInLogs => false;

		/// <inheritdoc />
		public override string GetNextState(Creature owner, Rng rng)
		{
			// 本轮 3 个随机行动已用完 → 第 4 拍固定处决，并清袋（下一轮独立重填）
			if (_beatsTaken() >= FbRandomBeats)
			{
				_bag.Clear();
				return _executeStateId;
			}

			if (_bag.Count == 0)
			{
				_bag.AddRange(_pool);
			}

			int index = rng.NextInt(_bag.Count);
			string next = _bag[index];
			_bag.RemoveAt(index);
			return next;
		}

		/// <inheritdoc />
		public override void RegisterStates(Dictionary<string, MonsterState> monsterStates)
		{
			monsterStates.Add(Id, this);
		}
	}

	// ── 行为实现 ──

	/// <summary>集约期连击：每击 9 × 3 段（v0.7：段数与伤害固定，不吃共鸣）。</summary>
	private async Task ComboMove(IReadOnlyList<Creature> targets)
	{
		if (_formB)
		{
			// 理论不可达（Form B 走 fb 分支），保险落在 Form B 招式上
			await HeavyBMove(targets);
			return;
		}

		await DamageCmd.Attack(ComboDamage).WithHitCount(ComboHitsBase)
			.FromMonster(this)
			.WithAttackerAnim("Combo", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "combo"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		_faTurn++;
	}

	/// <summary>集约期增益：播 normal_buff_1（1.8s），全体己方力量 +2（不消耗共鸣）。</summary>
	private async Task BuffMove(IReadOnlyList<Creature> targets)
	{
		CznMonsterSfx.Play(this, "buff");
		await CreatureCmd.TriggerAnim(Creature, "Buff", 1.80f);
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), CombatState.GetTeammatesOf(Creature),
			FaStrengthGain, Creature, null);
		_faTurn++;
	}

	/// <summary>
	/// 集约期压制（一阶段循环第三拍，v0.7a）：播 normal_buff_1 施法姿态
	/// （Form A 骨架没有 debuff 动作，借增益动作的施法定格），对玩家施加虚弱 2 + 脆弱 2。
	///
	/// <para>一阶段不挂共鸣，因此这里不像 Form B 的压制那样附带蓄力；它的职责是给一阶段
	/// 补一个真正有威胁的减益拍，取代原先「铺了不打人、不喂层、变身又重铺」的空转召唤拍。</para>
	/// </summary>
	private async Task SuppressMoveA(IReadOnlyList<Creature> targets)
	{
		CznMonsterSfx.Play(this, "buff");
		await CreatureCmd.TriggerAnim(Creature, "Buff", 1.80f);
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(), targets, DebuffAmount, Creature, null);
		await PowerCmd.Apply<FrailPower>(
			new ThrowingPlayerChoiceContext(), targets, DebuffAmount, Creature, null);
		_faTurn++;
	}

	/// <summary>集约期重击：固定 26（v0.7：不吃共鸣；Form A 用 normal_attack_ready/play2）。</summary>
	private async Task HeavyMove(IReadOnlyList<Creature> targets)
	{
		if (_formB)
		{
			await HeavyBMove(targets);
			return;
		}

		await DamageCmd.Attack(HeavyDamage).FromMonster(this)
			.WithAttackerAnim("Heavy", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "combo"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		_faTurn++;
	}

	/// <summary>
	/// 变身·壹：Form A 演出链 → 换骨 → 更名 → 回血 20% 已损失 → 召唤 ×3 → 共鸣首次铺 3
	/// → 力量 +3 → 落地。只有 Form A 才可走（Form B 的重召走 <see cref="ExecuteMove"/>）。
	/// </summary>
	private async Task TransformMove(IReadOnlyList<Creature> targets)
	{
		if (_formB)
		{
			await ExecuteMove(targets);
			return;
		}

		// ① 变身演出（Form A 骨架 normal_model_transform_to_stance1，2.00s）
		await CreatureCmd.TriggerAnim(Creature, "Transform", 2.00f);

		// ② 运行时换骨（引擎原生接口，NBossMapPoint / NTreasureButton 同款用法）：
		//    Form B 骨架 + 缩放标定 + 意图图标上移（hpbar_stance_offset 的 STS2 对应）。
		//    加载方式必须照 ActModel.ChestSpineResource 的引擎口径：
		//    tres 加载成普通 Resource 后用 binding 包装 —— 直接
		//    ResourceLoader.Load<MegaSkeletonDataResource> 会 InvalidCastException
		//    （tres 的 Godot 类型是 SpineSkeletonDataResource，MegaSkeletonDataResource 只是
		//    C# binding 包装；首轮实测日志 godot.log 2026-10-22 16:31 崩点即此处）。
		NCreatureVisuals? visuals = Creature.GetCreatureNode()?.Visuals;
		if (visuals?.SpineBody != null
			&& ResourceLoader.Load<Resource>(FormBSkeletonRes) is { } formBResRaw)
		{
			var formBRes = new MegaSkeletonDataResource(formBResRaw);
			visuals.SpineBody.SetSkeletonDataRes(formBRes);
			if (visuals.GetCurrentBody() is { } body)
			{
				body.Scale = new Vector2(VisualScaleB, VisualScaleB);
				body.Position = BodyOffsetB;
			}
			if (Creature.GetCreatureNode()?.GetSpecialNode<Node2D>("%IntentPos") is { } intentPos)
			{
				intentPos.Position = IntentPosB;
			}
		}

		// ③ 更名 + 空位状态（引擎 Title 动态取值，下一拍 UI 即显示新名）
		_formB = true;
		_faTurn = 0;
		_fbTurn = 0;

		// ④ 变身落地（Form B 骨架 stance1_model_transform_end，3.67s）
		CznMonsterSfx.Play(this, "transform");
		await CreatureCmd.TriggerAnim(Creature, "TransformEnd", 3.67f);

		// ④.5 回复 20% 已损失生命（v0.6 半血触发配套；向下取整，动画播完后结算）
		decimal lost = Creature.MaxHp - Creature.CurrentHp;
		if (lost > 0)
		{
			await CreatureCmd.Heal(Creature, decimal.Floor(lost * TransformHealRatio));
		}

		// ⑤ 召唤余响碎片 ×3 + 共鸣首次铺 3 + 力量 +3（变身回合的 CZN MONSTER_CREATE + 升格附赠）
		await SummonEchoes();
		await RebaseResonance(AgnesResonancePower.RebaseAmount);
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(), Creature, TransformStrengthGain, Creature, null);
	}

	/// <summary>Form B 重击：固定 30 单击（v0.7：不吃共鸣；stance1_m_attack_01，2.33s）。</summary>
	private async Task HeavyBMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(HeavyDamageB).FromMonster(this)
			.WithAttackerAnim("HeavyB", 2.33f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "b_heavy"))
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(null);
		_fbTurn++;
	}

	/// <summary>Form B 三连：每击 8 × 3 段（v0.7：段数与伤害固定，不吃共鸣）。</summary>
	private async Task TripleMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(TripleDamage).WithHitCount(TripleHitsBase)
			.FromMonster(this)
			.WithAttackerAnim("Triple", 0.67f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "b_triple"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		_fbTurn++;
	}

	/// <summary>
	/// Form B 压制（本轮的<b>蓄力拍</b>）：播 stance1_debuff_1（1.8s），虚弱 2 + 脆弱 2 + 共鸣 +2。
	/// 不打人，但把下一刀的处决抬高一层——「这回合不疼，下回合更疼」，
	/// 而它同时也是玩家清碎片的空档（清碎片能降处决系数），张力的来源。
	/// </summary>
	private async Task SuppressMove(IReadOnlyList<Creature> targets)
	{
		CznMonsterSfx.Play(this, "debuff");
		await CreatureCmd.TriggerAnim(Creature, "Suppress", 1.80f);
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(), targets, DebuffAmount, Creature, null);
		await PowerCmd.Apply<FrailPower>(
			new ThrowingPlayerChoiceContext(), targets, DebuffAmount, Creature, null);
		await AddResonance(SuppressCharge);
		_fbTurn++;
	}

	/// <summary>Form B 斩击：固定 34 单击（v0.7：不吃共鸣；stance1_s_attack_01_ready/play/end 组）。</summary>
	private async Task SlashMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(SlashDamage).FromMonster(this)
			.WithAttackerAnim("Slash", 3.00f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "b_slash"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);
		_fbTurn++;
	}

	/// <summary>
	/// 处决（v0.7 的高潮拍，每轮第 4 拍）：<b>收割全部碎片 → 倾泻共鸣的一刀 → 重召 ×3
	/// → 共鸣清零重铺 3</b>。
	///
	/// <para>伤害 = <c>基础 × (1 + 共鸣 + 2×场上碎片数)</c>：碎片是燃料，共鸣是引信。
	/// 系数在这里<b>先捕获</b>再斩杀碎片（斩杀后场上数量归零，晚读会变成 0）；
	/// 捕获所用的两个量（共鸣层数 / 存活碎片数）与意图委托完全同源，故玩家看到的数字就是实伤。</para>
	///
	/// <para>玩家在 3 个随机行动里清掉碎片，每清一只系数 −2 ⇒ 处决少挨 18 点（= 一只碎片血量），
	/// 代价是同样的输出没打在 Boss 身上——「清场 vs 抢血」是二阶段唯一的主动抉择，
	/// 且清几只都成立，不会被逼成「要么全清要么全放」。</para>
	/// </summary>
	private async Task ExecuteMove(IReadOnlyList<Creature> targets)
	{
		// ① 先捕获系数（此时碎片还在场，与意图图标同一算式）
		int coefficient = ExecuteCoefficient;
		decimal damage = ExecuteDamageBase * coefficient;

		// ② 收割自己的碎片（处决窗口屏蔽 AfterDeath 的 +1，碎片只作为燃料，不再回馈层数）
		await ExecuteEchoes();

		// ③ 倾泻共鸣的一刀（stance1_s_attack_01 大斩，3.00s）
		await DamageCmd.Attack(damage).FromMonster(this)
			.WithAttackerAnim("Execute", 3.00f)
			.WithAttackerFx(null, CznMonsterSfx.Placeholder(this, "b_slash"))
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(null);

		// ④ 重召下一轮的燃料
		await SummonEchoes();

		// ⑤ 唯一释放口：共鸣清零重铺 3，进入下一轮
		await RebaseResonance(AgnesResonancePower.RebaseAmount);
		_fbTurn = 0;
	}

	// ── 召唤 / 处决 helper（波勒米卡同款） ──

	/// <summary>找空槽（只看存活，尸体不占位）。</summary>
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

	/// <summary>召唤三枚余响碎片到空闲槽位（CZN cr_00065/66/67 ×3）。</summary>
	private async Task SummonEchoes()
	{
		for (int i = 0; i < 3; i++)
		{
			string? slot = FindFreeSlot();
			if (string.IsNullOrEmpty(slot))
			{
				break;
			}
			// MinionPower 由 AgnesEcho.AfterAddedToRoom 统一挂载，这里不重复添加
			await CreatureCmd.Add<AgnesEcho>(CombatState, slot);
		}
	}

	/// <summary>
	/// 收割全部余响碎片（处决拍的燃料清点）。碎片无处决动画，直接 force 击杀。
	///
	/// <para>v0.7b：碎片的价值已经写进处决公式（每只 +2 系数），共鸣也不再监听碎片死亡，
	/// 因此这里既不加层、也不需要屏蔽任何回调——「玩家打死」与「Boss 收割」是两件独立的事，
	/// 击杀碎片对玩家是纯收益（用输出换处决伤害下降），不存在负收益交换。</para>
	/// </summary>
	private async Task ExecuteEchoes()
	{
		List<Creature> echoes = CombatState.GetTeammatesOf(Creature)
			.Where(c => c != Creature && c.IsAlive && c.Monster is AgnesEcho)
			.ToList();
		if (echoes.Count == 0)
		{
			return;
		}

		// 同 CreatureCmd.Kill 的既知口径：不要手动 RemoveCreature（原生流程异步收尸）。
		await CreatureCmd.Kill(echoes, force: true);
	}

	// ── 动画（文档 §3.1 映射表；触发器全语义名，两形态分支注册） ──

	/// <inheritdoc />
	/// <remarks>
	/// 触发器 → clip：Form A 走 <c>normal_*</c>；Form B 走 <c>stance1_*</c>。
	/// 同名触发器（Combo/Heavy/Hit）按 <see cref="IsSecondForm"/> 谓词分支注册——
	/// AnimState 按插入顺序取第一个满足条件的分支（塞拉菲翁同款机制）。
	/// NextState 链：Form A 待机收 <c>idleA</c>（normal_idle），变身收尾后全部收
	/// <c>idleB</c>（stance1_idle）；换骨后旧 Form A 的 NextState 不会再触发到。
	/// </remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		// Form B 状态先声明（Form A 的变身链 NextState 指向 idleB，C# 局部变量前向引用不允许）
		AnimState idleB = new("stance1_idle", isLooping: true);

		// ── Form A ──
		AnimState idleA = new("normal_idle", isLooping: true);
		// 引擎从不发 "Spawn" 触发器：登场动画必须是初始状态（同原生 Parafright / GasBomb）。
		AnimState enter = new("normal_enter") { NextState = idleA };
		AnimState hitA = new("normal_hit") { NextState = idleA };
		AnimState deadA = new("normal_groggy");   // Form A 无专属死亡动作 → groggy 兜底帧
		AnimState combo = new("normal_attack_play2") { NextState = idleA };
		AnimState heavy = new("normal_attack_ready") { NextState = idleA };
		AnimState buff = new("normal_buff_1") { NextState = idleA };
		// 变身过渡收回 Form A 待机（NextState 在动画播完当场排队，排队时骨架还是 Form A ——
		// 若直接指 idleB 会刷「could not find 'stance1_idle'」并在换骨前空转）；
		// 换骨后本体动作链由 TransformEnd → idleB 接手，这里只是极短回落垫档。
		AnimState transformTo = new("normal_model_transform_to_stance1") { NextState = idleA };

		// ── Form B（换骨后）──
		AnimState hitB = new("stance1_hit") { NextState = idleB };
		AnimState deadB = new("stance1_death");
		AnimState heavyB = new("stance1_m_attack_01") { NextState = idleB };
		AnimState triple = new("stance1_attack_play1") { NextState = idleB };
		AnimState suppress = new("stance1_debuff_1") { NextState = idleB };
		AnimState slash = new("stance1_s_attack_01_play") { NextState = idleB };
		// 处决复用 s_attack_01 的斩击段（3.00s）：一刀斩落同时碎片自灭，
		// 若要换成专属演出，只需把这里改指别的 clip。
		AnimState execute = new("stance1_s_attack_01_play") { NextState = idleB };
		AnimState transformEnd = new("stance1_model_transform_end") { NextState = idleB };

		CreatureAnimator animator = new(enter, controller);
		animator.AddAnyState("Combo", combo);
		animator.AddAnyState("Heavy", heavy);
		animator.AddAnyState("Buff", buff);
		animator.AddAnyState("Transform", transformTo);
		animator.AddAnyState("HeavyB", heavyB);
		animator.AddAnyState("Triple", triple);
		animator.AddAnyState("Suppress", suppress);
		animator.AddAnyState("Slash", slash);
		animator.AddAnyState("Execute", execute);
		animator.AddAnyState("TransformEnd", transformEnd);
		// 先加带条件的，再加默认分支（AnimState 按插入顺序取第一个满足条件的分支）
		animator.AddAnyState("Hit", hitB, () => IsSecondForm);
		animator.AddAnyState("Hit", hitA);
		animator.AddAnyState("Dead", deadB, () => IsSecondForm);
		animator.AddAnyState("Dead", deadA);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>变身是状态转换，不作为图鉴招式展示；处决是招牌招式，保留展示。</remarks>
	protected override bool ShouldShowMoveInBestiary(string moveStateId)
	{
		return moveStateId != "AGNES_TRANSFORM_MOVE";
	}

	/// <inheritdoc />
	/// <remarks>
	/// 本条只负责 <b>Form A</b>（集约期）的一套动作：待机 <c>normal_idle</c>、招式 <c>normal_*</c>。
	/// Form A 骨架没有死亡动作（CZN 只给了 <c>normal_groggy</c>），也没有死亡采样，
	/// 所以「死亡」按钮播 <c>normal_groggy</c> 且静音——与战斗里 <c>Dead</c> 触发器的兜底分支同口径。
	///
	/// <para>第二形态（阿涅丝·弗布尔加扎伊特）是独立条目，见 <see cref="AgnesFinal"/>：
	/// 那边有真正的 <c>stance1_death</c> 与死亡采样。</para>
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: new CznBestiaryAction[]
			{
				new(GetBestiaryMoveName("AGNES_COMBO"), "normal_attack_play2", "combo"),
				new(GetBestiaryMoveName("AGNES_BUFF"), "normal_buff_1", "buff"),
				new(GetBestiaryMoveName("AGNES_HEAVY"), "normal_attack_ready", "combo"),
				new(GetBestiaryMoveName("AGNES_SUPPRESS_A"), "normal_buff_1", "buff"),
			},
			hurtClip: "normal_hit",
			hurtSfx: null,
			dieClip: "normal_groggy",
			dieSfx: null);
	}
}
