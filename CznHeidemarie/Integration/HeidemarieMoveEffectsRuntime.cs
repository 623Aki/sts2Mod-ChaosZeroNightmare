using System;
using System.Collections.Generic;
using System.Text.Json;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// <para>结构：每张牌建一个施法者舞台，目标侧按目标数各建一个 ——
/// 全部挂到对应单位自己的骨架节点下，因此自动继承该单位的位置、朝向与缩放。
/// 舞台自己计时，播完发 <c>finished</c>，由本类释放。</para>
///
/// <para>与全屏演出的分工：两张终结技（「解放极光」/「绝响」）在时间轴里
/// 同样登记了常规特效，但只在全屏演出<b>不接管</b>时才播 —— 演出开着时由
/// <see cref="HeidemarieCinematicRuntime.WillTakeOver"/> 让位，演出开着却判定不通过时
/// 由 <see cref="PlayFallback"/> 补播。于是「关掉全屏演出」不再等于「大招没有特效」。</para>
/// </summary>
internal static class HeidemarieMoveEffectsRuntime
{
	/// <summary>特效时间轴（卡牌 → 图层表）。</summary>
	private const string DataPath = "res://CznHeidemarie/effects/heidemarie_moves.json";

	/// <summary>特效舞台场景（Node2D + 合成器脚本）。</summary>
	private const string StageScenePath = "res://CznHeidemarie/effects/heidemarie_effect_stage.tscn";

	/// <summary>特效骨架 <c>_skel_data.tres</c> 目录，用于预加载。</summary>
	private const string SkelDataRoot = "res://CznHeidemarie/animations/effects/";

	/// <summary>
	/// 特效组库：组键 → 组的图层构成 + 挂载参数。
	/// 由 <c>tools/list_effect_groups.py</c> 从 CZN 源数据生成。
	/// 卡牌映射只写组键，骨架名从这里取。
	/// </summary>
	private const string GroupLibraryPath = "res://CznHeidemarie/effects/heidemarie_effect_groups.json";

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

	/// <summary>
	/// 本场战斗已经接管过的卡牌实例。
	/// CardPlayEnqueuingEvent 可能在同一张牌的多个 transport 阶段到达；
	/// 参考 mod 的 presentation token 只允许一份动作序列，本集合保证不会
	/// 因重复事件把 owner/target 舞台再组装一遍。按实例而非 CardModel.Id 去重，
	/// 避免同 ID 的不同牌实例互相吞掉演出。
	/// </summary>
	private static readonly HashSet<CardModel> PlayedCards = new();

	/// <summary>
	/// 一个挂在敌人身上的「目标侧」舞台，记着它与哪个敌人配对 ——
	/// 命中帧要按敌人反查它，才知道该校正哪一个舞台。
	/// </summary>
	private sealed class TargetStage
	{
		/// <summary>挂在敌人骨架下的特效舞台。</summary>
		internal Node2D Stage = null!;

		/// <summary>舞台所贴的敌人战斗节点。</summary>
		internal NCreature Target = null!;
	}

	/// <summary>
	/// 当前在播的目标侧舞台。
	///
	/// <para>与 <see cref="Active"/> 的分工：<see cref="Active"/> 收全部在播舞台
	/// （含施法者侧，用于换房 / 阵亡时统一收尾），这里只收目标侧 ——
	/// 因为只有目标侧需要按敌人做命中帧的锚点校正。</para>
	/// </summary>
	private static readonly List<TargetStage> TargetStages = new();

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

		// 命中帧校正：挂载时对齐一次还不够 —— 到真正结算伤害之间，敌人可能已经
		// 抖过（AnimShake）/ 缩放过（ScaleTo）/ 位移过，挂载那次算出的落点会过期
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 技能特效命中校正", (DamageReceivedEvent e) => AlignOnHit(e)));

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

