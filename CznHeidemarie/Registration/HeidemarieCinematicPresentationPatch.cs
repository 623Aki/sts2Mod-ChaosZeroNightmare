using System;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Integration;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

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
/// 拦下这次动画等待，把「等原生 waitTime」换成「等演出的命中信号」。
/// 本文件是同一套做法的本工程版本，闸门任务由
/// <see cref="HeidemarieCinematicRuntime"/> 提供。</para>
///
/// <para><b>「等演出收尾」为什么不挂 <c>Hook.AfterAttack</c> 了</b>（2026-10-16 改动）：
/// 原先在 AfterAttack 上挂尾巴，让 Execute 等整段演出播完。但「剑之乐园」是
/// 逐敌循环发多个 <c>AttackCommand</c>，每个指令都被那条尾巴阻塞到演出结束，
/// 后续指令到来时上一段演出必然已收尾 —— 去重拦不住、命中闸门也接不上，
/// 最终按敌人数重演。现在改由终结牌自己的 <c>OnPlay</c> 末尾
/// <c>await HeidemarieCinematicRuntime.WaitForPresentationEnd(this)</c>：
/// 逐敌攻击在演出播放期间连续完成（伤害在命中帧落地），出牌整体仍等演出收尾。</para>
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
	///
	/// <para>顺带把「战斗音效 / 角色语音」挂在这里：动作名是唯一能同时覆盖
	/// 原生动作与演出接管的通用信号，而且接管时原实现整体被跳过
	/// （连同它自己的音效），所以必须在返回前先补上我们自己的音频。</para>
	/// </summary>
	private static bool Prefix(Creature creature, string triggerName, float waitTime, ref Task __result)
	{
		try
		{
			if (creature.Player?.Character is not Heidemarie)
			{
				return true;
			}

			if (creature.IsDead)
			{
				return true;
			}

			// 先发声：不管是原生动作还是演出接管，这一击/这一下都要有音效与语音
			HeidemarieCombatAudio.OnTriggerAnim(creature, triggerName);

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
