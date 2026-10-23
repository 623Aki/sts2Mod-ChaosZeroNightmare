using System;
using CznMonster.Settings;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace CznMonster.Content.Net;

/// <summary>
/// 「本局怪物参数」的房主权威同步。
///
/// <para><b>规则</b>：联机时 CZN 怪物 / 首领位置 / 自定义血量 / 行动计数<b>全部以房主为准</b>。
/// 客户端的内存快照由本类写入，逻辑侧一律通过 <c>MonsterSettings.EffectiveXxx</c> 读取，
/// 保证两端用的是同一份参数。</para>
///
/// <para><b>时序</b>：</para>
/// <list type="bullet">
///   <item>大厅建立（<c>StartRunLobby</c> 构造）→ 两端 <see cref="Attach"/>，客户端注册处理器；</item>
///   <item>房主点「出发」→ <c>BeginRunForAllPlayers</c> Prefix 里 <see cref="BroadcastIfHost"/>，
///   消息在 <c>LobbyBeginRunMessage</c> 之前发出（可靠通道 FIFO）；</item>
///   <item>run 建立（<c>RunManager.InitializeShared</c>）→ 再 <see cref="Attach"/> 一次（覆盖读档联机）；</item>
///   <item>断开连接 → 清空快照，回到本地设置。</item>
/// </list>
///
/// <para><b>绝不落盘</b>：快照只活在内存里，玩家的 <c>monster.cfg</c> 不会被房主的值改写。</para>
/// </summary>
internal static class CznMonsterConfigSyncRuntime
{
	private static readonly MessageHandlerDelegate<CznMonsterConfigMessage> Handler = OnReceived;

	private static readonly Action<NetErrorInfo> DisconnectedHandler = OnDisconnected;

	private static INetGameService? _net;

	private static bool _initialized;

	/// <summary>初始化（订阅本地设置变更，房主改设置时重推）。由 <c>MainFile.Initialize</c> 调用。</summary>
	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}
		_initialized = true;
		MonsterSettings.Changed += OnLocalSettingsChanged;
	}

	/// <summary>绑定当前网络服务并注册处理器（幂等）。</summary>
	public static void Attach(INetGameService? net)
	{
		if (!_initialized || net == null || ReferenceEquals(net, _net))
		{
			return;
		}
		Detach();
		_net = net;
		try
		{
			net.RegisterMessageHandler(Handler);
			net.Disconnected += DisconnectedHandler;
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 参数同步绑定失败：{ex.GetType().Name}: {ex.Message}");
			return;
		}
		if (!net.Type.IsMultiplayer())
		{
			// 单机：确保不残留上一局的房主参数
			MonsterSettings.ClearMultiplayerSnapshot();
			return;
		}
		if (net.Type == NetGameType.Host)
		{
			// 房主自己是权威：把本地设置写成快照，逻辑侧的 EffectiveXxx 才不会回落成 false
			MonsterSettings.ApplyMultiplayerSnapshot(MonsterSettings.BuildSnapshot());
			BroadcastIfHost();
		}
	}

	private static void Detach()
	{
		if (_net == null)
		{
			return;
		}
		try
		{
			_net.UnregisterMessageHandler(Handler);
			_net.Disconnected -= DisconnectedHandler;
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 参数同步解绑失败：{ex.GetType().Name}: {ex.Message}");
		}
		_net = null;
	}

	/// <summary>房主把当前参数广播给所有客户端；非房主 / 未连接时什么都不做。</summary>
	public static void BroadcastIfHost()
	{
		INetGameService? net = _net;
		if (net == null || net.Type != NetGameType.Host || !net.IsConnected)
		{
			return;
		}
		try
		{
			MonsterSettings.MultiplayerSnapshot snapshot = MonsterSettings.BuildSnapshot();
			CznMonsterConfigMessage message = new CznMonsterConfigMessage
			{
				magic = CznMonsterConfigMessage.MagicValue,
				cznMonstersEnabled = snapshot.CznMonstersEnabled,
				bossPosRandom = snapshot.BossPosRandom,
				bossPosSpecified = snapshot.BossPosSpecified,
				bossChapter = snapshot.BossChapter,
				bossSelect = snapshot.BossSelect,
				bossHpCustom = snapshot.BossHpCustom,
				bossHpValue = snapshot.BossHpValue,
				actionCountEnabled = snapshot.ActionCountEnabled,
				actionCountScope = snapshot.ActionCountScope
			};
			net.SendMessage(message);
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 参数广播失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void OnReceived(CznMonsterConfigMessage message, ulong senderId)
	{
		try
		{
			INetGameService? net = _net;
			if (net == null || message.magic != CznMonsterConfigMessage.MagicValue)
			{
				return;
			}
			if (net.Type != NetGameType.Client)
			{
				// 房主自己收到回声没有意义
				return;
			}
			MonsterSettings.ApplyMultiplayerSnapshot(new MonsterSettings.MultiplayerSnapshot(
				CznMonstersEnabled: message.cznMonstersEnabled,
				BossPosRandom: message.bossPosRandom,
				BossPosSpecified: message.bossPosSpecified,
				BossChapter: message.bossChapter,
				BossSelect: message.bossSelect,
				BossHpCustom: message.bossHpCustom,
				BossHpValue: message.bossHpValue,
				ActionCountEnabled: message.actionCountEnabled,
				ActionCountScope: message.actionCountScope));
			MainFile.Log.Info($"CznMonster 已应用房主参数（CZN 怪物={message.cznMonstersEnabled}，行动计数={message.actionCountEnabled}）。");
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 参数应用失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void OnLocalSettingsChanged()
	{
		INetGameService? net = _net;
		if (net == null || net.Type != NetGameType.Host)
		{
			return;
		}
		// 房主改设置：先刷新自己的权威快照，再推给客户端
		MonsterSettings.ApplyMultiplayerSnapshot(MonsterSettings.BuildSnapshot());
		BroadcastIfHost();
	}

	private static void OnDisconnected(NetErrorInfo info)
	{
		MonsterSettings.ClearMultiplayerSnapshot();
		Detach();
	}
}
