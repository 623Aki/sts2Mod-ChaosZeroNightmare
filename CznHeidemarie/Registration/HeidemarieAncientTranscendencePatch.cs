using System.Collections.Generic;
using CznHeidemarie.Characters.Heidemarie.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CznHeidemarie.Registration;

/// <summary>
/// 把「绝响 → 终焉」的淬炼关系注册进游戏原生的「太古之牙」（<see cref="ArchaicTooth"/>）
/// 遗物，使其与参考 mod（CznNineCharacter「反射 → 反射：零」）走同一条先古牌转变路径。
///
/// <para><b>为什么不再自写先古事件</b>：游戏原生已具备完整的先古牌转变机制 ——
/// 玩家获得太古之牙时，<see cref="ArchaicTooth.AfterObtained"/> 会自动在卡组里
/// 找第一张可转变的牌，用 <c>CardCmd.Transform</c> 换成对应的先古牌，
/// 并<strong>继承原牌的升级状态与附魔</strong>（见
/// <see cref="ArchaicTooth"/> 的 <c>GetTranscendenceTransformedCard</c>）。
/// 自写事件等于把这套机制重复实现一遍，故对齐参考 mod 的做法：只往转变表里加一项。</para>
///
/// <para><b>零侵入保证</b>：注入的键是「绝响」的 ModelId —— 只有海黛玛丽的卡组里
/// 才存在这张牌，其他角色拿到太古之牙时 <c>GetTranscendenceStarterCard</c>
/// 匹配不到本项，行为与原版完全一致。</para>
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), "get_TranscendenceUpgrades")]
public static class HeidemarieAncientTranscendencePatch
{
	/// <summary>在原生转变表末尾追加「绝响 → 终焉」。</summary>
	public static void Postfix(Dictionary<ModelId, CardModel> __result)
	{
		if (__result != null)
		{
			__result[ModelDb.Card<HeidemarieFinalEcho>().Id] = ModelDb.Card<HeidemarieTerminus>();
		}
	}
}
