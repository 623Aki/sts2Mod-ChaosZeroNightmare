using Godot;

namespace CznCore.Presentation;

internal static class CharacterSkinSurfaceDefaults
{
	public const float CznCombatBodySpineScaleMultiplier = 4.55f;

	private const float CznCombatBodyReferenceSceneScale = 0.29f;

	public static readonly Vector2 CznCombatPositionOffset = Vector2.Zero;

	public static readonly Vector2 CznRestSiteBodyOffset = new Vector2(0f, 178f);

	public static readonly MerchantSurfaceProfile CznMerchantSurface = new MerchantSurfaceProfile("stop", new Vector2(2.021f, 2.021f));

	public static readonly RestSiteSurfaceProfile CznRestSiteSurface = new RestSiteSurfaceProfile(ApplySceneLayout: true, Vector2.Zero, new Vector2(3.75f, 3.75f), new RestSiteControlRectProfile(0f, 0f, 0f, 0f), new RestSiteControlRectProfile(-153f, -350f, 267f, 320f), new RestSiteControlRectProfile(-155f, -351f, 266f, 332f), new RestSiteControlRectProfile(209.209f, -317.103f, 209.209f, -317.103f), new RestSiteControlRectProfile(-73.6836f, -324.997f, -73.6836f, -324.997f));

	public static Vector2 ResolveCznCombatBodySpineScale(Vector2 nativeScale, float scaleMultiplier)
	{
		float scale = 0.29f * scaleMultiplier;
		return new Vector2(ApplyAxisSign(nativeScale.X, scale), ApplyAxisSign(nativeScale.Y, scale));
	}

	private static float ApplyAxisSign(float nativeAxisScale, float scale)
	{
		if (!(nativeAxisScale < 0f))
		{
			return scale;
		}
		return 0f - scale;
	}
}
