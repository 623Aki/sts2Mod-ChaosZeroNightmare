using System;
using System.Collections.Generic;
using System.Text.Json;
using CznCore.Godot;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using CznHeidemarie.Characters.Heidemarie.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Integration;

/// <summary>
/// 单体技能特效的接入层：把 <c>CznHeidemarie/animations/effects/</c> 里
/// 已经整合好的特效骨架接到卡牌演出上。
///
/// <para><b>挂载规则（与官方 <c>VfxCmd</c> 一致）：</b>
/// 所有舞台都挂 <c>creature.GetVfxContainer()</c>（战斗中是 <c>CombatVfxContainer</c>，
/// 图鉴里是图鉴容器），用世界坐标定位；不再挂到敌人 / 施法者的骨架节点下。</para>
///
/// <para><b>为什么首帧仍能与旧版对齐：</b>舞台根节点的全局基从目标单位的
/// <c>NCreature.Body</c> 复刻（含 <c>Visuals.Scale</c>、镜像、相机缩放），
/// 原点用官方锚点（默认 <c>GetBottomOfHitbox() + (0,-40)</c>，即旧的 healthbar 锚点），
/// z 用 <c>Body</c> 的有效深度。于是每个图层的世界位置仍等于
/// <c>anchor + BodyBasis * layerOffset</c>，与挂在骨架下时完全一致。</para>
///
/// <para><b>生命周期：</b>舞台自己的 <c>finished</c> 信号 → <see cref="HeidemarieVfx.Free"/>
/// （内部走 <c>QueueFreeSafely()</c>）；换房 / 胜利 / 玩家死亡 / 关闭特效设置时统一
/// <see cref="CancelAll"/>。不做节点池。</para>
///
/// <para>触发点是 <see cref="CardPlayEnqueuingEvent"/>（游戏 <c>CardModel.EnqueueManualPlay</c>
/// 的 Prefix）—— 它同时给出卡牌与目标，且早于 <c>OnPlay</c> 的伤害结算，
/// 正好保住重击 / 顶击档的前摇。命中帧再按 <see cref="DamageReceivedEvent"/> 重设一次原点，
/// 用来跟住敌人的抖动 / 位移。</para>
/// </summary>
internal static class HeidemarieMoveEffectsRuntime
{
	/// <summary>特效时间轴（档位 → 图层表）。</summary>
	private const string DataPath = "res://CznHeidemarie/effects/heidemarie_moves.json";

	/// <summary>特效舞台场景（Node2D + 合成器脚本）。</summary>
	private const string StageScenePath = "res://CznHeidemarie/effects/heidemarie_effect_stage.tscn";

	/// <summary>特效骨架 <c>_skel_data.tres</c> 目录，用于预加载。</summary>
	private const string SkelDataRoot = "res://CznHeidemarie/animations/effects/";

	/// <summary>特效组库：组键 → 组的图层构成 + 挂载参数。</summary>
	private const string GroupLibraryPath = "res://CznHeidemarie/effects/heidemarie_effect_groups.json";

	/// <summary>自组装特效组库：组名 → owner / target 两侧的图层构成。</summary>
	private const string ComposeLibraryPath = "res://CznHeidemarie/effects/heidemarie_effect_compose.json";

	/// <summary>预加载句柄键。</summary>
	private const string PreloadKey = "heidemarie_move_effects";

	/// <summary>施法者图层桶。</summary>
	private const string OwnerBucket = "owner";

	/// <summary>目标图层桶。</summary>
	private const string TargetBucket = "target";

	/// <summary>
	/// 目标特效默认锚点的屏幕像素偏移（正 = 更低，负 = 更高）。
	/// 旧策略表里的 <c>healthbar</c> 锚点就是 <c>GetBottomOfHitbox() + (0,-40)</c>；
	/// 迁移期保持这个数值，避免「重构 + 重新标定」两件事混在一起。
	/// </summary>
	private const float TargetAnchorOffsetY = -40f;

