using System;
using System.Collections.Generic;
using CznCore.Godot;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace CznHeidemarie.Integration;

/// <summary>
/// 战斗入场演出：进入战斗时让本体骨架播一次 <c>Entry</c> 动作链
/// （<c>enter_ready → enter_play → enter_end → idle</c>）。
///
/// <para>对应参考 mod 的 <c>NineBodyRuntime.PrepareEntryPatch</c> + <c>PlayEntry</c>。
/// <c>Entry</c> 是绘画写死的动画触发器名，需要 mod 自己触发 ——
/// 游戏只认识名字，不会主动调用它。两种做法：</para>
///
/// <list type="number">
///   <item>进战斗先把本体藏起来（<see cref="PrepareEntryPatch"/>），
///   免得它以默认姿势先露一面；</item>
///   <item>收到战斗开始事件时触发 <c>Entry</c> 并把本体重新显示（<see cref="PlayEntry"/>）。</item>
/// </list>
///
/// <para>隐藏带 2 秒超时兜底：即便战斗开始事件因故没到、或骨架尚未就绪，
/// 本体也会自动现身，不会把角色永久藏住。</para>
/// </summary>
internal static class HeidemarieCombatEntryRuntime
{
	/// <summary>标记本体已被「入场前隐藏」，避免重复隐藏与误显示。</summary>
	private const string EntryPreparedMeta = "czn_heidemarie_entry_prepared";

	/// <summary>隐藏后的兜底显示时长（秒），对应参考 mod 的 2 秒超时。</summary>
	private const float RevealFallbackSeconds = 2f;

	/// <summary>事件订阅句柄，保持引用以防被 GC 回收。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>
	/// 订阅战斗开始事件。由 <see cref="HeidemarieIntegration.Init"/> 在补丁应用后调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 战斗入场", (CombatStartingEvent e) =>
		{
			if (e.CombatState is null)
			{
				return;
			}

			foreach (Player player in e.CombatState.Players)
			{
				if (IsLocalHeidemarie(player))
				{
					PlayEntry(player);
				}
			}
		}));
	}

	/// <summary>
	/// 战斗房间就绪时，把本地 Heidemarie 的本体先隐藏起来，
	/// 等入场事件到来再显示（对应参考 mod 的 <c>NCombatRoom._Ready</c> 补丁）。
	/// </summary>
	[HarmonyPatch(typeof(NCombatRoom), "_Ready")]
	[HarmonyPriority(0)]
	private static class PrepareEntryPatch
	{
		private static void Postfix(NCombatRoom __instance)
		{
			// 只有真正的战斗房间才需要入场演出
			if (__instance.Mode != CombatRoomMode.ActiveCombat)
			{
				return;
			}

			foreach (NCreature node in __instance.CreatureNodes)
			{
				// 先取出实体再判空，避免对可能为 null 的 Entity 重复解引用
				Creature? entity = node.Entity;
				Player? player = entity?.Player;
				if (entity is null || player is null || !IsLocalHeidemarie(player) || entity.IsDead)
				{
					continue;
				}

				if (!GodotObject.IsInstanceValid(node))
				{
					continue;
				}

				node.Visuals.Hide();
				node.SetMeta(EntryPreparedMeta, Variant.From(true));

				// 兜底：2 秒内没人来 Reveal 就自己现身，避免角色一直不可见
				GodotScheduler.Delay(RevealFallbackSeconds, () => Reveal(node));
			}
		}
	}

	/// <summary>
	/// 触发入场动作并把本体重新显示出来。
	/// </summary>
	/// <param name="player">本地 Heidemarie 玩家。</param>
	private static void PlayEntry(Player player)
	{
		if (player.Creature.GetCreatureNode() is not { } node)
		{
			return;
		}

		// 触发器的下落链在 GenerateAnimator 里注册为 Entry → enter_ready → … → idle
		node.SetAnimationTrigger("Entry");
		Reveal(node);
	}

	/// <summary>
	/// 若本体正处于「入场前隐藏」状态，则恢复显示并清掉标记。
	/// </summary>
	private static void Reveal(NCreature node)
	{
		if (GodotObject.IsInstanceValid(node) && node.IsInsideTree() && node.HasMeta(EntryPreparedMeta))
		{
			node.Visuals.Show();
			node.RemoveMeta(EntryPreparedMeta);
		}
	}

	/// <summary>是不是本地玩家操作的 Heidemarie。</summary>
	private static bool IsLocalHeidemarie(Player? player)
	{
		if (player?.Character is not Heidemarie)
		{
			return false;
		}

		return LocalContext.IsMe(player);
	}
}
