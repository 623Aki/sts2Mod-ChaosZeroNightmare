using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Integration;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace CznHeidemarie;

/// <summary>
/// mod 入口类。游戏加载本 Mod 时会调用 <see cref="Initialize"/> 完成初始化。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile
{
	/// <summary>
	/// Mod 的唯一标识，需与 CznHeidemarie.json 中的 id 保持一致。
	/// </summary>
	public const string ModId = "CznHeidemarie";

	private static readonly Logger Log = new(ModId, LogType.Generic);

	/// <summary>
	/// Mod 初始化入口。在此应用 Harmony 补丁。
	/// </summary>
	/// <remarks>
	/// 自定义内容（卡牌、遗物、药水）不需要在这里手动注册：
	/// <list type="bullet">
	///   <item><c>ModelDb.Init()</c> 会扫描 mod 程序集中所有 <c>AbstractModel</c> 子类并实例化；</item>
	///   <item>各池子在 <c>GenerateAllCards/GenerateAllRelics/GenerateAllPotions</c> 里
	/// 用 <c>ModelDb.Card&lt;T&gt;()</c> 等形式引用这些模型，即可进入对应池子。</item>
	/// </list>
	/// <see cref="ModHelper.AddModelToPool{TPoolType,TModelType}"/> 只用于把模型追加进
	/// 「共享池」这类自己无法列举内容的池子；传入抽象基类（如 <c>CardModel</c>）会在
	/// 池子被读取时抛出 <c>ModelNotFoundException</c>，因此切勿这样使用。
	/// </remarks>
	public static void Initialize()
	{
		// 应用本程序集内的所有 Harmony 补丁
		// （见 Registration/HeidemarieRegistrationPatch.cs：把角色追加进 ModelDb.AllCharacters）
		Harmony harmony = new(ModId);
		try
		{
			harmony.PatchAll();
		}
		catch (System.Exception ex)
		{
			// 单个补丁挂载失败不应导致整个 mod 加载失败，至少要能进游戏看到日志
			Log.Error($"Harmony PatchAll 失败（部分补丁可能未生效）：{ex}");
		}

		// 「唯一」关键字的加牌去重补丁单独挂载：它需要按参数类型精确指定重载并改写异步返回值，
		// 是本组里最容易挂载失败的一个，因此隔离出来（失败只告警，不牵连其它补丁）。
		Registration.HeidemarieUniqueDeckGuard.Install(harmony, Log);

		// 接入 CznCore 框架：注册事件转发补丁组、启动出牌特写立绘、
		// 把本角色的设置分区挂进游戏设置界面（见 Integration/）
		HeidemarieIntegration.Init(harmony);

		Log.Info($"{ModId} initialized.");
	}
}
