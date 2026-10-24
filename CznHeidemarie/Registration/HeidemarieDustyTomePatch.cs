using System.Collections.Generic;
using System.Linq;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CznHeidemarie.Registration;

/// <summary>
/// 「尘封魔典」（<see cref="DustyTome"/>）候选集判定。
///
/// <para><b>问题背景</b>：原版 <c>DustyTome.SetupForPlayer</c> 的候选集是
/// 「角色卡池里 <see cref="CardRarity.Ancient"/> 且 <b>不在</b>
/// <c>ArchaicTooth.TranscendenceCards</c> 里的牌」，随后直接
/// <c>AncientCard = NextItem(items).Id</c> —— 候选为空时 <c>NextItem</c> 返回 null，
/// 取 <c>.Id</c> 就抛 <c>NullReferenceException</c>。</para>
///
/// <para>海德玛丽卡池里 <b>唯一</b>的先古牌是「终焉」，而它又通过
/// <see cref="HeidemarieAncientTranscendencePatch"/> 注册进了
/// <c>ArchaicTooth.TranscendenceUpgrades</c>，于是被上面的过滤条件全部排除 ⇒
/// 候选恒为空。DARV（<see cref="Darv"/>）每名玩家实例有 50% 概率生成「尘封魔典」选项，
/// 生成时就会调一次 <c>SetupForPlayer</c>，因此每次掷中都会刷一条 NRE，
/// 并把该玩家这一页的选项列表整页打空。</para>
///
/// <para><b>本文件的处理（A 阶段）</b>：不改动先古牌体系，只在「本角色确实没有可用先古牌」时
/// <b>不给该角色出尘封魔典选项</b>，并跳过 <c>SetupForPlayer</c> 的崩溃路径。
/// 判定条件与后续补丁共用，因此将来若给卡池补一张「不在淬炼表里」的先古牌（B 阶段），
/// 这两个补丁都会<b>自动失效</b>，无需回滚。</para>
///
/// <para><b>为什么不直接把「终焉」从 <c>TranscendenceCards</c> 里摘掉</b>：该属性在整个游戏里
/// 只有 <c>DustyTome.SetupForPlayer</c> 一处使用（是「魔典黑名单」），摘掉后海德玛丽的候选集
/// 会立刻变成 [终焉]，魔典就能把「终焉」当先古牌直接送出来，破坏「终焉只能由太古之牙淬炼获得」的设定。</para>
/// </summary>
internal static class HeidemarieAncientCardAvailability
{
	/// <summary>
	/// 该玩家的角色卡池里，是否还有「尘封魔典」可以送出的先古牌。
	/// </summary>
	/// <param name="player">事件持有者；为 <c>null</c> 时按「有」处理（交给原版逻辑）。</param>
	/// <returns>
	/// <c>true</c>（有）⇒ 原版行为不受影响；
	/// <c>false</c>（无）⇒ 调用方应阻止尘封魔典出现在该玩家的事件里。
	/// </returns>
	/// <remarks>
	/// 只有 <see cref="Heidemarie"/> 才做空集判定：其它角色一律返回 <c>true</c>，
	/// 保持零侵入（原版/其它 mod 角色的尘封魔典行为完全不变）。
	/// </remarks>
	internal static bool HasEligibleAncientCard(Player? player)
	{
		if (player is null || player.Character is not Heidemarie)
		{
			return true;
		}

		// 复刻原版过滤条件：先古 + 不在太古之牙转变表的「结果牌」集合里。
		// 这里按 ModelId 比较（record 值相等），比原版的模型引用比较更稳（克隆体同样成立）。
		List<ModelId> transcendenceIds = ArchaicTooth.TranscendenceCards.Select(card => card.Id).ToList();
		foreach (CardModel card in player.Character.CardPool.GetUnlockedCards(
			player.UnlockState, player.RunState.CardMultiplayerConstraint))
		{
			if (card.Rarity == CardRarity.Ancient && !transcendenceIds.Contains(card.Id))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// DARV 选项过滤：本角色没有可用先古牌时，把「尘封魔典」这一项从初始选项里摘掉。
///
/// <para><see cref="Darv.GenerateInitialOptions"/> 在
/// <c>Rng.NextBool()</c> 为真时会把魔典选项追加进列表，且<b>先</b>调 <c>SetupForPlayer</c>。
/// 若不摘掉，即使跳过崩溃路径，选项文案里的 <c>{AncientCard.StringValue}</c> 也没有值，
/// 玩家真的选中时 <c>AfterObtained</c> 还会再炸一次。</para>
/// </summary>
[HarmonyPatch(typeof(Darv), "GenerateInitialOptions")]
internal static class HeidemarieDarvDustyTomeOptionPatch
{
	/// <summary>
	/// 只在本角色「无可用先古牌」时过滤，其它角色原样返回。
	/// </summary>
	private static void Postfix(Darv __instance, ref IReadOnlyList<EventOption> __result)
	{
		if (__result is null || __result.Count == 0)
		{
			return;
		}

		if (HeidemarieAncientCardAvailability.HasEligibleAncientCard(__instance.Owner))
		{
			return;
		}

		// EventOption.Relic 是公开属性，直接按遗物类型判定，不依赖本地化键文本。
		List<EventOption> filtered = __result.Where(option => option.Relic is not DustyTome).ToList();
		if (filtered.Count == __result.Count)
		{
			return;
		}

		__result = filtered;
		Integration.HeidemarieIntegration.Log.Info(
			$"DARV：本角色卡池无可用的先古牌（终焉被太古之牙转变表排除），已摘除「尘封魔典」选项"
			+ $"（{__result.Count + 1} → {__result.Count} 项）。");
	}
}

/// <summary>
/// 兜底：万一还有别的路径调到 <see cref="DustyTome.SetupForPlayer"/>，
/// 本角色空候选时直接跳过原方法，避免 <c>NextItem(空).Id</c> 的空引用。
/// </summary>
/// <remarks>
/// 与选项过滤共用同一个判定：只要卡池里出现可用的先古牌（B 阶段），本补丁自动不再介入。
/// </remarks>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
internal static class HeidemarieDustyTomeSetupGuardPatch
{
	/// <summary>
	/// 返回 <c>false</c> = 跳过原方法；判定条件不成立时放行。
	/// </summary>
	private static bool Prefix(Player player)
	{
		if (HeidemarieAncientCardAvailability.HasEligibleAncientCard(player))
		{
			return true;
		}

		// 保留原文案：说明这是数据侧的已知状态，而不是游戏逻辑出错。
		Integration.HeidemarieIntegration.Log.Warn(
			"尘封魔典 SetupForPlayer 已跳过：本角色卡池没有可用的先古牌（唯一先古牌「终焉」属太古之牙专属）。");
		return false;
	}
}
