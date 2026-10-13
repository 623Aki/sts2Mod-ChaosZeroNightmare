using System;

namespace CznCore.Presentation;

internal static class SharedTeammateVoicePlaybackGate
{
	private const string LastPlayedTickKey = "CznCore.TeammateVoice.LastPlayedTick";

	public static bool Allows(float cooldownSeconds)
	{
		if (cooldownSeconds <= 0f)
		{
			return true;
		}
		AppDomain currentDomain = AppDomain.CurrentDomain;
		lock (currentDomain)
		{
			if (!(currentDomain.GetData("CznCore.TeammateVoice.LastPlayedTick") is long num))
			{
				return true;
			}
			return (float)(Environment.TickCount64 - num) >= cooldownSeconds * 1000f;
		}
	}

	public static void RecordSuccessfulPlayback()
	{
		AppDomain currentDomain = AppDomain.CurrentDomain;
		lock (currentDomain)
		{
			currentDomain.SetData("CznCore.TeammateVoice.LastPlayedTick", Environment.TickCount64);
		}
	}
}
