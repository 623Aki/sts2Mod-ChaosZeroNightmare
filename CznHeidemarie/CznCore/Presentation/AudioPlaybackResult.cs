namespace CznCore.Presentation;

internal readonly record struct AudioPlaybackResult(bool Played, float DurationSeconds)
{
	public static AudioPlaybackResult NotPlayed { get; } = new AudioPlaybackResult(Played: false, 0f);
}
