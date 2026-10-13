using MegaCrit.Sts2.Core.Entities.Players;

namespace CznCore.Presentation;

internal interface ICharacterContextCutInHost
{
	bool IsEnabled();

	bool IsPlayerCurrent(Player? player);

	string RuntimeNodeName(string suffix);

	void Warn(string message, int priority);

	void Info(string message, int priority)
	{
	}
}
