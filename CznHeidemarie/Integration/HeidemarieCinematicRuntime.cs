using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznHeidemarie.Integration;

/// <summary>
/// 全屏演出（Cinematic 全屏接管）的接入层。
///
/// <para>对应参考 mod 的两处实现：
/// <c>CznTipheraCharacter.Compatibility.TipheraCombatCinematicRuntime</c> 与
/// <c>CznNineCharacter.Presentation.NineCinematicRuntime</c>。
/// 架构一致：卡牌 → 演出路线（ug / ux）→ 实例化
/// <c>res://CznHeidemarie/cinematic/heidemarie_cinematic_stage.tscn</c>，
/// 由舞台上的 GDScript 合成器按毫秒时间轴推进一批独立特效骨架。</para>
///
/// <para>相对参考 mod 做的减法（详见 <c>参考mod动画实现清单.md</c> 第八节）：</para>
/// <list type="bullet">
///   <item>不补 <c>Hook.BeforeCardPlayed</c> / <c>AfterCardPlayed</c> 拦牌 ——
///   卡牌自身逻辑（花费、<c>OnPlay</c>）完全不受演出影响；</item>
///   <item>没有官方 OGG 音频（素材区只有 FMOD <c>.bank</c>），音频接口留空；</item>
///   <item>时间轴是手写的（<c>heidemarie_{ug,ux}.json</c>），不是 cfx 提取产物。</item>
/// </list>
///
/// <para><b>但伤害时机必须跟着演出走</b>：原生出牌演出里，
/// <c>AttackCommand.Execute</c> 是「触发施法者动作 → 等 <c>waitTime</c> → 结算伤害」，
/// 而本工程的骨架动作被黑幕接管、原生 <c>waitTime</c> 与演出时间轴毫无关系，
/// 于是伤害总是先落地、动画还在后面慢慢放。为此本类对外提供两个闸门
/// （<see cref="TryWaitForHit"/> / <see cref="WaitForPresentationEnd"/>，
/// 由 <c>Registration/HeidemarieCinematicPresentationPatch.cs</c> 接到游戏上）：
/// 攻击改成等演出的命中帧，攻击尾巴等整段演出收尾。演出不存在或已降级时不拦，
/// 一律走原生等待。</para>
///
/// <para>保留的参考 mod 主干：token 互斥、watchdog 超时、<c>TreeExiting</c> 兜底、
/// 本体租借与归还、契约单调性校验、以及**任一环节失败就交回原生出牌演出**的降级策略。</para>
/// </summary>
internal static class HeidemarieCinematicRuntime
{
	/// <summary>舞台场景路径（合成器脚本与时间轴都在同目录）。</summary>
	private const string StageScenePath = "res://CznHeidemarie/cinematic/heidemarie_cinematic_stage.tscn";

	/// <summary>时间轴 JSON 目录。</summary>
	private const string TimelineRoot = "res://CznHeidemarie/cinematic/";

	/// <summary>特效骨架 <c>_skel_data.tres</c> 目录。</summary>
	private const string SkelDataRoot = "res://CznHeidemarie/animations/effects/";

	/// <summary>预加载句柄的键，同一时刻只保留一份。</summary>
	private const string PreloadKey = "heidemarie_cinematic";

	/// <summary>挂载阶段的看门狗超时（秒），对应参考 mod 的 10 秒。</summary>
	private const float MountTimeoutSeconds = 10f;

	/// <summary>演出阶段的看门狗宽限（秒）。</summary>
	private const float FinishTimeoutPaddingSeconds = 2f;

	/// <summary>终焉剑舞的演出路线。</summary>
	private const string RouteUg = "ug";

	/// <summary>破军的演出路线。</summary>
	private const string RouteUx = "ux";

	/// <summary>合成器必须提供的方法（缺一即视为场景不兼容）。</summary>
	private static readonly string[] RequiredMethods =
	{
		"configure",
		"get_runtime_contract",
		"start_stage",
		"cancel_stage",
		"set_paused",
	};

