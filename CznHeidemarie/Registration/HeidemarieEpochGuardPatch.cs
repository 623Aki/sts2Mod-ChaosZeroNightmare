using CznHeidemarie.Characters.Heidemarie;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CznHeidemarie.Registration;

/// <summary>
/// Heidemarie 的 Epoch 检查守卫工具。
///
/// <para>游戏本体的 <c>ProgressSaveManager</c> 用硬编码的 <c>is</c> 链识别角色，
/// 只认 Ironclad / Silent / Regent / Defect / Necrobinder / Deprived 六个原版角色；
/// 认不出时会 <b>主动抛 <c>ArgumentOutOfRangeException("character", …)</c></b>，
/// 而不是返回 <c>null</c>。因此任何 mod 角色只要赢下一场精英战或 Boss 战，
/// 就会让 <c>UpdateAfterCombatWon</c> 中途抛出。</para>
///
/// <para>异常经 <c>TaskHelper.LogTaskExceptions</c> 吞掉，不会崩游戏，但会中断
/// <c>CombatManager.EndCombatInternal</c> 的后半段：成就检查、<c>SaveProgressFile</c>、
/// <c>CombatWon</c> / <c>CombatEnded</c> 事件、<c>ActionExecutor.Unpause()</c>
/// 与 <c>SetCombatState(NotInCombat)</c> 全部不执行，战斗收尾处于半截状态。</para>
///
/// <para>参见 <c>SlayTheSpire2_source/src/Core/Saves/Managers/ProgressSaveManager.cs</c>
/// 的 424 / 501 行（两处 throw）与 763-769 行（调用点）。
/// Nine 与 Tiphera 的 <c>*EpochGuardPatch.cs</c> 是同一问题的标准解法。</para>
/// </summary>
internal static class HeidemarieEpochGuard
{
	/// <summary>
	/// 仅当本地玩家是 Heidemarie 时跳过原实现；其它角色交回原实现，保持零侵入。
	/// </summary>
	/// <param name="localPlayer">正在被检查的本地玩家。</param>
	/// <returns>返回 <c>true</c> 表示继续执行原实现。</returns>
	internal static bool SkipForHeidemarie(Player localPlayer)
	{
		return localPlayer.Character is not Heidemarie;
	}
}

/// <summary>
/// 跳过 Heidemarie 的「击败 15 个精英」检查（原实现对未知角色抛异常）。
/// </summary>
[HarmonyPatch(
	typeof(global::MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager),
	"CheckFifteenElitesDefeatedEpoch")]
internal static class HeidemarieEliteEpochGuardPatch
{
	private static bool Prefix(Player localPlayer)
	{
		return HeidemarieEpochGuard.SkipForHeidemarie(localPlayer);
	}
}

/// <summary>
/// 跳过 Heidemarie 的「击败 15 个 Boss」检查（原实现对未知角色抛异常）。
/// </summary>
[HarmonyPatch(
	typeof(global::MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager),
	"CheckFifteenBossesDefeatedEpoch")]
internal static class HeidemarieBossEpochGuardPatch
{
	private static bool Prefix(Player localPlayer)
	{
		return HeidemarieEpochGuard.SkipForHeidemarie(localPlayer);
	}
}

/// <summary>
/// 跳过 Heidemarie 的角色解锁检查。原实现不会抛异常，但会去查
/// <c>HEIDEMARIE2_EPOCH</c> 等 mod 并不存在的 EpochId，刷
/// <c>EpochModel was not found :(</c> 错误日志。
/// </summary>
[HarmonyPatch(
	typeof(global::MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager),
	"ObtainCharUnlockEpoch")]
internal static class HeidemarieUnlockEpochGuardPatch
{
	private static bool Prefix(Player localPlayer)
	{
		return HeidemarieEpochGuard.SkipForHeidemarie(localPlayer);
	}
}