using System.Collections.Generic;

namespace CznCore.Presentation;

internal interface ICombatBodyBindingAnimationSource
{
	IEnumerable<string> EnumerateRequiredBodyBindingAnimations();
}
