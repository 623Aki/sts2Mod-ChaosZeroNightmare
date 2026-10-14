using System.Collections.Generic;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie（《卡厄思梦境》30093）角色模型。
///
/// <para>资源路径约定与 CznNineCharacter 保持一致：目录名 = mod id。
/// 因此所有资源都放在 <c>res://CznHeidemarie/</c> 之下。</para>
///
/// <para>本体可视场景不是在这里指定的 —— <c>CharacterModel.VisualsPath</c> 是
/// private 成员，取值为 <c>SceneHelper.GetScenePath("creature_visuals/" +
/// Id.Entry.ToLowerInvariant())</c>，即
/// <c>res://scenes/creature_visuals/heidemarie.tscn</c>。
/// 该路径由 mod 的 pck 以同名文件覆盖游戏资源，无需代码介入。</para>
/// </summary>
public sealed class Heidemarie : CharacterModel
{
	/// <summary>本体 Spine 骨架数据，由 res://scenes/creature_visuals/heidemarie.tscn 中的 SpineSprite 引用。</summary>
	public const string BodySkeletonDataPath = "res://CznHeidemarie/animations/model/30093_skel_data.tres";

	public override bool IsPlayable => true;

	public override CharacterGender Gender => CharacterGender.Feminine;

	protected override CharacterModel? UnlocksAfterRunAs => null;

	public override Color NameColor => new Color("C8B6E2");

	public override int StartingHp => 70;

	public override int StartingGold => 99;

	// 与 model/30093.skel 中 attack_play1(0.667s) / b_idle 的起手节奏对齐
	public override float AttackAnimDelay => 0.2f;

	public override float CastAnimDelay => 0.4667f;

