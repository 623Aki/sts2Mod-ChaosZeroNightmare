using System;
using CznHeidemarie.Integration;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace CznHeidemarie.Registration;

/// <summary>
/// 把本角色的「占位 FMOD 事件」挡掉 —— 战斗音效由我们自己的 ogg 播放器负责。
///
/// <para>起因：<c>CreatureCmd.TriggerAnim</c> 对 <c>Attack</c> / <c>Cast</c> /
/// <c>PowerUp</c> 三个动作会播 <c>CharacterModel.AttackSfx</c> 之类的
/// <b>FMOD 事件</b>；而本角色的真实音效是素材区导出的 Godot ogg
/// （见 <see cref="HeidemarieCombatAudio"/>），两套音频系统接不到一起。
/// 于是 <see cref="HeidemarieSfxRedirect"/> 把这些属性改成带
/// <c>/characters/heidemarie/</c> 前缀的占位路径，这里按前缀拦下：
/// 原生事件不发声（我们的 ogg 已经放了），也不会让 FMOD 去解析不存在的事件刷警告。</para>
///
/// <para>判定只看事件路径前缀，所以队伍里真正的战士（Ironclad）音效不受影响。</para>
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class HeidemariePlaceholderSfxSuppressPatch
{
	/// <summary>返回 <c>false</c> 表示整段跳过原实现（即不播放 FMOD 事件）。</summary>
	private static bool Prefix(string sfx)
	{
		try
		{
			if (!HeidemarieSfxRedirect.IsPlaceholderSfx(sfx))
			{
				return true;
			}

			// 占位事件按后缀分派到本工程的 ogg。
			// 选角语音（CharacterSelectSfx）这条链路由 NCharacterSelectScreen 直接
			// SfxCmd.Play(...) 触发，**不带 Player**，所以只能按事件名找人接手。
			if (sfx.EndsWith("_select", StringComparison.Ordinal))
			{
				HeidemarieCombatAudio.PlayCharacterSelectVoice();
			}

			return false;
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("占位音效拦截异常，交回原生播放：" + ex.Message);
			return true;
		}
	}
}

/// <summary>
/// 玩家死亡音效的占位拦截 —— 补上 <c>SfxCmd.Play</c> 那一处覆盖不到的链路。
///
/// <para><b>为什么必须单独补</b>：玩家死亡走的是
/// <c>NCreature.StartDeathAnim</c> → <c>SfxCmd.PlayDeath(Player)</c>，
/// 而 <c>PlayDeath</c> 内部<b>直接</b>调
/// <c>NAudioManager.PlayOneShot(player.Character.DeathSfx)</c>，<b>不经过</b>
/// <c>SfxCmd.Play</c> —— 所以 <see cref="HeidemariePlaceholderSfxSuppressPatch"/>
/// 拦不到它，占位路径 <c>event:/sfx/characters/heidemarie/heidemarie_die</c>
/// 会直达 FMOD 并刷 <c>cannot find sfx path</c>。</para>
///
/// <para>拦下后不发声，改由 <see cref="HeidemarieCombatAudio.OnPlayerDeath"/>
/// 播放本角色自己的死亡音效 —— 占位路径的设计意图本就是「让我们的 ogg 接管」。</para>
///
/// <para>判定只看 <c>DeathSfx</c> 是不是占位路径，而占位路径只由本角色的
/// <c>get_DeathSfx</c> 补丁产生，所以队伍里真正的战士（Ironclad）不受影响。</para>
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), new[] { typeof(Player) })]
internal static class HeidemariePlayerDeathSfxSuppressPatch
{
	/// <summary>返回 <c>false</c> 表示整段跳过原实现（即不播放 FMOD 占位事件）。</summary>
	private static bool Prefix(Player player)
	{
		try
		{
			if (!HeidemarieSfxRedirect.IsPlaceholderSfx(player?.Character?.DeathSfx))
			{
				return true;
			}

			HeidemarieCombatAudio.OnPlayerDeath(player);
			return false;
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("死亡音效拦截异常，交回原生播放：" + ex.Message);
			return true;
		}
	}
}

/// <summary>
/// 「攻击牌全屏演出时机」的挂载点。
///
/// <para><b>为什么挂在 <c>Hook.ModifyAttackHitCount</c> 上</b>：
/// 这条钩子在 <c>AttackCommand.Execute</c> 里紧跟 <c>Hook.BeforeAttack</c> 调用，
/// 是「攻击指令已经组好、骨架动作还没播、伤害还没结算」的那个时刻 ——
/// 恰好是全屏演出决定要不要接管的时机（参考 mod Nine 也挂在同一处）。
/// 挂 <c>CardPlayCommittedEvent</c> 拿不到 <c>AttackCommand</c>，
/// 也就做不了「可击杀敌人才播」所需的致死预判。</para>
///
/// <para>判定完全交给 CznCore 的现成策略：<c>Always</c> 直接放行；
/// <c>LethalOnly</c> 走致死预判，随机目标 / 有反伤类钩子等「无法可靠预判」
/// 的情况一律判为不可放行，退回原生演出 —— 与面板上的说明一致。</para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class HeidemarieAttackPresentationPatch
{
	/// <summary>取到攻击指令后决定全屏演出是否接管。</summary>
	private static void Postfix(ICombatState combatState, AttackCommand attackCommand, decimal __result)
	{
		try
		{
			if (__result <= 0m || attackCommand.ModelSource is not CardModel card)
			{
				return;
			}

			if (!HeidemariePresentationHost.IsLocalHeidemarie(card.Owner))
			{
				return;
			}

			// 记账本次攻击指令的来源牌：它紧邻该指令的 CreatureCmd.TriggerAnim，
			// 战斗音频据此做「一次出牌只响一句台词」的去重 ——
			// AoE 逐敌攻击（剑之乐园）会为每个敌人各走一次 TriggerAnim，
			// 不去重就会按敌人数重复播语音。
			HeidemarieCombatAudio.NoteAttackCard(card);

			HeidemarieCinematicRuntime.TryBeginFromAttack(card, attackCommand, combatState, (int)__result);
		}
		catch (Exception ex)
		{
			// 演出判定出问题只影响观感，绝不能影响伤害流程
			HeidemarieIntegration.Log.Warn("全屏演出判定异常，交回原生演出：" + ex.Message);
		}
	}
}

/// <summary>
/// 「额外粒子」的挂载点：卡牌拖尾是游戏自己实例化的，
/// 只能在 <c>NCardTrailVfx.Create</c> 返回后立刻按设置把粒子节点关掉。
/// </summary>
[HarmonyPatch(typeof(NCardTrailVfx), nameof(NCardTrailVfx.Create))]
internal static class HeidemarieTrailParticlePatch
{
	/// <summary>只处理海德玛丽的拖尾（其它角色的拖尾不归本 mod 管）。</summary>
	private static void Postfix(string characterTrailPath, NCardTrailVfx? __result)
	{
		try
		{
			if (__result is null
				|| string.IsNullOrEmpty(characterTrailPath)
				|| !characterTrailPath.Contains("heidemarie", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			HeidemarieTrailParticles.ApplyToTrail(__result);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("额外粒子开关异常：" + ex.Message);
		}
	}
}