	/// <summary>
	/// 目标侧图层整体抬升量（旧策略表 <c>target_z_lift=4</c>）。
	/// 现在 z 已按「层数」压过（min_z ≥ 0），gd 侧的 <c>delta</c> 自然退化成 0，
	/// 保留它是为了不改动这条历史约定。
	/// </summary>
	private const int TargetZLift = 4;

	/// <summary>前台分层：z ≥ 0 的层，落 <c>CombatVfxContainer</c>，画在角色之上。</summary>
	private const string FrontPart = "front";

	/// <summary>后台分层：z &lt; 0 的层，落 <c>BackCombatVfxContainer</c>，画在背景之上、角色之下。</summary>
	private const string BackPart = "back";



	/// <summary>一个在播舞台的登记项（一个 bucket 会拆成前台 / 后台两个根节点）。</summary>
	private sealed class StageEntry
	{
		/// <summary>本登记项持有的舞台根节点：前台（角色之前）与后台（角色之后）各最多一个。</summary>
		internal readonly List<Node2D> Roots = new();

		internal Creature Creature = null!;

		internal string Key = string.Empty;
	}

	/// <summary>目标侧舞台的额外信息：命中帧要按 Creature 反查并重设锚点。</summary>
	private sealed class TargetStage
	{
		internal StageEntry Entry = null!;

		internal HeidemarieVfxAnchor Anchor;

		internal Vector2 Offset;
	}

	/// <summary>正在播放的舞台，用于换卡 / 离房时统一收尾。</summary>
	private static readonly List<StageEntry> Active = new();

	/// <summary>只收目标侧舞台：只有目标侧需要在命中帧重设锚点。</summary>
	private static readonly List<TargetStage> TargetStages = new();

	/// <summary>
	/// 本场战斗已经接管过的卡牌实例。
	/// <c>CardPlayEnqueuingEvent</c> 可能在同一张牌的多个 transport 阶段到达；
	/// 按实例去重，避免 owner/target 舞台重复组装。
	/// </summary>
	private static readonly HashSet<CardModel> PlayedCards = new();

	/// <summary>事件订阅句柄。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>时间轴里登记过的档位键（延迟解析一次后缓存）。</summary>
	private static HashSet<string>? _knownKeys;

	/// <summary>舞台创建失败只提示一次，避免每张牌刷屏。</summary>
	private static bool _spawnFailureWarned;

	/// <summary>
	/// 订阅出牌与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效", (CardPlayEnqueuingEvent e) => TryPlay(e.Card, e.Target)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效预加载", (CombatStartingEvent _) => Preload()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效命中", (DamageReceivedEvent e) => OnDamageReceived(e)));
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

			// 终结技：全屏演出会整屏接管时让位；关掉全屏演出时由 PlayFallback 补播。
			if (HeidemarieCinematicRuntime.WillTakeOver(card))
			{
				return;
			}

