using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CznCore.Multiplayer;

/// <summary>
/// 客户端 → 房主的「请把你的设置发给我」请求。
///
/// <para>存在的意义：房主开局时只广播一次设置，后加入的玩家会错过那一次；
/// 客户端在 run 建立后主动问一次，房主定向回一份
/// <see cref="CznSettingsSyncMessage"/>，就能覆盖「房主先开、我后进」的场景。</para>
///
/// <para><see cref="ShouldBroadcast"/> 必须是 false：这条消息只该到房主手里，
/// 不能再被房主转发出去。</para>
/// </summary>
internal struct CznSettingsSyncRequestMessage : INetMessage
{
	/// <summary>消息头魔数，与 <see cref="CznSettingsSyncMessage.MagicValue"/> 保持一致。</summary>
	public const uint MagicValue = CznSettingsSyncMessage.MagicValue;

	/// <summary>魔数，接收侧必须校验。</summary>
	public uint magic;

	/// <summary>请求方 mod id。</summary>
	public string modId;

	/// <summary>请求方的 NetId，便于房主定向回包。</summary>
	public ulong requesterNetId;

	/// <summary>只发给房主，不广播。</summary>
	public bool ShouldBroadcast => false;

	/// <summary>可靠传输。</summary>
	public NetTransferMode Mode => NetTransferMode.Reliable;

	/// <summary>低频消息。</summary>
	public LogLevel LogLevel => LogLevel.VeryDebug;

	/// <summary>可缓冲，避免在大厅 / 读盘窗口丢失。</summary>
	public bool ShouldBuffer => true;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteUInt(magic);
		writer.WriteString(modId ?? string.Empty);
		writer.WriteULong(requesterNetId);
	}

	public void Deserialize(PacketReader reader)
	{
		magic = reader.ReadUInt();
		modId = reader.ReadString();
		requesterNetId = reader.ReadULong();
	}
}