			// 终结技：全屏演出会整屏接管时让位（黑幕会把常规特效整块盖掉），
			// 否则播放时间轴里给它们准备的常规特效。
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
	/// 全屏演出「判定不通过」时的补播入口。
	///
	/// <para>出牌瞬间（<c>CardPlayEnqueuingEvent</c>）单体特效已经因为演出开着而让位；
	/// 演出这边到 <c>Hook.ModifyAttackHitCount</c> 才做致死预判，若预判结论是「不播」，
	/// 大招就会只剩一段身体动作 —— 由 <see cref="HeidemarieCinematicRuntime"/> 调本方法补上。</para>
	/// </summary>
	/// <param name="card">刚打出的牌。</param>
	/// <param name="target">本次攻击的目标；全体敌人牌传 <c>null</c>，由本类自行展开。</param>
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
	/// 给一张牌建舞台：施法者一个、目标侧按 <see cref="ResolveTargets"/> 展开。
	/// </summary>
	private static void SpawnFor(CardModel card, Creature? target)
	{
		string key = card.GetType().Name;
		if (!HasEntry(key))
		{
			// 时间轴里没登记这张牌 —— 静默跳过，不刷日志
			return;
		}

		if (PlayedCards.Contains(card))
		{
			return;
		}

		Player? player = card.Owner;
		if (!IsLocalHeidemarie(player) || player!.Creature.IsDead)
		{
			return;
		}

		if (player.Creature.GetCreatureNode() is not { } ownerNode)
		{
			return;
		}

		Node2D? ownerAnchor = Spines.Find(ownerNode);
		if (ownerAnchor is not null && SpawnStage(ownerAnchor, key, OwnerBucket) is not null)
		{
			PlayedCards.Add(card);
		}

		foreach (Creature victim in ResolveTargets(card, target))
		{
			if (victim.GetCreatureNode() is not { } victimNode
				|| Spines.Find(victimNode) is not { } victimAnchor
				|| SpawnStage(victimAnchor, key, TargetBucket) is not { } targetStage)
			{
				continue;
			}

			// 舞台挂上去时原点落在敌人的**骨架原点**上，而图层位移在 CZN 源数据里
			// 是相对**受击点**标定的（见 heidemarie_effect_groups.gd 的
			// TARGET_OFFSET_OVERRIDES 注释）—— 先按当前受击点对齐一次，
			// 命中帧（AlignOnHit）再补一次。
			TargetStage tracked = new() { Stage = targetStage, Target = victimNode };
			TargetStages.Add(tracked);
			AlignToTargetSurface(tracked);
		}
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

	/// <summary>
	/// 在 <paramref name="anchor"/>（某个战斗单位的骨架节点）下建一个特效舞台。
	/// 舞台自己解析时间轴；没有任何可用图层时直接把节点回收并返回 <c>null</c>。
	/// </summary>
	/// <returns>建好的舞台节点；没建起来时为 <c>null</c>（调用方据此决定是否登记）。</returns>
	private static Node2D? SpawnStage(Node2D anchor, string key, string bucket)
	{
		PackedScene? scene = ResourceLoader.Load<PackedScene>(StageScenePath, null, ResourceLoader.CacheMode.Reuse);
		if (scene?.Instantiate(PackedScene.GenEditState.Disabled) is not Node2D stage)
		{
			Warn("缺少技能特效舞台场景：" + StageScenePath);
			return null;
		}

		anchor.AddChild(stage, forceReadableName: false, Node.InternalMode.Disabled);

		if (stage.Call("configure", DataPath, key, bucket).AsDouble() <= 0.0)
		{
			stage.QueueFree();
			return null;
		}

		// 特效亮度 / 不透明度：每建一个舞台就按当前设置调一次
		HeidemarieEffectMaterials.Apply(stage);

		Active.Add(stage);
		stage.Connect("finished", Callable.From(() => Release(stage)));
		stage.Call("start");
		return stage;
	}

	/// <summary>
	/// 立刻收掉所有在播的特效（设置里关掉「战斗特效」时调用，
	/// 挂到 <c>SkinSettingsUiRuntimeHooks.CleanupSkinEffectsRuntime</c>）。
	/// </summary>
	internal static void CancelAll() => Cleanup();

	// ──────────────────────────────────────────────────────────────────────────
	// 生命周期
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>舞台播完，回收节点。</summary>
	private static void Release(Node stage)
	{
		Active.Remove(stage);
		// 同步摘掉命中校正的配对，别让它一直攒着已经释放的舞台
		TargetStages.RemoveAll(tracked => tracked.Stage == stage || !GodotObject.IsInstanceValid(tracked.Stage));
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
		TargetStages.Clear();
		PlayedCards.Clear();
		Spines.Clear();
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 目标锚点校正（挂载时 + 命中帧）
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 命中帧（<c>Hook.AfterDamageReceived</c>）到点：把挨了这一次伤害的敌人身上
	/// 挂着的目标侧舞台再校正一次。
	///
	/// <para>舞台是在 <c>CardPlayEnqueuingEvent</c>（伤害结算<b>之前</b>）挂上的，
	/// 到真正命中之间敌人可能已经被抖过（<c>AnimShake</c> 改 <c>Visuals.Position</c>）、
	/// 缩放过（<c>ScaleTo</c> / Osty 体型变化）或位移过 ——
	/// 挂载时那次校正就此过期。这里用真实的伤害结算时刻补一次，
	/// 让落点跟着实际受击位置走。</para>
	/// </summary>
	private static void AlignOnHit(DamageReceivedEvent e)
	{
		if (TargetStages.Count == 0 || e.Target is null)
		{
			return;
		}

		foreach (TargetStage tracked in TargetStages)
		{
			if (!GodotObject.IsInstanceValid(tracked.Target))
			{
				continue;
			}

			// 按 Creature 实例配对：同一次伤害可能同时打中多个敌人（全体牌），
			// 每个敌人都要被各自校正一次，所以这里不 break。
			if (!ReferenceEquals(tracked.Target.Entity, e.Target))
			{
				continue;
			}

			AlignToTargetSurface(tracked);
		}
	}

	/// <summary>
	/// 把目标侧舞台的原点对到敌人的官方受击点
	/// （<c>NCreature.VfxSpawnPosition</c>，即场景里的 <c>%CenterPos</c>）。
	///
	/// <para>求法：先取受击点的全局坐标，再用舞台父节点的全局变换<b>求逆</b>换算成
	/// 局部坐标，直接设给舞台。走变换求逆而不是自己算差值，是为了让敌人自身的缩放
	/// 与翻转一并被还原 —— 舞台本来就挂在骨架下，不该在这里把 <c>Scale</c> 掰回 1。</para>
	///
	/// <para>任何一环取不到都静默跳过：落点校正失败只影响观感，绝不能中断演出，
	/// 也不该刷日志。</para>
	/// </summary>
	private static void AlignToTargetSurface(TargetStage tracked)
	{
		try
		{
			Node2D stage = tracked.Stage;
			NCreature target = tracked.Target;
			if (!GodotObject.IsInstanceValid(stage) || !GodotObject.IsInstanceValid(target))
			{
				return;
			}

			if (stage.GetParent() is not Node2D parentNode)
			{
				return;
			}

			if (!CombatTargetSurfaceAnchorRuntime.TryResolveSurfaceGlobal(
					target,
					CombatTargetSurfaceAnchorMode.VfxSpawnPosition,
					out Vector2 surfaceGlobal))
			{
				return;
			}

			stage.Call("set_anchor_local", parentNode.GetGlobalTransform().Inverse() * surfaceGlobal);
		}
		catch (Exception ex)
		{
			Warn("目标锚点校正失败：" + ex.Message);
		}
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
			List<string> paths = new() { StageScenePath, GroupLibraryPath };
			Dictionary<string, List<string>> groupSkeletons = LoadGroupSkeletons();
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
								string path = SkelDataRoot + skeleton + "_skel_data.tres";
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

	/// <summary>
	/// 读特效组库，建「组键 → 该组用到的骨架名」索引。
	///
	/// <para>卡牌映射（v4）只写组键，骨架名只存在组库里一份；预加载与运行时展开都靠它。
	/// 组库缺失时返回空表 —— 此时预加载会退化成「只加载舞台场景」，
	/// 特效仍能运行（首次出牌才现加载），只是慢一点。</para>
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
