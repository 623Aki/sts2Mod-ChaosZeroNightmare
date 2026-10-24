using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CznMonster.Content.Net;

/// <summary>
/// 房主 → 客户端的「本局怪物参数」下发消息。
///
/// <para><b>为什么需要它</b>：CznMonster 的设置（是否启用 CZN 怪物、首领位置、自定义血量、
/// 行动计数）全部参与<b>战斗与地图生成</b>。游戏本体是「双端确定性模拟 + 校验和」，
/// 一旦两端参数不同，`GenerateRooms` 会抽出不同 Boss、`MinInitialHp` 会算出不同血量，
/// `ChecksumTracker` 立刻判定 `StateDiverged` → 客户端掉线重开。</para>
///
/// <para><b>什么时候发</b>：房主在 <c>StartRunLobby.BeginRunForAllPlayers</c> 的 Prefix 里发，
/// 也就是在 <c>LobbyBeginRunMessage</c> 之前。可靠通道是 FIFO 的，所以客户端一定
/// 先应用参数、再生成房间（`GenerateRooms` 在 `SetUpNewMultiplayer` 里）。</para>
///
/// <para><b>为什么是 struct</b>：<c>NetMessageBus</c> 用 <c>Activator.CreateInstance(type)</c>
/// 造实例，struct 的默认无参构造必定可调用。</para>
/// </summary>
internal struct CznMonsterConfigMessage : INetMessage
{
	/// <summary>消息头魔数（'CZM1'），接收侧必须校验。</summary>
	public const uint MagicValue = 0x435A4D31u;

	public uint magic;

	public bool cznMonstersEnabled;

	public bool bossPosSpecified;

	public int bossChapter;

	public int bossSelect;

	public bool bossHpCustom;

	public int bossHpValue;

	public bool actionCountEnabled;

	public int actionCountScope;

	/// <summary>先发给房主，再由房主转发给其它客户端。</summary>
	public bool ShouldBroadcast => true;

	/// <summary>参数必须完整送达，走可靠传输。</summary>
	public NetTransferMode Mode => NetTransferMode.Reliable;

	/// <summary>低频消息。</summary>
	public LogLevel LogLevel => LogLevel.VeryDebug;

	/// <summary>可缓冲：换场景 / 读盘的窗口里不丢包。</summary>
	public bool ShouldBuffer => true;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteUInt(magic);
		writer.WriteBool(cznMonstersEnabled);
		writer.WriteBool(bossPosSpecified);
		writer.WriteInt(bossChapter);
		writer.WriteInt(bossSelect);
		writer.WriteBool(bossHpCustom);
		writer.WriteInt(bossHpValue);
		writer.WriteBool(actionCountEnabled);
		writer.WriteInt(actionCountScope);
	}

	public void Deserialize(PacketReader reader)
	{
		magic = reader.ReadUInt();
		cznMonstersEnabled = reader.ReadBool();
		bossPosSpecified = reader.ReadBool();
		bossChapter = reader.ReadInt();
		bossSelect = reader.ReadInt();
		bossHpCustom = reader.ReadBool();
		bossHpValue = reader.ReadInt();
		actionCountEnabled = reader.ReadBool();
		actionCountScope = reader.ReadInt();
	}
}
