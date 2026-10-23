using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CznCore.Multiplayer;

/// <summary>
/// 房主 → 所有客户端的「CZN 演出设置」下发消息。
///
/// <para>联机时必须「以房主为标准」的那批设置（演出开关类）通过它同步；
/// 音量 / 亮度 / 不透明度这类本地感官参数**不走这条消息**，见
/// <c>CznSettingsSyncRuntime</c> 顶部的分类说明。</para>
///
/// <para>载荷用「扁平 key=value 文本」而不是逐字段序列化，理由：
/// 设置项有二十多个且会随版本增删，逐字段写 <see cref="Serialize"/> 每加一项就要改两处，
/// 漏一处就是静默不同步；而文本格式与磁盘 <c>settings.cfg</c> 完全一致，
/// 可以直接复用现有的解析器，未知键自动忽略，新旧版本混装也不会炸。</para>
///
/// <para><b>为什么是 struct</b>：<c>NetMessageBus</c> 用
/// <c>Activator.CreateInstance(type)</c> 造实例，struct 的默认无参构造必定可调用，
/// 比 internal class 更安全。</para>
/// </summary>
internal struct CznSettingsSyncMessage : INetMessage
{
	/// <summary>消息头魔数（'CZN1'）。用于识别「解到了错位的消息类型」并安全丢弃。</summary>
	public const uint MagicValue = 0x435A4E31u;

	/// <summary>魔数，接收侧必须校验。</summary>
	public uint magic;

	/// <summary>房主设置的单调递增版本号，客户端丢弃比已应用版本更旧的包。</summary>
	public int revision;

	/// <summary>发送者（房主）的 NetId，接收侧用于确认「确实来自房主」。</summary>
	public ulong hostNetId;

	/// <summary>发送方 mod id（如 <c>CznHeidemarie</c>），避免多个 CZN mod 互相串味。</summary>
	public string modId;

	/// <summary>角色 + 全局的演出设置文本（<c>global.xxx=v</c> / <c>characters.&lt;id&gt;.xxx=v</c>）。</summary>
	public string settingsText;

	/// <summary>队友演出设置的文本（<c>global.teammate_*</c>）。</summary>
	public string multiplayerText;

	/// <summary>先发给房主，再由房主转发给其它客户端。</summary>
	public bool ShouldBroadcast => true;

	/// <summary>演出设置必须完整送达，走可靠传输。</summary>
	public NetTransferMode Mode => NetTransferMode.Reliable;

	/// <summary>设置变更频率很低，用 VeryDebug 避免刷日志。</summary>
	public LogLevel LogLevel => LogLevel.VeryDebug;

	/// <summary>换场景 / 读盘窗口先缓冲，避免刚开始战斗就丢包。</summary>
	public bool ShouldBuffer => true;

	public void Serialize(PacketWriter writer)
	{
		writer.WriteUInt(magic);
		writer.WriteInt(revision);
		writer.WriteULong(hostNetId);
		writer.WriteString(modId ?? string.Empty);
		writer.WriteString(settingsText ?? string.Empty);
		writer.WriteString(multiplayerText ?? string.Empty);
	}

	public void Deserialize(PacketReader reader)
	{
		magic = reader.ReadUInt();
		revision = reader.ReadInt();
		hostNetId = reader.ReadULong();
		modId = reader.ReadString();
		settingsText = reader.ReadString();
		multiplayerText = reader.ReadString();
	}
}
