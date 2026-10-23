using System;
using System.Threading;
using CznCore.Presentation;
using CznCore.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace CznCore.Multiplayer;

/// <summary>
/// 联机设置同步运行时：让「演出开关类」设置在多人游戏里以房主为标准。
///
/// <para><b>同步范围（必须与 <c>CharacterSkinSettingsStore.ExportHostSyncedText</c> 保持一致）：</b></para>
/// <list type="bullet">
///   <item><b>同步</b>：全局演出开关、每角色演出开关 / 全屏演出时机 / 战斗表现模式、
///   队友演出设置（<c>teammate_*</c>）。这些决定「屏幕上该出现什么」，
///   两端不一致会直接导致观感分裂。</item>
///   <item><b>不同步</b>：特效音量 / 亮度 / 不透明度、角色语音音量与间隔曲线、
///   左侧立绘缩放与位置、选人背景、调试日志。这些是玩家本机的口味与性能偏好，
///   「房主的音量」不该决定「我的音量」。</item>
/// </list>
///
/// <para><b>生命周期</b>：<c>RunManager.InitializeShared</c> 之后（NetService 已就绪）由
/// <c>CznSettingsSyncPatch</c> 调用 <see cref="OnRunServiceReady"/>：
/// 房主广播一次并在设置变更时重播；客户端注册处理器并主动请求一次（覆盖后加入）。
/// 断线 / 结束 run 时清空覆盖并恢复本地设置。</para>
///
/// <para><b>绝不落盘</b>：房主的值只写进内存，<c>CharacterSkinSettingsStore</c> 会在接管期间
/// 关闭写磁盘，退出联机后完全回到自己的配置。</para>
/// </summary>
internal static class CznSettingsSyncRuntime
{
	/// <summary>本地设置变更后延迟多久再广播（合并连续拖动滑块产生的多次变更）。</summary>
	private const float BroadcastDebounceSeconds = 0.35f;

	private static readonly MessageHandlerDelegate<CznSettingsSyncMessage> SyncHandler = OnSyncReceived;

	private static readonly MessageHandlerDelegate<CznSettingsSyncRequestMessage> RequestHandler = OnRequestReceived;

	private static readonly Action<NetErrorInfo> DisconnectedHandler = OnNetworkDisconnected;

	private static CharacterSkinSettingsStore? _store;

	private static string _modId = string.Empty;

	private static INetGameService? _net;

	private static bool _initialized;

	private static bool _applyingRemote;

	private static int _revision;

	private static int _appliedRevision;

	private static int _broadcastGeneration;

	/// <summary>客户端索取请求的最小间隔（毫秒），防止本地连续改动刷屏。</summary>
	private const int RequestThrottleMilliseconds = 2000;

	private static long _lastRequestTicks;

	/// <summary>
	/// 初始化。每个 CZN mod 各调一次，传入自己的 mod id 与设置存储。
	/// </summary>
	/// <param name="modId">mod id（如 <c>CznHeidemarie</c>），用于过滤别的 CZN mod 的消息。</param>
	/// <param name="store">本 mod 的设置存储。</param>
	public static void Initialize(string modId, CharacterSkinSettingsStore store)
	{
		if (_initialized)
		{
			return;
		}
		_initialized = true;
		_modId = modId ?? string.Empty;
		_store = store ?? throw new ArgumentNullException(nameof(store));
		// 房主改设置后实时重播；本地变更事件在 store 的每次 Set 后触发
		_store.Changed += OnLocalSettingsChanged;
	}

