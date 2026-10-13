using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal static class CombatOfficialCameraShakeProfiles
{
	private readonly record struct Sample(float TimeSeconds, float Offset);

	private static readonly Sample[] VibrationSamples = new Sample[7]
	{
		new Sample(0f, 0f),
		new Sample(0.0333f, 15f),
		new Sample(0.1f, -13f),
		new Sample(0.1667f, 7f),
		new Sample(0.2333f, -4f),
		new Sample(0.3f, 2f),
		new Sample(0.3667f, 0f)
	};

	private static readonly Sample[] WideKnockSamples = new Sample[5]
	{
		new Sample(0f, 0f),
		new Sample(0.0333f, 50f),
		new Sample(0.1f, -80f),
		new Sample(0.2f, 15f),
		new Sample(0.5f, 0f)
	};

	public static readonly IReadOnlyList<CombatCameraShakeKey> VibrationUp = BuildDirectional(VibrationSamples, Vector2.Up);

	public static readonly IReadOnlyList<CombatCameraShakeKey> VibrationDown = BuildDirectional(VibrationSamples, Vector2.Down);

	public static readonly IReadOnlyList<CombatCameraShakeKey> VibrationLeft = BuildDirectional(VibrationSamples, Vector2.Right);

	public static readonly IReadOnlyList<CombatCameraShakeKey> VibrationRight = BuildDirectional(VibrationSamples, Vector2.Left);

	public static readonly IReadOnlyList<CombatCameraShakeKey> WideKnockUp = BuildDirectional(WideKnockSamples, Vector2.Up);

	public static readonly IReadOnlyList<CombatCameraShakeKey> WideKnockDown = BuildDirectional(WideKnockSamples, Vector2.Down);

	public static readonly IReadOnlyList<CombatCameraShakeKey> WideKnockLeft = BuildDirectional(WideKnockSamples, Vector2.Right);

	public static readonly IReadOnlyList<CombatCameraShakeKey> WideKnockRight = BuildDirectional(WideKnockSamples, Vector2.Left);

	private static IReadOnlyList<CombatCameraShakeKey> BuildDirectional(IReadOnlyList<Sample> samples, Vector2 direction)
	{
		CombatCameraShakeKey[] array = new CombatCameraShakeKey[samples.Count];
		for (int i = 0; i < samples.Count; i++)
		{
			Sample sample = samples[i];
			array[i] = new CombatCameraShakeKey(sample.TimeSeconds, direction * sample.Offset);
		}
		return array;
	}
}
