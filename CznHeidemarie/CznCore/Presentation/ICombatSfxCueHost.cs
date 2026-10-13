namespace CznCore.Presentation;

internal interface ICombatSfxCueHost<TActor> where TActor : class
{
	bool IsEnabled();

	bool IsEnabledFor(TActor? actor)
	{
		return true;
	}

	bool IsTokenCurrent(TActor? actor, int token);

	bool IsActorValid(TActor? actor)
	{
		return true;
	}

	void PlaySfx(string path, float volume);

	void Warn(string message, int priority);
}
