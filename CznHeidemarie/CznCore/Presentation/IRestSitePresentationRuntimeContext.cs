using System.Collections.Generic;
using Godot;

namespace CznCore.Presentation;

internal interface IRestSitePresentationRuntimeContext
{
	IReadOnlyList<Node> FindSpineSprites(Node root);

	void ApplyLayerMaterial(Node root);

	void ApplyLayerMaterialDeferred(Node root, int maxFrames);

	void StartLayerEffects(Node root, bool visible);

	void LogWarn(string message)
	{
		SkinLog.Warn(message);
	}
}