			SpawnFor(card, target);
		}
		catch (Exception ex)
		{
			Warn("技能特效播放失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 全屏演出「判定不通过」时的补播入口，由 <see cref="HeidemarieCinematicRuntime"/> 调用。
	/// </summary>
	internal static void PlayFallback(CardModel card, Creature? target)
	{
		try
		{
			if (!IsEnabled() || card is not HeidemarieCard)
			{
				return;
			}

			SpawnFor(card, target);
		}
		catch (Exception ex)
		{
			Warn("技能特效补播失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 给一张牌建舞台：施法者一个（骨架原点），目标侧按 <see cref="ResolveTargets"/> 展开。
	/// 两个 bucket 都挂 <c>GetVfxContainer()</c>；目标侧额外登记到 <see cref="TargetStages"/>。
	/// </summary>
	private static void SpawnFor(CardModel card, Creature? target)
	{
		string key = HeidemariePresentationCatalog.ResolveKey((HeidemarieCard)card);
		if (!HasEntry(key) || PlayedCards.Contains(card))
		{
			return;
		}

		Player? player = card.Owner;
		if (!IsLocalHeidemarie(player) || player!.Creature.IsDead)
		{
			return;
		}

		if (player.Creature.GetCreatureNode() is null)
		{
			return;
		}

		bool spawned = false;

		// 施法者侧：旧实现挂在骨架原点；现在同样走容器，锚点用 BodyOrigin。
		if (SpawnStage(
				player.Creature,
				key,
				OwnerBucket,
				HeidemarieVfxAnchor.BodyOrigin,
				Vector2.Zero,
				0) is not null)
		{
			spawned = true;
		}

		Vector2 targetOffset = new(0f, TargetAnchorOffsetY);
		foreach (Creature victim in ResolveTargets(card, target))
		{
			if (victim.GetCreatureNode() is null)
			{
				continue;
			}

			StageEntry? entry = SpawnStage(
				victim,
				key,
				TargetBucket,
				HeidemarieVfxAnchor.FloorScreenOffset,
				targetOffset,
				TargetZLift);
			if (entry is null)
			{
				continue;
			}

			TargetStages.Add(new TargetStage
			{
				Entry = entry,
				Anchor = HeidemarieVfxAnchor.FloorScreenOffset,
				Offset = targetOffset,
			});
			spawned = true;
		}

		if (spawned)
		{
			PlayedCards.Add(card);
		}
	}

	/// <summary>
	/// 给一个 bucket 起舞台：按 z 正负拆成前台 / 后台两个根节点，各自配置、各自计时。
	/// </summary>
	private static StageEntry? SpawnStage(
		Creature creature,
		string key,
		string bucket,
		HeidemarieVfxAnchor anchor,
		Vector2 screenOffset,
		int zLift)
	{
		int baseZ = ResolveBodyZ(creature);

		StageEntry? entry = null;
		foreach (bool behind in new[] { false, true })
		{
			Node2D? root = SpawnPart(creature, key, bucket, anchor, screenOffset, baseZ, zLift, behind);
			if (root is null)
			{
				continue;
			}

			entry ??= new StageEntry
			{
				Creature = creature,
				Key = key,
			};
			entry.Roots.Add(root);
		}

		if (entry is null)
		{
			return null;
		}

		Active.Add(entry);

		foreach (Node2D root in entry.Roots.ToArray())
		{
			StageEntry owner = entry;
			Node2D node = root;
			node.Connect(
				"finished",
				Callable.From(() => OnRootFinished(owner, node)),
				(uint)GodotObject.ConnectFlags.OneShot);
		}

		foreach (Node2D root in entry.Roots.ToArray())
		{
			try
			{
				root.Call("start");
			}
			catch (Exception ex)
			{
				Warn("技能特效舞台启动失败：" + ex.Message);
				Cancel(entry);
				return null;
			}
		}

		return entry;
	}

	/// <summary>建单个分层的舞台（behind = 后台容器，只收 z &lt; 0 的层）。</summary>
	private static Node2D? SpawnPart(
		Creature creature,
		string key,
		string bucket,
		HeidemarieVfxAnchor anchor,
		Vector2 screenOffset,
		int baseZ,
		int zLift,
		bool behind)
	{
		string part = behind ? BackPart : FrontPart;
		Node2D? stage = HeidemarieVfx.SpawnOnCreature(creature, StageScenePath, anchor, screenOffset, behind);
		if (stage is null)
		{
			if (!_spawnFailureWarned)
			{
				_spawnFailureWarned = true;
				Warn("技能特效舞台创建失败（场景缺失 / 容器不可用 / 测试模式），后续同类失败不再提示");
			}

			return null;
		}

		if (!stage.HasSignal("finished"))
		{
			Warn("技能特效舞台缺少 finished 信号：" + StageScenePath);
			HeidemarieVfx.Free(stage);
			return null;
		}

		double duration;
		try
		{
			duration = stage.Call("configure", DataPath, key, bucket, baseZ, zLift, part).AsDouble();
		}
		catch (Exception)
		{
			// 兼容旧 pck（脚本还是 5 参数）：只建前台一路，避免重复画同一批层。
			if (behind)
			{
				HeidemarieVfx.Free(stage);
				return null;
			}

			try
			{
				duration = stage.Call("configure", DataPath, key, bucket, baseZ, zLift).AsDouble();
			}
			catch (Exception ex)
			{
				Warn("技能特效舞台配置失败：" + ex.Message);
				HeidemarieVfx.Free(stage);
				return null;
			}
		}

		if (duration <= 0.0)
		{
			HeidemarieVfx.Free(stage);
			return null;
		}

		try
		{
			HeidemarieEffectMaterials.Apply(stage);
		}
		catch (Exception ex)
		{
			Warn("技能特效材质应用失败：" + ex.Message);
			HeidemarieVfx.Free(stage);
			return null;
		}

		return stage;
	}

	/// <summary>取单位 Body 的有效 z（舞台根节点的 z 基准）；取不到时返回 0。</summary>
	private static int ResolveBodyZ(Creature creature)
	{
		if (creature.GetCreatureNode() is not { } stageOwner)
		{
			return 0;
		}

		Node2D? body = stageOwner.Body;
		if (!GodotNodeUtil.IsAlive(body))
		{
			body = stageOwner.Visuals;
		}

		return GodotNodeUtil.IsAlive(body) ? HeidemarieVfx.EffectiveZ(body) : 0;
	}

	/// <summary>
	/// 展开「目标侧」的落点：单体牌直接用传入的目标；
	/// 全体敌人牌的目标为 <c>null</c>，这里展开成每一个存活敌人各建一个舞台。
	/// </summary>
	private static IEnumerable<Creature> ResolveTargets(CardModel card, Creature? target)
	{
		if (target is not null)
		{
			if (target.IsAlive)
			{
				yield return target;
			}

			yield break;
		}

		if (card.TargetType != TargetType.AllEnemies)
		{
			yield break;
		}

		IEnumerable<Creature>? enemies = card.Owner?.Creature.CombatState?.Enemies;
		if (enemies is null)
		{
			yield break;
		}

		foreach (Creature enemy in enemies)
		{
			if (enemy.IsAlive)
			{
				yield return enemy;
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 命中帧：重设原点
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 伤害结算时刻：把挨了这一次伤害的敌人身上挂着的目标舞台再对一次锚点。
	/// 舞台是在出牌前挂上的，到真正命中之间敌人可能已经被抖过 / 位移过；
	/// 这里只重设根节点变换，图层 offset 不动。目标已死亡时跳过重设，
	/// 让最后一帧照常播完，由舞台自己的 finished 释放。
	/// </summary>
	private static void OnDamageReceived(DamageReceivedEvent e)
	{
		if (e.Target is null || TargetStages.Count == 0)
		{
			return;
		}

		List<TargetStage> snapshot = new(TargetStages);
		foreach (TargetStage tracked in snapshot)
		{
			StageEntry entry = tracked.Entry;
			if (entry.Roots.Count == 0)
			{
				TargetStages.Remove(tracked);
				continue;
			}

			if (!ReferenceEquals(entry.Creature, e.Target))
			{
				continue;
			}

			// 目标已死亡时不再重设锚点，但也不立刻取消舞台：
			// 命中/击杀的最后一帧仍要播完，由舞台自己的 finished 释放。
			if (e.Target.IsDead)
			{
				continue;
			}

			if (entry.Creature.GetCreatureNode() is not { } creatureNode)
			{
				continue;
			}

			foreach (Node2D root in entry.Roots.ToArray())
			{
				if (!GodotNodeUtil.IsAlive(root))
				{
					continue;
				}

				try
				{
					HeidemarieVfx.ApplyCreatureAnchor(root, creatureNode, tracked.Anchor, tracked.Offset);
				}
				catch (Exception ex)
				{
					Warn("目标特效命中重定位失败：" + ex.Message);
				}
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 生命周期
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>某一路舞台播完：释放该根节点；两路都放完才移出活动表。</summary>
	private static void OnRootFinished(StageEntry entry, Node2D root)
	{
		entry.Roots.Remove(root);
		HeidemarieVfx.Free(root);
		if (entry.Roots.Count == 0)
		{
			Active.Remove(entry);
			TargetStages.RemoveAll(tracked => ReferenceEquals(tracked.Entry, entry));
		}
	}

	/// <summary>释放登记项持有的全部根节点并移出活动表。</summary>
	private static void Release(StageEntry entry)
	{
		Active.Remove(entry);
		TargetStages.RemoveAll(tracked => ReferenceEquals(tracked.Entry, entry) || tracked.Entry.Roots.Count == 0);
		foreach (Node2D root in entry.Roots)
		{
			HeidemarieVfx.Free(root);
		}

		entry.Roots.Clear();
	}

	/// <summary>取消一个舞台：先停时钟/隐藏图层，再走统一释放。</summary>
	private static void Cancel(StageEntry entry)
	{
		foreach (Node2D root in entry.Roots.ToArray())
		{
			if (!GodotNodeUtil.IsAlive(root))
			{
				continue;
			}

			try
			{
				root.Call("cancel");
			}
			catch
			{
				// 取消失败也要释放，不能因为一个节点卡住整批
			}
		}

		Release(entry);
	}

	/// <summary>取消并释放全部进行中的特效（换房 / 玩家阵亡 / 胜利 / 设置关闭）。</summary>
	internal static void CancelAll() => Cleanup();

	private static void Cleanup()
	{
		List<StageEntry> snapshot = new(Active);
		foreach (StageEntry entry in snapshot)
		{
			Cancel(entry);
		}

		Active.Clear();
		TargetStages.Clear();
		PlayedCards.Clear();
		_spawnFailureWarned = false;
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
			// 舞台场景走 mod 自己的同步缓存（不依赖 PreloadManager）。
			HeidemarieVfx.PreloadScene(StageScenePath);

			List<string> paths = new();
			Dictionary<string, List<string>> groupSkeletons = LoadGroupSkeletons();
			Dictionary<string, List<string>> composeSkeletons = LoadComposeSkeletons();
			using JsonDocument document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(DataPath));
			if (document.RootElement.TryGetProperty("effects", out JsonElement effects))
			{
				foreach (JsonProperty effect in effects.EnumerateObject())
				{
					if (effect.Value.TryGetProperty("compose", out JsonElement composeRows)
						&& composeRows.ValueKind == JsonValueKind.Array)
					{
						foreach (JsonElement row in composeRows.EnumerateArray())
						{
							if (!row.TryGetProperty("group", out JsonElement composeGroupElement)
								|| composeGroupElement.GetString() is not { Length: > 0 } composeGroup
								|| !composeSkeletons.TryGetValue(composeGroup, out List<string>? composeFound))
							{
								continue;
							}

							foreach (string skeleton in composeFound)
							{
								AddPreloadPath(paths, skeleton);
							}
						}
					}

					foreach (string bucket in new[] { OwnerBucket, TargetBucket })
					{
						if (!effect.Value.TryGetProperty(bucket, out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
						{
							continue;
						}

						foreach (JsonElement row in rows.EnumerateArray())
						{
							// v4 只写组键（骨架名在组库里），v3 直接写骨架名，两代都收。
							IEnumerable<string> names;
							if (row.TryGetProperty("group", out JsonElement groupElement)
								&& groupElement.GetString() is { Length: > 0 } group)
							{
								names = groupSkeletons.TryGetValue(group, out List<string>? found)
									? found
									: Array.Empty<string>();
							}
							else if (row.TryGetProperty("name", out JsonElement nameElement)
								&& nameElement.GetString() is { Length: > 0 } name)
							{
								names = new[] { name };
							}
							else
							{
								continue;
							}

							foreach (string skeleton in names)
							{
								AddPreloadPath(paths, skeleton);
							}
						}
					}
				}
			}

			// 顺手把「哪些档位登记过」缓存下来；必须在 JsonDocument 释放前取走。
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

	/// <summary>
	/// 读特效组库，建「组键 → 该组用到的骨架名」索引。
	/// 组库缺失时返回空表 —— 预加载会退化成「只加载舞台场景」，特效仍能运行。
	/// </summary>
	private static Dictionary<string, List<string>> LoadGroupSkeletons()
	{
		Dictionary<string, List<string>> result = new(StringComparer.Ordinal);
		try
		{
			if (!global::Godot.FileAccess.FileExists(GroupLibraryPath))
			{
				Warn("缺少特效组库，骨架预加载已跳过：" + GroupLibraryPath);
				return result;
			}

			using JsonDocument library = JsonDocument.Parse(
				global::Godot.FileAccess.GetFileAsString(GroupLibraryPath));
			if (!library.RootElement.TryGetProperty("classes", out JsonElement classes))
			{
				return result;
			}

			foreach (JsonProperty bucket in classes.EnumerateObject())
			{
				foreach (JsonProperty group in bucket.Value.EnumerateObject())
				{
					List<string> skeletons = new();
					if (group.Value.TryGetProperty("spine_layers", out JsonElement layers)
						&& layers.ValueKind == JsonValueKind.Array)
					{
						foreach (JsonElement layer in layers.EnumerateArray())
						{
							if (layer.TryGetProperty("source", out JsonElement sourceElement)
								&& sourceElement.GetString() is { Length: > 0 } source
								&& !skeletons.Contains(source))
							{
								skeletons.Add(source);
							}
						}
					}

					result[group.Name] = skeletons;
				}
			}
		}
		catch (Exception ex)
		{
			Warn("特效组库读取失败（骨架预加载会不完整）：" + ex.Message);
		}

		return result;
	}

	/// <summary>
	/// 读自组装组库，建「组名 → 该组用到的骨架名」索引。
	/// 组库缺失时返回空表 —— 预加载退化成只收 v4 组键与 v3 行的骨架，特效仍能运行。
	/// </summary>
	private static Dictionary<string, List<string>> LoadComposeSkeletons()
	{
		Dictionary<string, List<string>> result = new(StringComparer.Ordinal);
		try
		{
			if (!global::Godot.FileAccess.FileExists(ComposeLibraryPath))
			{
				return result;
			}

			using JsonDocument library = JsonDocument.Parse(
				global::Godot.FileAccess.GetFileAsString(ComposeLibraryPath));
			if (!library.RootElement.TryGetProperty("groups", out JsonElement groups))
			{
				return result;
			}

			foreach (JsonProperty group in groups.EnumerateObject())
			{
				List<string> skeletons = new();
				foreach (string bucket in new[] { OwnerBucket, TargetBucket })
				{
					if (!group.Value.TryGetProperty(bucket, out JsonElement layers)
						|| layers.ValueKind != JsonValueKind.Array)
					{
						continue;
					}

					foreach (JsonElement layer in layers.EnumerateArray())
					{
						// compose 的层字段叫 name（原子组库叫 source）
						if (layer.TryGetProperty("name", out JsonElement nameElement)
							&& nameElement.GetString() is { Length: > 0 } name
							&& !skeletons.Contains(name))
						{
							skeletons.Add(name);
						}
					}
				}

				result[group.Name] = skeletons;
			}
		}
		catch (Exception ex)
		{
			Warn("自组装组库读取失败（骨架预加载会不完整）：" + ex.Message);
		}

		return result;
	}

	/// <summary>把一个骨架的 <c>_skel_data.tres</c> 加进预加载列表（去重）。</summary>
	private static void AddPreloadPath(List<string> paths, string skeleton)
	{
		string path = SkelDataRoot + skeleton + "_skel_data.tres";
		if (!paths.Contains(path))
		{
			paths.Add(path);
		}
	}

	/// <summary>这张牌登记的档位在时间轴里有没有特效（登记过才值得建舞台）。</summary>
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
