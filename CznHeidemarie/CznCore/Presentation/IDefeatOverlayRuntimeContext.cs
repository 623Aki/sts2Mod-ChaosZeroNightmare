using Godot;

namespace CznCore.Presentation;

internal interface IDefeatOverlayRuntimeContext
{
	bool IsEnabled { get; }

	DefeatCgOverlayMode OverlayMode { get; }

	CharacterSkinSfxPlayerRuntime SfxPlayer { get; }

	void ApplyEffectMaterial(Node root);

	void ApplyEffectMaterialDeferred(Node root, int frames);

	void ApplyNodeMaterial(Node node);

	void LogInfo(string message)
	{
		SkinLog.Debug(message);
	}

	void LogWarn(string message)
	{
		SkinLog.Warn(message);
	}
}
