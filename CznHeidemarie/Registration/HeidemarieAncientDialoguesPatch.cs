using System.Collections.Generic;
using System.Reflection;
using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace CznHeidemarie.Registration;

/// <summary>
/// 给全部原生先古的对白表补上「海黛玛丽」的角色条目，
/// 与参考 mod（CznNineCharacter <c>NineAncientDialoguesPatch</c>）同一用途、同一做法。
///
/// <para><b>为什么必须有这个补丁</b>：<c>AncientDialogueSet.GetValidDialogues</c>
/// 先按角色 ID 查 <c>CharacterDialogues</c>，查不到再落到 ANY 对白 / repeating 池；
/// 三者皆空时 <c>NEventRoom</c> 会对空集合调 <c>Rng.Chaotic.NextItem</c> 直接抛异常。
/// 原生先古的 <c>DefineDialogues</c> 只给原生角色写了条目，海黛玛丽
/// 一进先古房间就会踩空。补一条 repeating 空对白即可兜底
/// （具体文案在 <c>localization/zhs/ancients.json</c>，见
/// <see cref="AncientDialogueSet.PopulateLocKeys"/> 的 key 规则）。</para>
///
/// <para><b>名单比参考 mod 多一个「建筑师」</b>：<see cref="TheArchitect"/> 的
/// <c>AgnosticDialogues</c> 为空且 <c>LoadDialogue</c> 禁用 ANY 回退，
/// 不补条目必然踩空；参考 mod 漏了它，这里补全。</para>
///
/// <para><b>零侵入保证</b>：用 <c>TryAdd</c> —— 原生角色（IRONCLAD 等）的条目已存在，
/// 不会被覆盖；其他 mod 角色同理。</para>
/// </summary>
[HarmonyPatch]
public static class HeidemarieAncientDialoguesPatch
{
	/// <summary>需要兼容的全部原生先古（参考 mod 的 8 个 + 建筑师）。</summary>
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.DeclaredMethod(typeof(Neow), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Darv), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Nonupeipe), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Orobas), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Pael), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Tanx), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Tezcatara), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(Vakuu), "DefineDialogues");
		yield return AccessTools.DeclaredMethod(typeof(TheArchitect), "DefineDialogues");
	}

	/// <summary>在对白表里补一条海黛玛丽的空对白（已存在则跳过）。</summary>
	private static void Postfix(AncientDialogueSet __result)
	{
		__result.CharacterDialogues.TryAdd(
			ModelDb.GetId(typeof(Heidemarie)).Entry,
			new List<AncientDialogue>(1)
			{
				new AncientDialogue("")
				{
					VisitIndex = 0,
					IsRepeating = true,
				},
			});
	}
}
