using System;
using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal sealed record DefeatPresentationProfile(string CgTexturePath, string CollapseSfxPath, float CgStartOverlapSeconds, float DimAlpha, float DimFadeSeconds, float EffectIntroDelaySeconds, float CgIntroDelaySeconds, float CgFadeInSeconds, float CgMotionSeconds, float CgStartScaleMultiplier, float CgEndScaleMultiplier, Vector2 CgStartOffset, Vector2 CgEndOffset, float CollapseSfxDelaySeconds, float CollapseSfxVolumeDb, ulong DuplicateSuppressMs, IReadOnlyList<DefeatEffectLayerProfile> EffectLayers, IReadOnlyList<DefeatSfxCue>? SfxCues = null, IReadOnlyList<DefeatCgMotionLayerProfile>? CgMotionLayers = null, float RuntimeCgScaleAdapter = 1f, string? CgType2TexturePath = null, float CgType1ScaleAdapter = 1f, float CgType2ScaleAdapter = 1f, IReadOnlyList<DefeatFrameSequenceLayerProfile>? FrameSequenceLayers = null, float EffectReferenceHeight = 0f, IReadOnlyList<DefeatCgMotionLayerProfile>? CgType2MotionLayers = null)
{
	public IReadOnlyList<DefeatSfxCue> EffectiveSfxCues => SfxCues ?? new _003C_003Ez__ReadOnlySingleElementList<DefeatSfxCue>(new DefeatSfxCue("collapse", CollapseSfxPath, CollapseSfxDelaySeconds, CollapseSfxVolumeDb));

	public IReadOnlyList<DefeatCgMotionLayerProfile> EffectiveCgMotionLayers => CgMotionLayers ?? Array.Empty<DefeatCgMotionLayerProfile>();

	public IReadOnlyList<DefeatCgMotionLayerProfile> EffectiveCgType2MotionLayers => CgType2MotionLayers ?? EffectiveCgMotionLayers;

	public IReadOnlyList<DefeatFrameSequenceLayerProfile> EffectiveFrameSequenceLayers => FrameSequenceLayers ?? Array.Empty<DefeatFrameSequenceLayerProfile>();

	public float OverlayLifetimeSeconds
	{
		get
		{
			float num = 0f;
			foreach (DefeatEffectLayerProfile effectLayer in EffectLayers)
			{
				num = MathF.Max(num, effectLayer.VisibleSeconds);
			}
			float x = num + EffectIntroDelaySeconds;
			float x2 = 0f;
			foreach (DefeatFrameSequenceLayerProfile effectiveFrameSequenceLayer in EffectiveFrameSequenceLayers)
			{
				x2 = MathF.Max(x2, effectiveFrameSequenceLayer.StartDelaySeconds + effectiveFrameSequenceLayer.VisibleSeconds);
			}
			float x3 = CgIntroDelaySeconds + CgMotionSeconds;
			float dimFadeSeconds = DimFadeSeconds;
			return MathF.Max(0.25f, MathF.Max(x2, MathF.Max(x, MathF.Max(x3, dimFadeSeconds))));
		}
	}
}
