using CznCore.Settings;

namespace CznHeidemarie.Integration;

/// <summary>
/// 角色设置存储。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.NineSettings</c>（17 行的小包装）：
/// 把配置文件路径与角色 id 换成本 mod 的值即可，其余由 CznCore 的设置系统接管。</para>
///
/// <para>配置落盘在 <c>user://CznModConfig/</c> 下，与参考 mod 共用同一套目录约定。</para>
/// </summary>
internal static class HeidemarieSettings
{
	/// <summary>角色 id，与 <see cref="CharacterSkinSettings"/>、设置面板的键一致。</summary>
	public const string CurrentCharacterId = "heidemarie";

	/// <summary>磁盘配置存储。</summary>
	internal static CharacterSkinSettingsStore Store { get; } = new CharacterSkinSettingsStore(
		CharacterSkinSettingsStore.BuildConfigPath("CznHeidemarie"),
		CurrentCharacterId,
		(string message, int _) => HeidemarieIntegration.Log.Warn(message));

	/// <summary>当前角色的设置视图（读写都走它）。</summary>
	public static SkinCharacterSettings CurrentCharacter { get; } = new SkinCharacterSettings(Store, CurrentCharacterId);
}