	/// <summary>
	/// run 的服务层就绪（<c>RunManager.InitializeShared</c> 之后）时调用。
	/// 单机直接空转，且会先清掉上一局可能残留的接管状态。
	/// </summary>
	/// <param name="net">当前 run 的 <see cref="INetGameService"/>。</param>
	public static void OnRunServiceReady(INetGameService? net)
	{
		try
		{
			if (!_initialized || _store == null || net == null)
			{
				return;
			}
			if (!net.Type.IsMultiplayer())
			{
				// 单机：保证不残留上一局联机的覆盖值
				Detach();
				ClearHostSettings();
				return;
			}
			if (!ReferenceEquals(net, _net))
			{
				Detach();
				Attach(net);
			}
			if (net.Type == NetGameType.Host)
			{
				BroadcastNow();
			}
			else
			{
				SendRequest();
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync attach failed: " + SkinLog.FormatException(ex));
		}
	}

	private static void Attach(INetGameService net)
	{
		_net = net;
		net.RegisterMessageHandler(SyncHandler);
		net.RegisterMessageHandler(RequestHandler);
		net.Disconnected += DisconnectedHandler;
		_appliedRevision = 0;
	}

	private static void Detach()
	{
		if (_net == null)
		{
			return;
		}
		try
		{
			_net.UnregisterMessageHandler(SyncHandler);
			_net.UnregisterMessageHandler(RequestHandler);
			_net.Disconnected -= DisconnectedHandler;
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync detach failed: " + SkinLog.FormatException(ex));
		}
		_net = null;
	}

	private static void OnNetworkDisconnected(NetErrorInfo info)
	{
		// 断线后房主的值不再有效，直接回落到本地配置
		ClearHostSettings();
		Detach();
	}

	/// <summary>房主把当前设置广播给所有客户端。</summary>
	private static void BroadcastNow()
	{
		INetGameService? net = _net;
		CharacterSkinSettingsStore? store = _store;
		if (net == null || store == null || !_initialized)
		{
			return;
		}
		if (!net.IsConnected)
		{
			return;
		}
		try
		{
			// 房主关掉「以房主为准」时发一条空包，让客户端清掉已有的覆盖、回到本地设置
			bool followHost = store.TeammateUseHostSettings;
			_revision++;
			CznSettingsSyncMessage message = new CznSettingsSyncMessage
			{
				magic = CznSettingsSyncMessage.MagicValue,
				revision = _revision,
				hostNetId = net.NetId,
				modId = _modId,
				settingsText = followHost ? store.ExportHostSyncedText() : string.Empty,
				multiplayerText = followHost ? store.ExportHostSyncedMultiplayerText() : string.Empty
			};
			net.SendMessage(message);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync broadcast failed: " + SkinLog.FormatException(ex));
		}
	}

	/// <summary>客户端向房主要一份当前设置（后加入 / 重新打开开关时用）。</summary>
	private static void SendRequest()
	{
		INetGameService? net = _net;
		if (net == null || !net.IsConnected || net.Type != NetGameType.Client)
		{
			return;
		}
		// 节流：本地改设置也会触发 Changed，避免连续拖动滑块把请求刷成风暴
		long now = System.Environment.TickCount64;
		if (now - Interlocked.Read(ref _lastRequestTicks) < RequestThrottleMilliseconds)
		{
			return;
		}
		Interlocked.Exchange(ref _lastRequestTicks, now);
		try
		{
			CznSettingsSyncRequestMessage message = new CznSettingsSyncRequestMessage
			{
				magic = CznSettingsSyncRequestMessage.MagicValue,
				modId = _modId,
				requesterNetId = net.NetId
			};
			net.SendMessage(message);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync request failed: " + SkinLog.FormatException(ex));
		}
	}

	/// <summary>收到房主下发的设置。</summary>
	private static void OnSyncReceived(CznSettingsSyncMessage message, ulong senderId)
	{
		try
		{
			CharacterSkinSettingsStore? store = _store;
			INetGameService? net = _net;
			if (store == null || net == null || !_initialized)
			{
				return;
			}
			if (message.magic != CznSettingsSyncMessage.MagicValue)
			{
				// 解到了错位的消息类型（mod 消息 id 不匹配），安全丢弃
				return;
			}
			if (!string.Equals(message.modId, _modId, StringComparison.Ordinal))
			{
				// 其它 CZN mod 的设置，交给它自己的 runtime 处理
				return;
			}
			if (!store.TeammateUseHostSettings)
			{
				// 本机玩家选择了「不跟随房主」，收到也不应用
				return;
			}
			if (net.Type == NetGameType.Host)
			{
				// 房主收到自己的回声没有意义
				return;
			}
			if (TryGetHostNetId(out ulong hostNetId) && senderId != hostNetId)
			{
				SkinLog.Warn($"CZN settings sync ignored: sender {senderId} is not the host ({hostNetId}).");
				return;
			}
			if (store.HostSettingsOverrideActive && message.revision <= _appliedRevision)
			{
				// 乱序 / 重复包
				return;
			}
			string settingsText = message.settingsText ?? string.Empty;
			string multiplayerText = message.multiplayerText ?? string.Empty;
			if (settingsText.Length == 0 && multiplayerText.Length == 0)
			{
				// 空包 = 房主关掉了「以房主为准」，清掉覆盖回到本地
				_applyingRemote = true;
				try
				{
					store.ClearHostSyncedSettings();
					_appliedRevision = message.revision;
				}
				finally
				{
					_applyingRemote = false;
				}
				return;
			}
			_applyingRemote = true;
			try
			{
				store.ApplyHostSyncedSettings(settingsText, multiplayerText);
				_appliedRevision = message.revision;
			}
			finally
			{
				_applyingRemote = false;
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync apply failed: " + SkinLog.FormatException(ex));
		}
	}

	/// <summary>房主收到客户端的索取请求，定向补发一份。</summary>
	private static void OnRequestReceived(CznSettingsSyncRequestMessage message, ulong senderId)
	{
		try
		{
			INetGameService? net = _net;
			CharacterSkinSettingsStore? store = _store;
			if (net == null || store == null || !_initialized)
			{
				return;
			}
			if (message.magic != CznSettingsSyncRequestMessage.MagicValue)
			{
				return;
			}
			if (!string.Equals(message.modId, _modId, StringComparison.Ordinal))
			{
				return;
			}
			if (net.Type != NetGameType.Host || !net.IsConnected)
			{
				return;
			}
			// 房主没开「以房主为准」时回一条空包，让请求方清掉可能残留的覆盖
			bool followHost = store.TeammateUseHostSettings;
			CznSettingsSyncMessage reply = new CznSettingsSyncMessage
			{
				magic = CznSettingsSyncMessage.MagicValue,
				revision = ++_revision,
				hostNetId = net.NetId,
				modId = _modId,
				settingsText = followHost ? store.ExportHostSyncedText() : string.Empty,
				multiplayerText = followHost ? store.ExportHostSyncedMultiplayerText() : string.Empty
			};
			net.SendMessage(reply, senderId);
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync reply failed: " + SkinLog.FormatException(ex));
		}
	}

	private static void OnLocalSettingsChanged(string? characterId)
	{
		CharacterSkinSettingsStore? store = _store;
		if (store == null || _applyingRemote)
		{
			return;
		}
		if (!store.TeammateUseHostSettings)
		{
			// 关掉「以房主为准」：清掉覆盖，回到本地
			ClearHostSettings();
			return;
		}
		if (_net == null)
		{
			return;
		}
		if (_net.Type == NetGameType.Host)
		{
			ScheduleBroadcast();
		}
		else if (_net.Type == NetGameType.Client && !store.HostSettingsOverrideActive)
		{
			// 本地目前没有被覆盖（刚重新打开开关 / 房主还没下发）→ 向房主要一份。
			// 已经在跟随房主时不再请求，避免本地调音量之类的动作触发无谓往返。
			SendRequest();
		}
	}

	/// <summary>房主侧防抖广播：连续拖滑块只发最后一次。</summary>
	private static void ScheduleBroadcast()
	{
		int generation = Interlocked.Increment(ref _broadcastGeneration);
		try
		{
			if (!(Engine.GetMainLoop() is SceneTree sceneTree))
			{
				BroadcastNow();
				return;
			}
			sceneTree.CreateTimer(BroadcastDebounceSeconds).Timeout += () =>
			{
				if (generation == Volatile.Read(in _broadcastGeneration))
				{
					BroadcastNow();
				}
			};
		}
		catch (Exception ex)
		{
			SkinLog.Warn("CZN settings sync deferred broadcast failed: " + SkinLog.FormatException(ex));
			BroadcastNow();
		}
	}

	/// <summary>清空房主覆盖并恢复本地设置（断线、退出联机、关闭开关时调用）。</summary>
	private static void ClearHostSettings()
	{
		CharacterSkinSettingsStore? store = _store;
		if (store == null || !store.HostSettingsOverrideActive)
		{
			return;
		}
		_applyingRemote = true;
		try
		{
			store.ClearHostSyncedSettings();
			_appliedRevision = 0;
		}
		finally
		{
			_applyingRemote = false;
		}
	}

	/// <summary>
	/// 取房主的 NetId。大厅 slot 0 由房主先占位（<c>AddLocalHostPlayer</c>），
	/// 所以 <c>RunLobby.Players[0]</c> 就是房主。
	/// 取不到时返回 false，调用方退化为「只校验魔数 + mod id」。
	/// </summary>
	private static bool TryGetHostNetId(out ulong hostNetId)
	{
		hostNetId = 0uL;
		try
		{
			var players = RunManager.Instance?.RunLobby?.Players;
			if (players == null || players.Count == 0)
			{
				return false;
			}
			hostNetId = players[0].id;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
