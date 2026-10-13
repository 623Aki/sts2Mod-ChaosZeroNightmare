using Godot;

namespace CznCore.Presentation.Vfx;

internal struct VfxSpawnParams
{
	public Texture2D? Texture = null;

	public Vector2 Position = Vector2.Zero;

	public float Rotation = 0f;

	public Vector2 Scale = Vector2.One;

	public Color Modulate = Colors.White;

	public float Lifetime = 0.35f;

	public Vector2 Size = Vector2.Zero;

	public Rect2 SourceRect = new Rect2(0f, 0f, 0f, 0f);

	public int SortingOrder = 0;

	public bool AdditiveBlend = false;

	public VfxFadeMode FadeMode = VfxFadeMode.FadeOut;

	public float ScaleFrom = 1f;

	public float ScaleTo = 1f;

	public Vector2 Velocity = Vector2.Zero;

	public float AngularVelocity = 0f;

	public VfxSpawnParams()
	{
	}
}
