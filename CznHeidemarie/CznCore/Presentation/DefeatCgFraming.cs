using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal static class DefeatCgFraming
{
	internal const float FitRatio = 0.9f;

	internal static Rect2 ContentRect(Texture2D texture)
	{
		Rect2 rect = new Rect2(Vector2.Zero, texture.GetSize());
		using Image image = texture.GetImage();
		if (image == null || (image.IsCompressed() && image.Decompress() != Error.Ok))
		{
			return rect;
		}
		Rect2I usedRect = image.GetUsedRect();
		return usedRect.HasArea() ? new Rect2(usedRect.Position, usedRect.Size) : rect;
	}

	internal static IReadOnlyList<DefeatCgMotionLayerProfile> Reframe(IReadOnlyList<DefeatCgMotionLayerProfile> layers, Vector2 viewport, Vector2 textureSize, Rect2 content, float baseScale)
	{
		DefeatCgMotionKeyProfile defeatCgMotionKeyProfile = null;
		foreach (DefeatCgMotionLayerProfile layer in layers)
		{
			if (!layer.Additive && layer.Keyframes.Count != 0)
			{
				DefeatCgMotionKeyProfile defeatCgMotionKeyProfile2 = Sample(layer.Keyframes, 0.5f);
				if (defeatCgMotionKeyProfile == null || defeatCgMotionKeyProfile2.Alpha > defeatCgMotionKeyProfile.Alpha)
				{
					defeatCgMotionKeyProfile = defeatCgMotionKeyProfile2;
				}
			}
		}
		Vector2 vector = content.Size * baseScale * (defeatCgMotionKeyProfile?.ScaleMultiplier ?? 0f);
		if (defeatCgMotionKeyProfile == null || viewport.X <= 0f || viewport.Y <= 0f || vector.X <= 0.001f || vector.Y <= 0.001f)
		{
			return layers;
		}
		float num = MathF.Min(viewport.X / vector.X, viewport.Y / vector.Y) * 0.9f;
		Vector2 vector2 = (content.GetCenter() - textureSize * 0.5f) * (baseScale * defeatCgMotionKeyProfile.ScaleMultiplier) / viewport;
		List<DefeatCgMotionLayerProfile> list = new List<DefeatCgMotionLayerProfile>(layers.Count);
		foreach (DefeatCgMotionLayerProfile layer2 in layers)
		{
			List<DefeatCgMotionKeyProfile> list2 = new List<DefeatCgMotionKeyProfile>(layer2.Keyframes.Count);
			foreach (DefeatCgMotionKeyProfile keyframe in layer2.Keyframes)
			{
				list2.Add(keyframe with
				{
					OffsetXRatio = (keyframe.OffsetXRatio - defeatCgMotionKeyProfile.OffsetXRatio - vector2.X) * num,
					OffsetYRatio = (keyframe.OffsetYRatio - defeatCgMotionKeyProfile.OffsetYRatio - vector2.Y) * num,
					ScaleMultiplier = keyframe.ScaleMultiplier * num
				});
			}
			list.Add(layer2 with
			{
				Keyframes = list2
			});
		}
		return list;
	}

	private static DefeatCgMotionKeyProfile Sample(IReadOnlyList<DefeatCgMotionKeyProfile> keys, float time)
	{
		if (time <= keys[0].Offset)
		{
			return keys[0];
		}
		for (int i = 1; i < keys.Count; i++)
		{
			DefeatCgMotionKeyProfile defeatCgMotionKeyProfile = keys[i];
			if (!(time > defeatCgMotionKeyProfile.Offset))
			{
				DefeatCgMotionKeyProfile defeatCgMotionKeyProfile2 = keys[i - 1];
				float amount = Math.Clamp((time - defeatCgMotionKeyProfile2.Offset) / MathF.Max(1E-05f, defeatCgMotionKeyProfile.Offset - defeatCgMotionKeyProfile2.Offset), 0f, 1f);
				return new DefeatCgMotionKeyProfile(time, float.Lerp(defeatCgMotionKeyProfile2.OffsetXRatio, defeatCgMotionKeyProfile.OffsetXRatio, amount), float.Lerp(defeatCgMotionKeyProfile2.OffsetYRatio, defeatCgMotionKeyProfile.OffsetYRatio, amount), float.Lerp(defeatCgMotionKeyProfile2.ScaleMultiplier, defeatCgMotionKeyProfile.ScaleMultiplier, amount), float.Lerp(defeatCgMotionKeyProfile2.Alpha, defeatCgMotionKeyProfile.Alpha, amount));
			}
		}
		return keys[keys.Count - 1];
	}
}
