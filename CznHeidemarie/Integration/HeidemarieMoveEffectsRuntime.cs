using System;
using System.Collections.Generic;
using System.Text.Json;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CznHeidemarie.Integration;

/// <summary>
/// 单体技能特效的接入层：把 <c>CznHeidemarie/animations/effects/</c> 里
/// 已经整合好的特效骨架正式接到卡牌演出上。
///
/// <para>对应参考 mod 的 <c>TipheraCombatEffects</c> / <c>NineBasicEffects</c>，
/// 但做法更轻：<b>不</b>走 <c>CharacterSkinProfile</c> + <c>CombatPresentationController</c>
/// 那条「一套框架驱动整套战斗演出」的大路（本工程从未接入它），
/// 而是复用与全屏演出同一套毫秒时间轴思路，见
/// <c>CznHeidemarie/effects/heidemarie_effect_stage.gd</c>。</para>
///
/// <para>触发点是 <see cref="CardPlayEnqueuingEvent"/>（游戏 <c>CardModel.EnqueueManualPlay</c>
/// 的 Prefix）—— 它同时给出卡牌与**目标**，正是官方为「出牌演出」预留的时机，
/// 早于 <c>OnPlay</c> 的数值结算。</para>
///
/// <para>结构：每张牌最多建两个舞台 —— 施法者一个、目标一个 ——
/// 各自挂到对应单位自己的骨架节点下，因此自动继承该单位的位置、朝向与缩放。
/// 舞台自己计时，播完发 <c>finished</c>，由本类释放。</para>
/// </summary>
internal static class HeidemarieMoveEffectsRuntime
{
	/// <summary>特效时间轴（卡牌 → 图层表）。</summary>
	private const string DataPath = "res://CznHeidemarie/effects/heidemarie_moves.json";

	/// <summary>特效舞台场景（Node2D + 合成器脚本）。</summary>
	private const string StageScenePath = "res://CznHeidemarie/effects/heidemarie_effect_stage.tscn";

	/// <summary>特效骨架 <c>_skel_data.tres</c> 目录，用于预加载。</summary>
	private const string SkelDataRoot = "res://CznHeidemarie/animations/effects/";

	/// <summary>预加载句柄键。</summary>
	private const string PreloadKey = "heidemarie_move_effects";

	/// <summary>施法者图层桶。</summary>
	private const string OwnerBucket = "owner";

	/// <summary>目标图层桶。</summary>
	private const string TargetBucket = "target";

	/// <summary>骨架查找与缓存。</summary>
	private static readonly CombatCreatureSpineResolver Spines = new();

	/// <summary>正在播放的舞台，用于换卡 / 离房时统一收尾。</summary>
	private static readonly List<Node> Active = new();

	/// <summary>事件订阅句柄。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>时间轴里登记过的卡牌类型名（延迟解析一次后缓存）。</summary>
	private static HashSet<string>? _knownKeys;

	/// <summary>
	/// 订阅出牌与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		// 出牌入队：拿得到卡牌与目标，是开始播放特效的正确时机
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效", (CardPlayEnqueuingEvent e) => TryPlay(e.Card, e.Target)));

		// 进战斗时线程预加载本卡组会用到的特效骨架
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效预加载", (CombatStartingEvent _) => Preload()));

