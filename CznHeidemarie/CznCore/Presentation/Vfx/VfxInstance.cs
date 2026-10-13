using Godot;

namespace CznCore.Presentation.Vfx;

internal sealed class VfxInstance
{
	public bool Active;

	public Texture2D? Texture;

	public Rid TextureRid;

	public Vector2 Position;

	public float Rotation;

	public Vector2 Scale;

	public Color Modulate;

	public float Lifetime;

	public float Age;

	public Vector2 Size;

	public Rect2 SourceRect;

	public int SortingOrder;

	public bool AdditiveBlend;

	public VfxFadeMode FadeMode;

	public float ScaleFrom;

	public float ScaleTo;

	public Vector2 Velocity;

	public float AngularVelocity;

	public float CurrentAlpha;

	public void Reset()
	{
		Active = false;
		Texture = null;
		TextureRid = default;
		Age = 0f;
	}
}
