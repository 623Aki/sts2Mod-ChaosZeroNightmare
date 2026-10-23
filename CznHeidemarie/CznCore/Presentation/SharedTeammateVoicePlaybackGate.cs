using System;

namespace CznCore.Presentation;

/// <summary>
/// 队友语音的播放冷却闸门。
///
/// <para><b>按 <c>Player.NetId</c> 分桶</b>：多人局里如果所有队友共用一个时间戳，
/// A 队友的一句台词会吃掉 B 队友的冷却窗口，表现为「某个队友永远没声音」。
/// 桶放在 <see cref="AppDomain"/> 数据槽而不是静态字典里，
/// 是为了让同一进程内多个 CZN mod（各带一份 CznCore）共享同一份记录。</para>
/// </summary>
internal static class SharedTeammateVoicePlaybackGate
{
	private const string TickKeyPrefix = "CznCore.TeammateVoice.LastPlayedTick.";

	/// <summary>该玩家现在是否已经过了冷却（<paramref name="cooldownSeconds"/> ≤ 0 表示不限制）。</summary>
	public static bool Allows(ulong netId, float cooldownSeconds)
	{
		if (cooldownSeconds <= 0f)
		{
			return true;
		}
		AppDomain currentDomain = AppDomain.CurrentDomain;
		lock (currentDomain)
		{
			if (!(currentDomain.GetData(TickKeyPrefix + netId) is long lastPlayed))
			{
				return true;
			}
			return (float)(Environment.TickCount64 - lastPlayed) >= cooldownSeconds * 1000f;
		}
	}

	/// <summary>记录该玩家刚播过一句。</summary>
	public static void RecordSuccessfulPlayback(ulong netId)
	{
		AppDomain currentDomain = AppDomain.CurrentDomain;
		lock (currentDomain)
		{
			currentDomain.SetData(TickKeyPrefix + netId, Environment.TickCount64);
		}
	}
}
