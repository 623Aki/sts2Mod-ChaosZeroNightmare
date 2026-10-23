using System;
using CznMonster.Content.Net;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace CznMonster.Registration;

// 说明：这里原本还有一个 `StartRunLobby` 构造函数的 patch，用来「大厅一建立就绑定」。
// 但 Harmony 对 `.ctor + 参数类型` 的类级声明解析失败（日志：Patching exception in method null），
// 而客户端有 `InitializeFromMessage`、房主有 `BeginRunForAllPlayers` 两个稳定挂载点，
// 已经覆盖同一时刻，所以直接删掉，避免出现「跳过补丁」的误导日志。

/// <summary>
/// 房主点「出发」时把参数广播出去。
///
/// <para><b>必须在原方法之前</b>：原方法第一件事就是 <c>SendMessage(LobbyBeginRunMessage)</c>，
/// 之后才 <c>BeginRunLocally</c>（客户端收到 BeginRun 后生成房间）。
/// 可靠通道 FIFO ⇒ 只要参数包先发出，客户端一定先应用参数再生成房间。</para>
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "BeginRunForAllPlayers")]
internal static class CznMonsterBeginRunBroadcastPatch
{
	private static void Prefix(StartRunLobby __instance)
	{
		// 顺手再 Attach 一次：即使构造函数的补丁没挂上（版本差异 / 挂载失败），
		// 这里也能拿到 NetService，保证房主的广播不会因为 _net 为空而静默失败。
		CznMonsterConfigSyncRuntime.Attach(__instance.NetService);
		CznMonsterConfigSyncRuntime.BroadcastIfHost();
	}
}

/// <summary>
/// 客户端加入大厅后补注册一次（构造函数的兜底：加入消息一定在房主广播参数之前到达）。
/// </summary>
[HarmonyPatch(typeof(StartRunLobby), "InitializeFromMessage")]
internal static class CznMonsterLobbyJoinedPatch
{
	private static void Postfix(StartRunLobby __instance)
	{
		CznMonsterConfigSyncRuntime.Attach(__instance.NetService);
	}
}

/// <summary>
/// run 的服务层就绪后再绑定一次（覆盖「读档联机」：那条路不走 <see cref="StartRunLobby"/>）。
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeShared")]
internal static class CznMonsterRunAttachPatch
{
	private static void Postfix(RunManager __instance)
	{
		CznMonsterConfigSyncRuntime.Attach(__instance.NetService);
	}
}