	/// <summary>
	/// 自定义动画状态机映射。
	///
	/// <para>游戏默认实现用的是 <c>idle_loop / attack / cast / hurt / die / relaxed_loop</c>
	/// 这套约定名，而 model/30093.skel 里没有这些名字（只有 <c>b_idle / attack_play1 /
	/// buff_play / hit / death</c> 等）。沿用默认实现会让
	/// <c>CreatureAnimator</c> 找不到动画，只在日志里刷
	/// <c>could not find 'idle_loop' animation</c>，角色会静止不动。</para>
	///
	/// <para>这里按 CznNineCharacter 的同一套思路，把游戏触发器映射到骨架真实动画，
	/// 并给攻击/技能/入场做「起手 → 施放 → 收招 → 待机」的链式衔接。</para>
	/// </summary>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller, Creature creature)
	{
		// ── 待机姿态 ────────────────────────────────────────────────────────
		// 先用骨架能力查询挑出真实存在的待机动画（结果按骨架缓存，不会逐帧查询）：
		// 默认动作取 idle（放松待机），b_idle 作为备选；换成别的骨架时也能自动落到存在的那个，
		// 不至于整段静止。
		string combatIdleName = HeidemarieAnimation.PickFirstAvailable(controller, "idle", "b_idle", "move");
		string relaxedIdleName = HeidemarieAnimation.PickFirstAvailable(controller, "idle", "b_idle");
		string lowHealthIdleName = HeidemarieAnimation.PickFirstAvailable(controller, "collapse_idle", "groggy", "idle");

		// b_idle 是战斗架势（9.6s 循环），作为战斗中的默认待机。
		AnimState idle = new AnimState(combatIdleName, isLooping: true);
		// 血量低于 25% 时改用倒地待机，与游戏默认实现的低血量分支同构。
		AnimState lowHealthIdle = new AnimState(lowHealthIdleName, isLooping: true);
		// idle 是放松待机，由 Relaxed / CardRestore 进入。
		AnimState relaxedIdle = new AnimState(relaxedIdleName, isLooping: true);
		// 两段姿态切换过渡：放松 → 战斗架势 / 战斗架势 → 放松。
		AnimState toCombatStance = new AnimState("idle_to_b_idle");
		AnimState toRelaxedStance = new AnimState("b_idle_to_idle");
		// 骨架里的特殊待机与位移动画，暂由卡牌按需调用。
		AnimState specialIdle = new AnimState("special0_idle", isLooping: true);
		AnimState move = new AnimState("move");

		// ── 受击与眩晕 ──────────────────────────────────────────────────────
		AnimState hit = new AnimState("hit");
		AnimState groggy = new AnimState("groggy");

		// ── 死亡 ────────────────────────────────────────────────────────────
		// 常规：倒地预备 → 倒地。
		AnimState deathReady = new AnimState("death_ready");
		AnimState death = new AnimState("death");
		// 演出强化版：fatal_intro 起手 → fatal1 倒地 → 复用 death 收尾。
		AnimState fatalIntro = new AnimState("fatal_intro");
		AnimState fatal = new AnimState("fatal1");

		// ── 普通攻击（两套收招共用同一段落）────────────────────────────────
		// Attack：起手 → 横挥，Attack2：起手 → 纵劈，随后共用 attack_end 收招。
		AnimState attackReady = new AnimState("attack_ready");
		AnimState attackPlay1 = new AnimState("attack_play1");
		AnimState attack2Ready = new AnimState("attack_ready");
		AnimState attackPlay2 = new AnimState("attack_play2");
		AnimState attackEnd = new AnimState("attack_end");

		// ── 技能攻击链 u1 / u3 ──────────────────────────────────────────────
		AnimState u1Ready = new AnimState("u1_attack_ready");
		AnimState u1Play = new AnimState("u1_attack_play");
		AnimState u1End = new AnimState("u1_attack_end");
		AnimState u3Ready = new AnimState("u3_attack_ready");
		AnimState u3Play = new AnimState("u3_attack_play");
		AnimState u3End = new AnimState("u3_attack_end");

		// ── 终结技 ux / ug（单段演出）───────────────────────────────────────
		AnimState uxAttack = new AnimState("ux_attack");
		AnimState ugAttack = new AnimState("ug_attack");

		// ── 防御 / 增益 / 减益 / 治疗 ───────────────────────────────────────
		AnimState defenseReady = new AnimState("defense_ready");
		AnimState defensePlay = new AnimState("defense_play");
		AnimState buffReady = new AnimState("buff_ready");
		AnimState buffPlay = new AnimState("buff_play");
		AnimState u2Ready = new AnimState("u2_buff_ready");
		AnimState u2Play = new AnimState("u2_buff_play");
		AnimState u4Ready = new AnimState("u4_buff_ready");
		AnimState u4Play = new AnimState("u4_buff_play");
		AnimState debuffReady = new AnimState("debuff_ready");
		AnimState debuffPlay = new AnimState("debuff_play");
		AnimState cureReady = new AnimState("cure_ready");
		AnimState curePlay = new AnimState("cure_play");

		// ── 入场与胜利 ──────────────────────────────────────────────────────
		AnimState enterReady = new AnimState("enter_ready");
		AnimState enterPlay = new AnimState("enter_play");
		AnimState enterEnd = new AnimState("enter_end");
		AnimState victoryReady = new AnimState("victory_ready");
		AnimState victory = new AnimState("victory", isLooping: true);

		// ── 衔接：所有短动画收招后回到战斗架势待机 ──────────────────────────
		toCombatStance.AddNextState(idle);
		toRelaxedStance.AddNextState(relaxedIdle);
		move.AddNextState(idle);
		hit.AddNextState(idle);
		groggy.AddNextState(idle);

		attackReady.AddNextState(attackPlay1);
		attackPlay1.AddNextState(attackEnd);
		attack2Ready.AddNextState(attackPlay2);
		attackPlay2.AddNextState(attackEnd);
		attackEnd.AddNextState(idle);

		u1Ready.AddNextState(u1Play);
		u1Play.AddNextState(u1End);
		u1End.AddNextState(idle);
		u3Ready.AddNextState(u3Play);
		u3Play.AddNextState(u3End);
		u3End.AddNextState(idle);
		uxAttack.AddNextState(idle);
		ugAttack.AddNextState(idle);

		defenseReady.AddNextState(defensePlay);
		defensePlay.AddNextState(idle);
		buffReady.AddNextState(buffPlay);
		buffPlay.AddNextState(idle);
		u2Ready.AddNextState(u2Play);
		u2Play.AddNextState(idle);
		u4Ready.AddNextState(u4Play);
		u4Play.AddNextState(idle);
		debuffReady.AddNextState(debuffPlay);
		debuffPlay.AddNextState(idle);
		cureReady.AddNextState(curePlay);
		curePlay.AddNextState(idle);

		enterReady.AddNextState(enterPlay);
		enterPlay.AddNextState(enterEnd);
		enterEnd.AddNextState(idle);
		victoryReady.AddNextState(victory);

		deathReady.AddNextState(death);
		fatalIntro.AddNextState(fatal);
		fatal.AddNextState(death);

		// ── 触发器接线 ──────────────────────────────────────────────────────
		CreatureAnimator animator = new CreatureAnimator(idle, controller);

		// 待机：按血量在战斗架势与倒地待机之间二选一
		animator.AddAnyState("Idle", idle, () => !IsLowHealth(creature));
		animator.AddAnyState("Idle", lowHealthIdle, () => IsLowHealth(creature));
		animator.AddAnyState("Relaxed", relaxedIdle);
		animator.AddAnyState("Revive", idle);

		// 姿态切换
		animator.AddAnyState("CardReady", toCombatStance);
		animator.AddAnyState("CardRestore", toRelaxedStance);
		animator.AddAnyState("SpecialIdle", specialIdle);
		animator.AddAnyState("Move", move);

		// 受击
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Groggy", groggy);

		// 攻击
		animator.AddAnyState("Attack", attackReady);
		animator.AddAnyState("Attack2", attack2Ready);
		animator.AddAnyState("U1Attack", u1Ready);
		animator.AddAnyState("U3Attack", u3Ready);
		animator.AddAnyState("XAttack", uxAttack);
		animator.AddAnyState("GAttack", ugAttack);

		// 防御 / 增益 / 减益 / 治疗
		animator.AddAnyState("Defend", defenseReady);
		animator.AddAnyState("Cast", buffReady);
		animator.AddAnyState("PowerUp", buffReady);
		animator.AddAnyState("U2Buff", u2Ready);
		animator.AddAnyState("U4Buff", u4Ready);
		animator.AddAnyState("Debuff", debuffReady);
		animator.AddAnyState("Cure", cureReady);

		// 入场 / 胜利 / 死亡
		animator.AddAnyState("Entry", enterReady);
		animator.AddAnyState("Victory", victoryReady);
		animator.AddAnyState("Dead", deathReady);
		animator.AddAnyState("Fatal", fatalIntro);

		return animator;
	}

