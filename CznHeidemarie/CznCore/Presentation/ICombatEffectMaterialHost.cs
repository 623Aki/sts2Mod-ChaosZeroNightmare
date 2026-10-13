namespace CznCore.Presentation;

internal interface ICombatEffectMaterialHost
{
	float GetEffectBrightness();

	float GetEffectOpacity()
	{
		return 1f;
	}
}
