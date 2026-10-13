namespace CznCore.Presentation;

internal interface ICombatVideoCutInOverlayHost<TActor> where TActor : class
{
	bool IsEnabled();

	bool IsEnabledFor(TActor? actor)
	{
		return true;
	}

	bool IsTokenCurrent(TActor? actor, int token);

	string RuntimeNodeName(string suffix);

	void Warn(string message, int priority);

	void Info(string message, int priority)
	{
	}
}