	/// <summary>合成器必须提供的信号（缺一即视为场景不兼容）。</summary>
	private static readonly string[] RequiredSignals =
	{
		"presentation_started",
		"hit_reached",
		"actors_restored",
		"stage_finished",
		"resources_finished",
	};

	/// <summary>一段进行中的演出。字段语义与参考 mod 的 <c>Phase</c> 一致。</summary>
	private sealed class Phase
	{
		/// <summary>舞台节点（CanvasLayer）。</summary>
		internal Node? Stage;

		/// <summary>施法者的战斗节点，用于在看门狗里判断玩家是否还在。</summary>
		internal NCreature? Creature;

		/// <summary>施法者本体骨架（被借用期间隐藏）。</summary>
		internal Node2D? Body;

		/// <summary>借用前本体的可见性，归还时原样恢复。</summary>
		internal bool BodyWasVisible;

		/// <summary>施法者玩家。</summary>
		internal Player? Player;

		/// <summary>演出所在房间，用于比对其是否还是当前房间。</summary>
		internal NCombatRoom? Room;

		/// <summary>互斥令牌。</summary>
		internal int Token;

		/// <summary>演出路线（<c>ug</c> / <c>ux</c>），用于与攻击触发器配对。</summary>
		internal string Route = string.Empty;

		/// <summary>触发本段演出的卡牌，用于把演出尾巴挂回同一次攻击。</summary>
		internal CardModel? Card;

		/// <summary>
		/// 命中闸门：演出时钟推进到命中帧时完成，攻击结算等它
		/// （见 <see cref="TryWaitForHit"/>）。
		///
		/// <para>用 <c>RunContinuationsAsynchronously</c>：信号是在舞台的
		/// <c>_process</c> 里发的，默认内联续体等于在遍历图层的中途插进伤害结算。</para>
		/// </summary>
		internal readonly TaskCompletionSource Hit = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// 收尾闸门：舞台播完 / 被打断 / 看门狗超时都完成它，攻击尾巴等它
		/// （见 <see cref="WaitForPresentationEnd"/>）。
		/// </summary>
		internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>演出总时长（秒），来自 <c>get_runtime_contract</c>。</summary>
		internal double DurationSeconds;

		/// <summary>挂载时刻，用于「尚未开演」的超时判定。</summary>
		internal ulong MountMsec = Time.GetTicksMsec();

		/// <summary>正式开演时刻，0 表示还在挂载阶段。</summary>
		internal ulong StartMsec;

		/// <summary>本体已归还标记。</summary>
		internal bool ActorsReleased;

		/// <summary>已收尾标记。</summary>
		internal bool Ended;

		/// <summary>每帧看门狗回调，收尾时必须摘掉。</summary>
		internal Action? FrameCallback;
	}

	/// <summary>骨架查找与缓存，可跨演出复用。</summary>
	private static readonly CombatCreatureSpineResolver Spines = new();

	/// <summary>事件订阅句柄，保持引用以防被 GC 回收。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>当前演出；同一时刻至多一段。</summary>
	private static Phase? _phase;

	/// <summary>令牌计数器。</summary>
	private static int _token;

	/// <summary>
	/// 订阅出牌事件与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		// 打出去的牌：判断是不是走全屏演出的两张大招
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 全屏演出", (CardPlayCommittedEvent e) => TryPlay(e.Card)));

		// 进战斗时把时间轴引用到的骨架先预热，避免第一次放大招才现加载
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 全屏演出预加载", (CombatStartingEvent _) => Preload()));