		// 生命周期兜底
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效入房", (RoomEnteringEvent _) => Cleanup()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效阵亡", (PlayerDeathResolvedEvent e) =>
		{
			if (IsLocalHeidemarie(e.Player))
			{
				Cleanup();
			}
		}));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效胜利", (PlayerCombatVictoryEvent e) =>
		{
			if (IsLocalHeidemarie(e.Player))
			{
				Cleanup();
			}
		}));
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 触发
	// ──────────────────────────────────────────────────────────────────────────

	private static void TryPlay(CardModel? card, Creature? target)
	{
		try
		{
			if (card is null || !IsEnabled() || card is not HeidemarieCard)
			{
				return;
			}

			// 终结技由全屏演出整屏接管，这里不再叠单体特效
			if (card is HeidemarieLiberatedAurora or HeidemarieFinalEcho)
			{
				return;
			}

			Player? player = card.Owner;
			if (!IsLocalHeidemarie(player) || player!.Creature.IsDead)
			{
				return;
			}

			string key = card.GetType().Name;
			if (!HasEntry(key))
			{
				// 时间轴里没登记这张牌 —— 静默跳过，不刷日志
				return;
			}

			if (player.Creature.GetCreatureNode() is not { } ownerNode)
			{
				return;
			}

			Node2D? ownerAnchor = Spines.Find(ownerNode);
			if (ownerAnchor is not null)
			{
				SpawnStage(ownerAnchor, key, OwnerBucket);
			}

			if (target is not null && target.IsAlive && target.GetCreatureNode() is { } targetNode)
			{
				Node2D? targetAnchor = Spines.Find(targetNode);
				if (targetAnchor is not null)
				{
					SpawnStage(targetAnchor, key, TargetBucket);
				}
			}
		}
		catch (Exception ex)
		{
			Warn("技能特效播放失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 在 <paramref name="anchor"/>（某个战斗单位的骨架节点）下建一个特效舞台。
	/// 舞台自己解析时间轴；没有任何可用图层时返回 0 秒，此处直接把节点回收。
	/// </summary>
	private static void SpawnStage(Node2D anchor, string key, string bucket)
	{
		PackedScene? scene = ResourceLoader.Load<PackedScene>(StageScenePath, null, ResourceLoader.CacheMode.Reuse);
		if (scene?.Instantiate(PackedScene.GenEditState.Disabled) is not Node2D stage)
		{
			Warn("缺少技能特效舞台场景：" + StageScenePath);
			return;
		}

		anchor.AddChild(stage, forceReadableName: false, Node.InternalMode.Disabled);

		if (stage.Call("configure", DataPath, key, bucket).AsDouble() <= 0.0)
		{
			stage.QueueFree();
			return;
		}

		Active.Add(stage);
		stage.Connect("finished", Callable.From(() => Release(stage)));
		stage.Call("start");
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 生命周期
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>舞台播完，回收节点。</summary>
	private static void Release(Node stage)
	{
		Active.Remove(stage);
		if (GodotObject.IsInstanceValid(stage))
		{
			stage.QueueFree();
		}
	}

	/// <summary>取消并释放全部进行中的特效（换房 / 玩家阵亡 / 胜利）。</summary>
	private static void Cleanup()
	{
		foreach (Node stage in Active)
		{
			if (!GodotObject.IsInstanceValid(stage))
			{
				continue;
			}

			try
			{
				stage.Call("cancel");
			}
			catch
			{
				// 取消失败也要释放，不能因为一个节点卡住整批
			}

			stage.QueueFree();
		}

		Active.Clear();
		Spines.Clear();
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 预加载 / 数据查询 / 工具
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 进战斗时把时间轴引用到的特效骨架做线程预加载，避免第一次出牌才现加载。
	/// </summary>
	private static void Preload()
	{
		if (!IsEnabled())
		{
			CombatOverlayPreload.Release(PreloadKey);
			return;
		}

		try
		{
			List<string> paths = new() { StageScenePath };
			using JsonDocument document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(DataPath));
			if (document.RootElement.TryGetProperty("effects", out JsonElement effects))
			{
				foreach (JsonProperty effect in effects.EnumerateObject())
				{
					foreach (string bucket in new[] { OwnerBucket, TargetBucket })
					{
						if (!effect.Value.TryGetProperty(bucket, out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
						{
							continue;
						}

						foreach (JsonElement row in rows.EnumerateArray())
						{
							if (row.TryGetProperty("name", out JsonElement nameElement)
								&& nameElement.GetString() is { Length: > 0 } name)
							{
								string path = SkelDataRoot + name + "_skel_data.tres";
								if (!paths.Contains(path))
								{
									paths.Add(path);
								}
							}
						}
					}
				}
			}

			// 顺手把「哪些卡牌登记过」缓存下来；必须在 JsonDocument 释放前取走
			ResolveKeyCache(document);

			CombatOverlayPreload.HoldThreaded(
				PreloadKey,
				directoryRoots: null,
				explicitPaths: paths,
				onCompleted: () => Info($"技能特效预加载完成，共 {paths.Count} 项"));
		}
		catch (Exception ex)
		{
			CombatOverlayPreload.Release(PreloadKey);
			Warn("技能特效预加载跳过：" + ex.Message);
		}
	}

	/// <summary>这张牌在时间轴里有没有登记（登记过才值得建舞台）。</summary>
	private static bool HasEntry(string key)
	{
		HashSet<string>? keys = _knownKeys;
		if (keys is null)
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(DataPath));
				ResolveKeyCache(document);
				keys = _knownKeys;
			}
			catch (Exception ex)
			{
				Warn("技能特效时间轴读取失败：" + ex.Message);
				_knownKeys = new HashSet<string>(StringComparer.Ordinal);
				return false;
			}
		}

		return keys is not null && keys.Contains(key);
	}

	private static void ResolveKeyCache(JsonDocument document)
	{
		if (_knownKeys is not null)
		{
			return;
		}

		HashSet<string> keys = new(StringComparer.Ordinal);
		if (document.RootElement.TryGetProperty("effects", out JsonElement effects))
		{
			foreach (JsonProperty effect in effects.EnumerateObject())
			{
				keys.Add(effect.Name);
			}
		}

		_knownKeys = keys;
	}

	private static bool IsEnabled()
	{
		return HeidemarieSettings.CurrentCharacter.EffectiveEffectsEnabled;
	}

	private static bool IsLocalHeidemarie(Player? player)
	{
		if (player?.Character is not Heidemarie)
		{
			return false;
		}

		return LocalContext.IsMe(player);
	}

	private static void Info(string message)
	{
		HeidemarieIntegration.Log.Info("技能特效: " + message);
	}

	private static void Warn(string message)
	{
		HeidemarieIntegration.Log.Warn("技能特效: " + message);
	}
}
