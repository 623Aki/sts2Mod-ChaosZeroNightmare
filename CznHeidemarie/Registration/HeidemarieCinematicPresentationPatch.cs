using System;
using System.Threading.Tasks;
using CznHeidemarie.Integration;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// 全屏演出与原生攻击结算「对表」的补丁 —— 修「伤害先出、动画后放」。
///
/// <para><b>原生节奏</b>（<c>AttackCommand.Execute</c>）：
/// 触发施法者动作 → <c>await CreatureCmd.TriggerAnim(attacker, 触发器, waitTime)</c>
/// → 结算伤害 → <c>await Hook.AfterAttack(...)</c>。
/// <c>waitTime</c> 是给原生骨架动作留的固定秒数，与全屏演出的时间轴毫无关系：
/// 演出刚起手，伤害就落了，观感上就是「先掉血、后放大招」。</para>
///
/// <para><b>参考 mod 的做法</b>（<c>NineCinematicRuntime.TryWaitForHit</c> /
/// <c>TipheraCombatCinematicRuntime.TryGetCoveredBodyWait</c>）：
/// 拦下这次动画等待，把「等原生 waitTime」换成「等演出的命中信号」；
/// 再在 <c>Hook.AfterAttack</c> 上补一个尾巴，等整段演出播完才结束这次出牌。
/// 本文件是同一套做法的本工程版本，闸门任务由
/// <see cref="HeidemarieCinematicRuntime"/> 提供。</para>
///
/// <para>接管有严格的触发条件（演出存在 + 路线与触发器配对 + 同一玩家），
/// 不满足就原样走游戏逻辑，所以普通打击 / 技能 / 敌方动作都不受影响。</para>
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
internal static class HeidemarieCinematicHitGatePatch
{
	/// <summary>
	/// 把「等原生动作播完」换成「等全屏演出的命中帧」。
	///
	/// <para>返回 <c>false</c> 表示整段跳过原实现（连同它的音效与骨架动画）：
	/// 演出已经接管画面，原生动作本就不可见。</para>
	/// </summary>
	private static bool Prefix(Creature creature, string triggerName, float waitTime, ref Task __result)
	{
		try
		{
			if (creature.IsDead)
			{
				return true;
			}

			Player? player = creature.Player;
			if (player is null || !HeidemarieCinematicRuntime.TryWaitForHit(player, triggerName, out Task hit))
			{
				return true;
			}

			__result = hit;
			return false;
		}
		catch (Exception ex)
		{
			// 补丁出问题必须退回原生节奏，绝不能把出牌卡死
			HeidemarieIntegration.Log.Warn("全屏演出命中闸门异常，交回原生等待：" + ex.Message);
			return true;
		}
	}
}

/// <summary>
/// 攻击尾巴：让这次攻击（以及它所在的出牌）等整段全屏演出收尾再结束。
///
/// <para>不做这一步的话，命中之后演出还剩 2~3 秒的余韵，
/// 而玩家的回合、敌人的动作已经继续，画面会叠在一起。</para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterAttack))]
internal static class HeidemarieCinematicTailPatch
{
	/// <summary>把原生返回值接到演出的收尾闸门上。</summary>
	private static void Postfix(AttackCommand command, ref Task __result)
	{
		try
		{
			if (command.ModelSource is not CardModel card)
			{
				return;
			}

			Task tail = HeidemarieCinematicRuntime.WaitForPresentationEnd(card);
			if (tail.IsCompleted)
			{
				return;
			}

			__result = AwaitTail(__result, tail);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("全屏演出收尾闸门异常，交回原生节奏：" + ex.Message);
		}
	}

	/// <summary>先等原生钩子跑完，再等演出收尾；原生异常照原样抛出。</summary>
	private static async Task AwaitTail(Task nativeHooks, Task cinematicTail)
	{
		await nativeHooks;
		await cinematicTail;
	}
}
