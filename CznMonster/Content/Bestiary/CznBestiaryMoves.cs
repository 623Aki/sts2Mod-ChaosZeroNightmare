using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznMonster.Content.Audio;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznMonster.Content.Bestiary;

/// <summary>图鉴按钮的一条数据：显示名 + 动画 clip + 音效 clip（音效为 <see cref="CznMonsterSfx"/> 的采样表键）。</summary>
public readonly record struct CznBestiaryAction(LocString Name, string Clip, string? SfxClip);

/// <summary>
/// CZN 怪物的图鉴招式表组装器（配合 <see cref="CznMonster.Registration.CznMonsterBestiaryPatch"/>）。
///
/// <para><b>为什么不用原生 <c>BestiaryMonsterMove.FromAnim</c></b>：原生流程只认
/// <c>die</c> / <c>hurt</c> / <c>revive</c> 这三个 <b>clip 名</b>（<c>MonsterModel.GenerateBestiaryMoveList</c>
/// 里的 <c>HasAnimation("die")</c> 探测），而 CZN 骨架的死亡动作叫
/// <c>normal_death</c> / <c>stance1_death</c>，于是图鉴里永远没有「死亡」按钮；
/// 且 <c>FromAnim</c> 播完会接回 <c>idle_loop</c>，CZN 的待机叫 <c>normal_idle</c>，接错了还会刷警告。</para>
///
/// <para><b>为什么不用「状态招式」（stateId）</b>：那条路会让图鉴执行 <c>MonsterModel.PerformMove()</c>，
/// 即真跑一遍战斗逻辑（判目标、加 Power、召唤增援）。图鉴里的战斗状态是
/// <c>NullCombatState</c>，这些指令既无意义又容易抛异常。这里改成纯演出：
/// 直接对 Spine 播 clip + 播一条采样，零游戏逻辑，行为与点按钮的预期一致。</para>
///
/// <para><b>音效为什么要显式给</b>：<c>NBestiary.PlayMoveAnim</c> 的 <c>FromAnim</c> 分支用
/// <c>NAudioManager.PlayOneShot</c> 播 FMOD 事件，而本 mod 的采样是本地 ogg（占位路径由
/// <c>SfxCmd.Play</c> 前缀补丁接管，那条链不经过 <c>PlayOneShot</c>）。所以这里统一调
/// <see cref="CznMonsterSfx.Play(MonsterModel, string, float)"/>，与战斗里同一张采样表。</para>
/// </summary>
public static class CznBestiaryMoves
{
	/// <summary>「受伤」按钮固定取原生图鉴表的通用名。</summary>
	public static LocString HurtName => new("bestiary", "ACTION_NAME.hurt");

	/// <summary>「死亡」按钮固定取原生图鉴表的通用名。</summary>
	public static LocString DieName => new("bestiary", "ACTION_NAME.die");

	/// <summary>
	/// 组装一张图鉴招式表：先按顺序放招式，最后固定补「受伤」「死亡」两条
	/// （顺序与原生一致，死亡永远在最后一格）。
	/// </summary>
	/// <param name="owner">怪物模型（只用于取音效表键与日志）。</param>
	/// <param name="visuals">图鉴布局刚创建出来的视觉节点，按钮闭包直接操作它。</param>
	/// <param name="idleClip">待机 clip：非死亡动作播完接回它。</param>
	/// <param name="actions">招式表（子类按自己的 clip / 采样填）。</param>
	/// <param name="hurtClip">受击 clip（CZN 为 <c>normal_hit</c> / <c>stance1_hit</c>）。</param>
	/// <param name="hurtSfx">受击采样键；无采样传 null（静音侧写）。</param>
	/// <param name="dieClip">死亡 clip（CZN 为 <c>normal_death</c> / <c>stance1_death</c>）。</param>
	/// <param name="dieSfx">死亡采样键；无采样传 null。</param>
	public static List<BestiaryMonsterMove> Build(
		MonsterModel owner,
		NCreatureVisuals? visuals,
		string idleClip,
		IReadOnlyList<CznBestiaryAction> actions,
		string hurtClip,
		string? hurtSfx,
		string dieClip,
		string? dieSfx)
	{
		List<BestiaryMonsterMove> list = new(actions.Count + 2);
		foreach (CznBestiaryAction action in actions)
		{
			list.Add(Anim(owner, visuals, action.Name, action.Clip, action.SfxClip, idleClip));
		}

		// 受伤 / 死亡：与原生一样在播之前掐掉所有循环音
		list.Add(Anim(owner, visuals, HurtName, hurtClip, hurtSfx, idleClip).StopOtherSfx());
		list.Add(Anim(owner, visuals, DieName, dieClip, dieSfx, idleClip: null).StopOtherSfx());
		return list;
	}

	/// <summary>
	/// 单条演出：播 <paramref name="clip"/>（不循环），<paramref name="idleClip"/> 非空时排队接回待机，
	/// 同时播一条采样。整条是纯表现，不触发任何战斗逻辑。
	/// </summary>
	private static BestiaryMonsterMove Anim(
		MonsterModel owner,
		NCreatureVisuals? visuals,
		LocString name,
		string clip,
		string? sfxClip,
		string? idleClip)
	{
		return BestiaryMonsterMove.FromAction(name, () =>
		{
			// 图鉴反复切换条目时旧节点已被释放，这里必须判有效性（SpineAnimation 本身是 null-safe 包装）
			if (visuals != null && GodotObject.IsInstanceValid(visuals))
			{
				SpineAnimationAccess animation = visuals.SpineAnimation;
				animation.SetAnimation(clip, loop: false);
				if (!string.IsNullOrEmpty(idleClip))
				{
					animation.AddAnimation(idleClip, delay: 0f, loop: true);
				}
			}

			if (!string.IsNullOrEmpty(sfxClip))
			{
				CznMonsterSfx.Play(owner, sfxClip);
			}

			return Task.CompletedTask;
		});
	}
}