	public override CardPoolModel CardPool => ModelDb.CardPool<HeidemarieCardPool>();

	public override RelicPoolModel RelicPool => ModelDb.RelicPool<HeidemarieRelicPool>();

	public override PotionPoolModel PotionPool => ModelDb.PotionPool<HeidemariePotionPool>();

	/// <summary>
	/// 初始卡组：3 张打击 + 3 张防御 + 1 张凝聚极光 + 1 张剑雨 + 1 张剑光（共 9 张）。
	///
	/// <para>测试期曾临时塞入 8 张「专属牌」用于点亮骨架动画，现已移除。
	/// 角色卡（剑光 / 剑雨 / 剑冢 / 凝锋 / 安魂剑 / 绝响）通过奖励与商店获取；
	/// 「剑光」后续会加入初始卡组（当前阶段先保持纯基础攻防）。</para>
	/// </summary>
	public override IEnumerable<CardModel> StartingDeck => new CardModel[9]
	{
		// 基础攻防 3 打击 + 3 防御 + 凝聚极光 + 剑光 + 剑雨
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieDefend>(),
		ModelDb.Card<HeidemarieDefend>(),
		ModelDb.Card<HeidemarieDefend>(),
		ModelDb.Card<HeidemarieCondensedAurora>(),
		ModelDb.Card<HeidemarieSwordrain>(),
		ModelDb.Card<HeidemarieSwordlight>(),
	};

	/// <summary>
	/// 初始遗物：战士的默认遗物「燃烧之血」（战斗胜利时回复生命）。
	/// </summary>
	public override IReadOnlyList<RelicModel> StartingRelics => new RelicModel[1]
	{
		ModelDb.Relic<BurningBlood>(),
	};

	/// <summary>
	/// 专属转场音效就位前，临时复用战士的转场音效，避免 FMOD 找不到事件报错。
	/// </summary>
	public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

	/// <summary>
	/// 选角语音（在选人界面点选本角色时由 <c>NCharacterSelectScreen</c> 播放）。
	///
	/// <para>这里返回的是<b>占位 FMOD 事件路径</b>，不是真事件 —— 本角色真正的语音
	/// 是素材区导出的 ogg（<c>se_*</c> / <c>vo_30093_*.ogg</c>），由
	/// <see cref="Integration.HeidemarieCombatAudio"/> 用 Godot 音频播放器播放。
	/// 占位路径由 <c>Registration/HeidemarieCombatPresentationPatch.cs</c> 里的
	/// <c>SfxCmd.Play</c> 补丁按前缀拦下，改播我们自己的
	/// <c>vo_30093_team_join_01.ogg</c>：既不会去 FMOD 解析一个不存在的事件，
	/// 也顺带避免「借用战士选角语音」那种串味。</para>
	/// </summary>
	public override string CharacterSelectSfx => Registration.HeidemarieSfxRedirect.PlaceholderSfxPrefix + "select";

	public override List<string> GetArchitectAttackVfx() => new List<string>
	{
		"vfx/vfx_attack_blunt",
		"vfx/vfx_heavy_blunt",
		"vfx/vfx_attack_slash",
		"vfx/vfx_bloody_impact",
		"vfx/vfx_rock_shatter",
	};
}