		// 生命周期兜底：离房 / 阵亡 / 胜利都要收尾
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 全屏演出入房", (RoomEnteringEvent _) => CancelFor(null, "room_entering")));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 全屏演出阵亡", (PlayerDeathResolvedEvent e) => CancelFor(e.Player, "player_death")));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 全屏演出胜利", (PlayerCombatVictoryEvent e) => CancelFor(e.Player, "player_victory")));
	}

	/// <summary>设置里关掉「出牌演出」时立即收尾（挂到设置面板的钩子上）。</summary>
	internal static void ApplyCardPlayPresentation(bool enabled)
	{
		if (!enabled)
		{
			CancelFor(null, "settings_disabled");
		}
	}

	/// <summary>当前是否有全屏演出在跑。</summary>
	internal static bool IsActive => _phase is not null;

	/// <summary>
	/// 攻击触发器的命中闸门配对表：角色自己的攻击触发器 → 对应演出路线。
	///
	/// <para>这两个触发器只被走全屏演出的两张牌使用（「绝响」→ <c>XAttack</c> / ux，
	/// 「解放极光」→ <c>GAttack</c> / ug，见 <c>HeidemarieAttackPlan</c>），
	/// 所以按触发器配对天然只命中大招：普通打击（<c>Attack</c> / <c>Attack2</c> /
	/// <c>U1Attack</c> / <c>U3Attack</c>）与技能动作一律不受影响。</para>
	/// </summary>
	private static string? RouteForTrigger(string triggerName)
	{
		return triggerName switch
		{
			"XAttack" => RouteUx,
			"GAttack" => RouteUg,
			_ => null,
		};
	}

	/// <summary>
	/// 取「这次攻击要等到的命中时刻」。
	///
	/// <para>调用方（<c>CreatureCmd.TriggerAnim</c> 的补丁）拿到 <c>true</c> 时，
	/// 应当改等 <paramref name="task"/>，而不是等原生骨架动画播完 <c>waitTime</c> ——
	/// 原生等待与演出时间轴无关，正是「伤害先出、动画后放」的根源。</para>
	///
	/// <para>没有演出、触发器不属于本段演出、或演出已经收尾时返回 <c>false</c>，交回原生等待。</para>
	/// </summary>
	/// <param name="player">发起攻击的玩家。</param>
	/// <param name="triggerName">攻击触发器名。</param>
	/// <param name="task">命中时刻；未接管时为已完成任务。</param>
	internal static bool TryWaitForHit(Player player, string triggerName, out Task task)
	{
		task = Task.CompletedTask;

		Phase? phase = _phase;
		string? route = RouteForTrigger(triggerName);
		if (phase is null || phase.Ended || route is null || phase.Route != route || phase.Player != player)
		{
			return false;
		}

		task = phase.Hit.Task;
		return true;
	}

	/// <summary>
	/// 取「本次攻击所属演出播完」的任务，让攻击尾巴（<c>Hook.AfterAttack</c>）
	/// 等整段演出收尾再结束这次出牌 —— 与两个参考 mod 的
	/// <c>WaitForFinish</c> / <c>Completion.Finished</c> 等价。
	///
	/// <para>没有匹配的演出时返回已完成的任务，调用方不必特判。</para>
	/// </summary>
	internal static Task WaitForPresentationEnd(CardModel? card)
	{
		Phase? phase = _phase;
		if (phase is null || phase.Ended || phase.Card is null || card is null)
		{
			return Task.CompletedTask;
		}

		// 比实例也比 Id：模型在牌堆间移动时可能是同一 Id 的不同实例。
		bool sameCard = ReferenceEquals(phase.Card, card) || phase.Card.Id == card.Id;
		return sameCard ? phase.Finished.Task : Task.CompletedTask;
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 触发
	// ──────────────────────────────────────────────────────────────────────────

	private static void TryPlay(CardModel? card)
	{
		try
		{
			string? route = ResolveRoute(card);
			if (route is null || !IsEnabled())
			{
				return;
			}

			Player? player = card!.Owner;
			if (!IsLocalHeidemarie(player) || player!.Creature.IsDead)
			{
				return;
			}

			NCombatRoom? room = NCombatRoom.Instance;
			if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
			{
				return;
			}

			if (player.Creature.GetCreatureNode() is not { } creatureNode)
			{
				return;
			}

			Node2D? anchor = Spines.Find(creatureNode);
			if (anchor is null || !CombatCinematicFormation.TryGetViewportPosition(anchor, out Vector2 ownerPosition))
			{
				Warn("缺少施法者锚点，交回原生出牌演出");
				return;
			}

			Node? stage = null;
			Phase? phase = null;
			try
			{
				PackedScene? scene = ResourceLoader.Load<PackedScene>(StageScenePath, null, ResourceLoader.CacheMode.Reuse);
				stage = scene?.Instantiate(PackedScene.GenEditState.Disabled);
				if (stage is null)
				{
					throw new InvalidOperationException("缺少全屏演出舞台场景：" + StageScenePath);
				}

				foreach (string method in RequiredMethods)
				{
					if (!stage.HasMethod(method))
					{
						throw new InvalidOperationException("舞台场景缺少方法：" + method);
					}
				}

				foreach (string signalName in RequiredSignals)
				{
					if (!stage.HasSignal(signalName))
					{
						throw new InvalidOperationException("舞台场景缺少信号：" + signalName);
					}
				}

				room.AddChild(stage, forceReadableName: false, Node.InternalMode.Disabled);

				if (!stage.Call("configure", route, anchor, ownerPosition).AsBool())
				{
					throw new InvalidOperationException("舞台拒绝配置（资源或时序不齐）");
				}

				// 排他：先收掉上一段，再让出牌特写立绘让位
				CancelFor(player, "replacement");
				HeidemarieBattleReadyRuntime.Cleanup();

				phase = new Phase
				{
					Stage = stage,
					Creature = creatureNode,
					Body = anchor,
					BodyWasVisible = anchor.Visible,
					Player = player,
					Room = room,
					Token = NextToken(),
					Route = route,
					Card = card,
				};
				_phase = phase;

				Godot.Collections.Dictionary contract = stage.Call("get_runtime_contract").AsGodotDictionary();
				phase.DurationSeconds = contract["duration_seconds"].AsDouble();
				double hitSeconds = contract["hit_release_seconds"].AsDouble();
				double actorReleaseSeconds = contract["battlefield_actor_release_seconds"].AsDouble();
				ValidateContract(phase, hitSeconds, actorReleaseSeconds);

				Phase accepted = phase;
				stage.Connect("presentation_started", Callable.From(() => Guard(accepted, () => OnStarted(accepted))));
				stage.Connect("hit_reached", Callable.From(() => Guard(accepted, () => OnHitReached(accepted))));
				stage.Connect("actors_restored", Callable.From(() => Guard(accepted, () => RestoreActors(accepted))));
				stage.Connect("stage_finished", Callable.From(() => End(accepted, "finished", cancelled: false)));
				stage.Connect("resources_finished", Callable.From(() => End(accepted, "resources_finished", cancelled: false)));
				stage.TreeExiting += () => End(accepted, "tree_exit", cancelled: true);

				phase.FrameCallback = () => Guard(accepted, () => Watch(accepted));
				room.GetTree().ProcessFrame += phase.FrameCallback;

				// 借用本体：隐藏，等 actors_restored（或收尾时）归还
				anchor.Visible = false;

				if (!stage.Call("start_stage").AsBool())
				{
					throw new InvalidOperationException("舞台拒绝 start_stage");
				}

				Log($"全屏演出开始 route={route} token={phase.Token} duration={phase.DurationSeconds:0.###}s");
				return;
			}
			catch (Exception ex)
			{
				Warn("全屏演出挂载失败，交回原生出牌演出：" + ex.Message);
				if (phase is not null)
				{
					End(phase, "mount_failure", cancelled: true);
				}
				else if (GodotObject.IsInstanceValid(stage))
				{
					stage!.QueueFree();
				}

				return;
			}
		}
		catch (Exception ex)
		{
			Warn("全屏演出异常：" + ex.Message);
		}
	}

	/// <summary>
	/// 把卡牌映射到演出路线；不是大招就返回 <c>null</c>。
	///
	/// <para>「解放极光」挂 ug 全屏演出（设计文档指定）；「绝响」走 ux。
	/// 原先绑定在已移除的测试卡（终焉剑舞 / 破军）上，现改绑正式卡。</para>
	/// </summary>
	private static string? ResolveRoute(CardModel? card)
	{
		return card switch
		{
			HeidemarieLiberatedAurora => RouteUg,
			HeidemarieFinalEcho => RouteUx,
			_ => null,
		};
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 回调
	// ──────────────────────────────────────────────────────────────────────────

	private static void OnStarted(Phase phase)
	{
		if (_phase != phase || phase.StartMsec != 0)
		{
			return;
		}

		phase.StartMsec = Time.GetTicksMsec();
		Log($"正式开演 route token={phase.Token} 预演耗时={phase.StartMsec - phase.MountMsec}ms");
	}

	/// <summary>
	/// 演出推进到命中帧：放行等在那里的攻击结算
	/// （见 <see cref="TryWaitForHit"/>）。
	/// </summary>
	private static void OnHitReached(Phase phase)
	{
		if (phase.Hit.TrySetResult())
		{
			Log($"命中放行 route={phase.Route} token={phase.Token}");
		}
	}

	/// <summary>把本体交还给战斗：恢复可见性并触发待机，避免停在演出最后一帧。</summary>
	private static void RestoreActors(Phase phase)
	{
		if (phase.ActorsReleased)
		{
			return;
		}

		phase.ActorsReleased = true;
		try
		{
			if (GodotObject.IsInstanceValid(phase.Body))
			{
				phase.Body!.Visible = phase.BodyWasVisible;
			}

			if (GodotObject.IsInstanceValid(phase.Creature)
				&& phase.Creature!.IsInsideTree()
				&& phase.Player?.Creature.IsAlive == true)
			{
				phase.Creature.SetAnimationTrigger("Idle");
			}
		}
		catch (Exception ex)
		{
			Warn("归还本体失败：" + ex.Message);
		}
	}

	private static void Guard(Phase phase, Action action)
	{
		if (phase.Ended)
		{
			return;
		}

		try
		{
			action();
		}
		catch (Exception ex)
		{
			Warn("演出回调异常：" + ex.Message);
			End(phase, "callback_failure", cancelled: true);
		}
	}

	/// <summary>
	/// 每帧看门狗：房间还在 / 玩家没死 / 设置没关 / 没超时，任一不满足即强制收尾。
	/// </summary>
	private static void Watch(Phase phase)
	{
		if (_phase != phase)
		{
			return;
		}

		bool alive = GodotObject.IsInstanceValid(phase.Room)
			&& phase.Room!.IsInsideTree()
			&& NCombatRoom.Instance == phase.Room
			&& GodotObject.IsInstanceValid(phase.Body)
			&& phase.Player?.Creature.IsAlive == true
			&& IsEnabled();

		double elapsed = (Time.GetTicksMsec() - (phase.StartMsec == 0 ? phase.MountMsec : phase.StartMsec)) / 1000.0;
		double limit = phase.StartMsec == 0
			? MountTimeoutSeconds
			: phase.DurationSeconds + FinishTimeoutPaddingSeconds;

		if (!alive || elapsed > limit)
		{
			End(phase, alive ? "watchdog_timeout" : "owner_or_settings_changed", cancelled: true);
		}
	}

	/// <summary>取消当前演出（可按玩家过滤）。</summary>
	private static void CancelFor(Player? player, string reason)
	{
		Phase? phase = _phase;
		if (phase is not null && (player is null || phase.Player == player))
		{
			End(phase, reason, cancelled: true);
		}

		Spines.Clear();
	}

	/// <summary>
	/// 收尾：先取消舞台 → 再归还本体 → 最后释放节点。
	/// 用 <c>try/finally</c> 保证本体一定归还，不会把角色永久藏住。
	/// </summary>
	private static void End(Phase? phase, string reason, bool cancelled)
	{
		if (phase is null || phase.Ended)
		{
			return;
		}

		phase.Ended = true;
		if (_phase == phase)
		{
			_phase = null;
		}

		// 两道闸门无条件放行：演出正常播完时它们早已由 hit_reached / stage_finished 完成，
		// 这里兜的是取消、降级、看门狗超时等异常路径 ——
		// 宁可提前放行，也绝不能把等命中的攻击或等收尾的钩子吊死。
		phase.Hit.TrySetResult();
		phase.Finished.TrySetResult();

		try
		{
			if (phase.FrameCallback is not null && GodotObject.IsInstanceValid(phase.Room))
			{
				SceneTree? tree = phase.Room!.GetTree();
				if (tree is not null)
				{
					tree.ProcessFrame -= phase.FrameCallback;
				}
			}
		}
		catch
		{
			// 摘回调失败不该阻塞收尾
		}

		phase.FrameCallback = null;

		try
		{
			if (GodotObject.IsInstanceValid(phase.Stage))
			{
				phase.Stage!.Call("cancel_stage");
			}
		}
		catch (Exception ex)
		{
			Warn("舞台取消失败：" + ex.Message);
		}
		finally
		{
			RestoreActors(phase);
			if (GodotObject.IsInstanceValid(phase.Stage))
			{
				phase.Stage!.QueueFree();
			}

			phase.Stage = null;
			Log($"全屏演出结束 reason={reason} cancelled={cancelled}");
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 预加载 / 契约校验 / 工具
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 进战斗时把两条路线的舞台场景与全部图层骨架做线程预加载。
	/// 只预热时间轴**实际引用**的骨架，不会把 142 组特效全拉进来。
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
			CollectRouteResources(RouteUg, paths);
			CollectRouteResources(RouteUx, paths);
			CombatOverlayPreload.HoldThreaded(
				PreloadKey,
				directoryRoots: null,
				explicitPaths: paths,
				onCompleted: () => Log($"全屏演出预加载完成，共 {paths.Count} 项"));
		}
		catch (Exception ex)
		{
			CombatOverlayPreload.Release(PreloadKey);
			Warn("全屏演出预加载跳过：" + ex.Message);
		}
	}

	private static void CollectRouteResources(string route, List<string> paths)
	{
		string jsonPath = TimelineRoot + "heidemarie_" + route + ".json";
		if (!global::Godot.FileAccess.FileExists(jsonPath) && !ResourceLoader.Exists(jsonPath))
		{
			return;
		}

		using JsonDocument document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(jsonPath));
		if (!document.RootElement.TryGetProperty("layers", out JsonElement layers))
		{
			return;
		}

		foreach (JsonElement layer in layers.EnumerateArray())
		{
			if (layer.TryGetProperty("name", out JsonElement nameElement)
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

	/// <summary>
	/// 契约单调性校验：<c>0 &lt; hit &lt;= duration</c> 且 <c>hit &lt;= actorRelease &lt;= duration</c>。
	/// 不满足直接抛，由调用方降级回原生出牌演出。
	/// </summary>
	private static void ValidateContract(Phase phase, double hitSeconds, double actorReleaseSeconds)
	{
		double duration = phase.DurationSeconds;
		bool valid = double.IsFinite(duration)
			&& double.IsFinite(hitSeconds)
			&& double.IsFinite(actorReleaseSeconds)
			&& duration > 0.0
			&& hitSeconds > 0.0
			&& hitSeconds <= duration
			&& actorReleaseSeconds >= hitSeconds
			&& actorReleaseSeconds <= duration;

		if (!valid)
		{
			throw new InvalidOperationException(
				$"演出时序契约非法：duration={duration} hit={hitSeconds} actorRelease={actorReleaseSeconds}");
		}
	}

	private static bool IsEnabled()
	{
		return HeidemarieSettings.CurrentCharacter.EffectiveEffectsEnabled
			&& HeidemarieSettings.CurrentCharacter.ShowCardPlayPresentation;
	}

	private static bool IsLocalHeidemarie(Player? player)
	{
		if (player?.Character is not Heidemarie)
		{
			return false;
		}

		return LocalContext.IsMe(player);
	}

	private static int NextToken()
	{
		_token++;
		if (_token == 0)
		{
			_token++;
		}

		return _token;
	}

	private static void Log(string message)
	{
		HeidemarieIntegration.Log.Info("全屏演出: " + message);
	}

	private static void Warn(string message)
	{
		HeidemarieIntegration.Log.Warn("全屏演出: " + message);
	}
}
