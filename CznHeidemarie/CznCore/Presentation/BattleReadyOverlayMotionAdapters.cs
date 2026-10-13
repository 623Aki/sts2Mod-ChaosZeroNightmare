namespace CznCore.Presentation;

internal static class BattleReadyOverlayMotionAdapters
{
	public static BattleReadyOverlayMotionAdapter CznSts2FallbackSlide()
	{
		return new BattleReadyOverlayMotionAdapter(0.3f, 0.8f, 170f, 0.38f, 0.18f);
	}
}
