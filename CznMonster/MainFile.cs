using System;
using System.Collections.Generic;
using System.Reflection;
using CznMonster.Content.Audio;
using CznMonster.Content.Monsters;
using CznMonster.Content.Net;
using CznMonster.Registration;
using CznMonster.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace CznMonster;

/// <summary>
/// mod 入口类。游戏加载本 Mod 时会调用 <see cref="Initialize"/> 完成初始化。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile
{
	/// <summary>
	/// Mod 的唯一标识，需与 CznMonster.json 中的 id 保持一致。
	/// </summary>
	public const string ModId = "CznMonster";

	internal static readonly Logger Log = new(ModId, LogType.Generic);

	/// <summary>
	/// Mod 初始化入口：注册共享设置面板扩展 + 应用 Harmony 补丁。
	/// </summary>
	/// <remarks>
	/// 自定义内容（怪物模型、遭遇模型、能力）不需要在这里手动注册：
	/// <c>ModelDb.Init()</c> 会扫描 mod 程序集中所有 <c>AbstractModel</c> 子类并实例化。
	/// </remarks>
	public static void Initialize()
	{
		// 共享设置面板扩展注册：设置界面打开时框架才读取，注册时机只需早于玩家打开设置
		SettingsExtensionRegistrar.Register();

		// 行动计数：订阅战斗生命周期事件（CombatManager 单例，此时已就绪）
		ActionCountPatches.Initialize();

		// 美梦偷牌：每场战斗开始时无条件复位持有区与「每回合偷几张」——
		// 不能只靠 Power 的 AfterCombatEnd（Boss 被击杀时 Power 会先被摘掉，那条钩子不执行）。
		SweetDreamStealRuntime.Initialize();

		// 联机：本局怪物参数以房主为准（CZN 怪物 / 首领位置 / 自定义血量 / 行动计数）。
		// 客户端通过大厅阶段下发的快照生成地图与战斗，避免与房主劈叉。
		CznMonsterConfigSyncRuntime.Initialize();

		// 音效：进本 mod 首领战前预热全部 CZN 采样（波勒米卡组 28 条 + Diallos 双怪组 36 条）
		CznMonsterSfx.Initialize();

		Harmony harmony = new(ModId);
		ApplyModPatches(harmony);

		Log.Info($"{ModId} initialized.");
	}

	/// <summary>
	/// 只自动挂载本 mod 自己命名空间里的补丁，并逐类隔离失败（铁律 6）。
	/// CznCore 副本不调用 PatchAll —— 本 mod 不注册 CznCore 自带的补丁组。
	/// </summary>
	private static void ApplyModPatches(Harmony harmony)
	{
		int patched = 0;
		List<string>? skipped = null;
		foreach (Type patchType in Assembly.GetExecutingAssembly().GetTypes())
		{
			string? typeNamespace = patchType.Namespace;
			if (typeNamespace is null
				|| !(typeNamespace == "CznMonster"
					|| typeNamespace.StartsWith("CznMonster.", StringComparison.Ordinal))
				|| patchType.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0)
			{
				continue;
			}

			try
			{
				harmony.CreateClassProcessor(patchType).Patch();
				patched++;
			}
			catch (Exception ex)
			{
				(skipped ??= new List<string>()).Add(patchType.Name);
				Log.Warn($"跳过补丁 {patchType.Name}：{ex.GetType().Name}: {ex.Message}");
			}
		}

		string summary = $"Harmony 补丁：成功 {patched} 个";
		if (skipped is not null)
		{
			summary += $"，跳过 {skipped.Count} 个 [{string.Join(", ", skipped)}]";
		}

		Log.Info(summary);
	}
}
