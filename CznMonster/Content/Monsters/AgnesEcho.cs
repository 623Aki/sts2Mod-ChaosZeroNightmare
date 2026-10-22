using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using CznMonster.Content.Bestiary;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznMonster.Content.Monsters;

/// <summary>
/// 余响碎片（CZN 3000541）：阿涅丝的召唤物。
///
/// <para>命名裁定：CZN 的 3000541 无可提取名称文本；按「非攻击共鸣体」设计
/// （阿涅丝设计文档 §1.5：骨架仅 <c>normal_idle / normal_hit</c> 两个动作、无攻击采样，
/// 是纯展示/献祭型单位）取名<b>余响碎片</b>（Echo Shard）——变身后铺满的三枚
/// 残响在场时撑起阿涅丝的「共鸣」意象，死亡回馈 +1 共鸣层。</para>
///
/// <para>行为：登场拍空过（HiddenIntent，不出意图图标）；没有攻击动作，也没有
/// 音频 bank（CZN 提取 7 文件无音频）——全静音侧写（同塞拉菲翁 T15 口径）。
/// 挂 <see cref="MinionPower"/>：不阻止胜利、不掉奖励。</para>
/// </summary>
public sealed class AgnesEcho : MonsterModel
{
	/// <inheritdoc />
	/// <remarks>v0.6：基础血量 12 → 18（升华 21，同 +75% 比例）——供品更经打，处决加层更耐攒。</remarks>
	public override int MinInitialHp => AgnesScaling.Hp(
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 21, 18));

	/// <inheritdoc />
	public override int MaxInitialHp => MinInitialHp;

	/// <inheritdoc />
	/// <remarks>召唤物不进原生的章节/事件图鉴流程（专属区块条目不经此过滤）。</remarks>
	public override bool ShouldShowInCompendium => false;

	/// <inheritdoc />
	public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;

	/// <inheritdoc />
	protected override string VisualsPath => "res://scenes/creature_visuals/agnes_echo.tscn";

	/// <inheritdoc />
	/// <remarks>非攻击单位：占位路径在采样表无对应项，<c>SfxCmd.Play</c> 补丁静默跳过。</remarks>
	protected override string AttackSfx => CznMonsterSfx.Placeholder(this, "none");

	/// <inheritdoc />
	/// <remarks>3000541 无音频 bank：占位路径 + <see cref="HasDeathSfx"/> 假，全程静音（T15 口径）。</remarks>
	public override string DeathSfx => CznMonsterSfx.Placeholder(this, "none");

	/// <inheritdoc />
	public override bool HasDeathSfx => false;

	/// <inheritdoc />
	public override string? HurtSfx => null;

	/// <inheritdoc />
	/// <remarks>登场动画是初始 <see cref="AnimState"/>；3000541 无 enter 动作，初始即 idle。</remarks>
	public override async Task AfterAddedToRoom()
	{
		await base.AfterAddedToRoom();
		await PowerCmd.Apply<MinionPower>(
			new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
	}

	/// <inheritdoc />
	/// <remarks>
	/// 非攻击共鸣体：唯一行动是「随波哼鸣」空过拍（不出意图图标）。
	/// 玩家打不打它都行——打死会喂给阿涅丝 1 层共鸣（<see cref="AgnesResonancePower"/> 联动）。
	/// </remarks>
	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		MoveState hum = new("ECHO_HUM_MOVE", HumMove, new HiddenIntent());
		hum.FollowUpState = hum;
		return new MonsterMoveStateMachine(new MonsterState[] { hum }, hum);
	}

	private async Task HumMove(IReadOnlyList<Creature> targets)
	{
		await Cmd.CustomScaledWait(0.1f, 0.2f);
	}

	/// <inheritdoc />
	/// <remarks>空过拍不进图鉴（与登场/阶段转换同口径）。</remarks>
	protected override bool ShouldShowMoveInBestiary(string moveStateId) => false;

	/// <inheritdoc />
	/// <remarks>
	/// 3000541 只有 <c>normal_idle / normal_hit</c> 两个动作，且都拖满 8s 时间轴——
	/// 死亡触发器走 <c>normal_hit</c> 定格一拍（不挂 NextState，避免死亡后又接回 idle
	/// 循环摆动）；时长问题由 <c>CznMonsterEchoDeathAnimPatch</c> 压缩（引擎死亡流程
	/// 按轨道总长等待，见 Registration）。
	/// </remarks>
	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		AnimState idle = new("normal_idle", isLooping: true);
		AnimState hit = new("normal_hit") { NextState = idle };
		AnimState dead = new("normal_hit");

		CreatureAnimator animator = new(idle, controller);
		animator.AddAnyState("Hit", hit);
		animator.AddAnyState("Dead", dead);
		return animator;
	}

	/// <inheritdoc />
	/// <remarks>
	/// 碎片没有招式，图鉴里只给「受伤」「死亡」两条，且都落在 <c>normal_hit</c> 上
	/// （骨架只有 <c>normal_idle / normal_hit</c> 两个动作）——与战斗里的死亡触发器同一取舍。
	/// 无音频 bank，全部静音（T15 口径）。
	/// </remarks>
	public override List<BestiaryMonsterMove> GenerateBestiaryMoveList(NCreatureVisuals? creatureVisuals)
	{
		return CznBestiaryMoves.Build(
			this,
			creatureVisuals,
			idleClip: "normal_idle",
			actions: Array.Empty<CznBestiaryAction>(),
			hurtClip: "normal_hit",
			hurtSfx: null,
			dieClip: "normal_hit",
			dieSfx: null);
	}
}
