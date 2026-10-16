using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
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
///   <item>舞台自身不带音频（gd 侧无音频接口）：演出音效由 C# 挂在
///   <c>presentation_started</c>（演出 epoch 零点）起播、取消时停播，与动画
///   严格同起；音效长于画面时尾部自然放完，攻击尾巴只等舞台不等音效；</item>
///   <item><c>heidemarie_{ug,ux}.json</c> 由 <c>.srmd + .cfx</c> 提取，舞台运行时消费
///   图层、相机和时序适配段。</item>
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

	/// <summary>
	/// 特效组库：组键 → 组的图层构成 + 挂载参数。
	/// 时间轴已瘦身为「组键 + 窗口」，图层构成都从这里取。
	/// </summary>
	private const string GroupLibraryPath = "res://CznHeidemarie/effects/heidemarie_effect_groups.json";

	/// <summary>预加载句柄的键，同一时刻只保留一份。</summary>
	private const string PreloadKey = "heidemarie_cinematic";

	/// <summary>挂载阶段的看门狗超时（秒），对应参考 mod 的 10 秒。</summary>
	private const float MountTimeoutSeconds = 10f;

	/// <summary>演出阶段的看门狗宽限（秒）。</summary>
	private const float FinishTimeoutPaddingSeconds = 2f;

	/// <summary>终焉剑舞的演出路线。</summary>
	internal const string RouteUg = "ug";

	/// <summary>破军的演出路线。</summary>
	internal const string RouteUx = "ux";

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
		"takeover_reached",
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

		/// <summary>本体是否已被收起（立刻收起或等 <c>takeover_reached</c> 后收起）。</summary>
		internal bool BodyHidden;

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
		// 触发点不在这里：全屏演出改由攻击指令钩子（Hook.ModifyAttackHitCount）
		// 判定并起手，见 Registration/HeidemarieCombatPresentationPatch.cs 与
		// TryBeginFromAttack —— 只有拿到 AttackCommand 才能做「可击杀敌人才播」的致死预判。

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
	/// <para>走全屏演出的两张牌各占一个触发器：「解放极光」→ <c>GAttack</c> / ug，
	/// 「剑之乐园」→ <c>U3Attack</c> / ux（见 <c>HeidemarieAttackPlan</c>）。
	/// 其余攻击档（<c>Attack</c> / <c>Attack2</c> / <c>U1Attack</c>）与技能动作不在表内，
	/// 因此天然不受影响。</para>
	///
	/// <para><b>注意</b>：<c>U3Attack</c> 是「剑之乐园」的专属触发器 —— 目前顶档（u3）
	/// 只有它一张牌，配对唯一。日后若有别的牌改用 u3 档，必须给它另开一个触发器，
	/// 否则那段牌的伤害也会被挂到本演出上。</para>
	/// </summary>
	private static string? RouteForTrigger(string triggerName)
	{
		return triggerName switch
		{
			"U3Attack" => RouteUx,
			"GAttack" => RouteUg,
			_ => null,
		};
	}

	/// <summary>
	/// 这次攻击的全屏演出是否已经接管并在跑。
	///
	/// <para>战斗音效据此让位：舞台接管时，演出音效改由舞台在
	/// <c>presentation_started</c> 起播（见 <see cref="OnStarted"/>），
	/// <c>CreatureCmd.TriggerAnim</c> 那条路再播同一段就会双响。
	/// 舞台没起来（致死预判不通过、挂载失败等降级路径）时返回 <c>false</c>，
	/// 音效走 TriggerAnim 老路，保证降级路径也有声音。</para>
	///
	/// <para>挂载先于骨架动作（<c>Hook.ModifyAttackHitCount</c> 在
	/// <c>AttackCommand.Execute</c> 里紧跟 <c>Hook.BeforeAttack</c> 调用），
	/// 所以 TriggerAnim 到来时若舞台已接手，这里必然看得到。</para>
	/// </summary>
	internal static bool IsStagePresenting(Player? player, string triggerName)
	{
		Phase? phase = _phase;
		string? route = RouteForTrigger(triggerName);
		return phase is not null && !phase.Ended && route is not null && phase.Route == route && phase.Player == player;
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

	/// <summary>
	/// 攻击指令就绪时判定这次攻击要不要走全屏演出（由
	/// <c>Hook.ModifyAttackHitCount</c> 的补丁调用，见
	/// <c>Registration/HeidemarieCombatPresentationPatch.cs</c>）。
	///
	/// <para>判定链：① 这张牌有没有绑定演出路线；② 特效 / 出牌演出开关；
	/// ③ <b>攻击牌全屏演出时机</b> —— <c>Always</c> 直接放行，
	/// <c>LethalOnly</c> 走 CznCore 的致死预判（随机目标、反伤类钩子等
	/// 不可靠情形一律判为不放行）。任一条不过就什么都不做，
	/// 攻击按原生演出走（<c>WithAttackerAnim</c> 里那套动作与等待照旧）。</para>
	/// </summary>
	/// <param name="card">攻击的来源牌。</param>
	/// <param name="command">已经组好的攻击指令。</param>
	/// <param name="combatState">当前战斗状态。</param>
	/// <param name="hitCount">本次攻击的段数（致死预判需要）。</param>
	internal static void TryBeginFromAttack(CardModel? card, AttackCommand command, ICombatState combatState, int hitCount)
	{
		try
		{
			string? route = ResolveRoute(card);
			if (route is null || !IsEnabled())
			{
				return;
			}

			// 同一张牌的后续攻击指令不再重复起手 ——「剑之乐园」是逐敌循环发多个
			// AttackCommand，每个敌人都会走到这里；而 TryPlay 开头会
			// CancelFor(player, "replacement") 收掉上一段，不去重的话第一段刚起手
			// 就被第二段取消，最终一段都放不出来。
			if (IsSameCardAsActivePerformance(card))
			{
				return;
			}

			if (card!.Owner is not { } player || !IsLocalHeidemarie(player) || player.Creature.IsDead)
			{
				return;
			}

			bool allowed = CombatAttackFullScreenPresentationPolicy.Allows(
				HeidemariePresentationHost.Gates,
				HeidemariePresentationHost.Audience,
				card,
				command,
				combatState,
				hitCount,
				HeidemariePresentationHost.AttackTargets,
				out CombatAttackLethalForecastResult forecast,
				Warn);

			if (!allowed)
			{
				Log($"全屏演出跳过 route={route} 预判={forecast.Forecast}（{forecast.Reason}）");

				// 演出不接管，但单体特效那边已经因为「演出开着」让位了 ——
				// 这里补播一次常规特效，否则大招会退化成「只有一段身体动作」。
				HeidemarieMoveEffectsRuntime.PlayFallback(card!, command.CardPlay?.Target);
				return;
			}

			TryPlay(card);
		}
		catch (Exception ex)
		{
			Warn("全屏演出判定异常，交回原生演出：" + ex.Message);
		}
	}

	/// <summary>
	/// 当前正在跑的演出是不是就由这张牌发起。
	///
	/// <para>比实例也比 Id：模型在牌堆间移动时可能是同一 Id 的不同实例
	/// （与 <see cref="WaitForPresentationEnd"/> 同一口径）。</para>
	/// </summary>
	private static bool IsSameCardAsActivePerformance(CardModel? card)
	{
		Phase? phase = _phase;
		if (phase is null || phase.Ended || phase.Card is null || card is null)
		{
			return false;
		}

		return ReferenceEquals(phase.Card, card) || phase.Card.Id == card.Id;
	}

	/// <summary>
	/// 这张牌有没有绑定全屏演出路线。
	/// 「左侧角色演出」选「带全屏演出的卡牌」那一档时用它筛牌。
	/// </summary>
	internal static bool HasCinematicRoute(CardModel? card) => ResolveRoute(card) is not null;

	/// <summary>
	/// 这张牌打出时，全屏演出**会不会接管画面**。
	///
	/// <para>单体特效的触发点（<c>CardPlayEnqueuingEvent</c>）早于本类的演出判定，
	/// 它拿不到「致死预判是否通过」的结论，只能先问一句「演出开不开」：
	/// 演出开着就让位（屏幕会被黑幕整块盖住，再叠常规特效只会互相打架），
	/// 演出关着就照常播常规特效。</para>
	///
	/// <para>「演出开着但预判不通过」这一档由
	/// <see cref="HeidemarieMoveEffectsRuntime.PlayFallback"/> 补播，见
	/// <see cref="TryBeginFromAttack"/>。</para>
	/// </summary>
	internal static bool WillTakeOver(CardModel? card) => HasCinematicRoute(card) && IsEnabled();

	/// <summary>立刻收尾当前演出（设置里关掉「战斗特效 / 出牌演出」时调用）。</summary>
	internal static void CancelAll() => CancelFor(null, "settings_disabled");

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

				// 特效亮度 / 不透明度：整段演出按设置统一调一次
				HeidemarieEffectMaterials.Apply(stage);

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
				// 黑幕接管时刻：> 0 表示开场有一段「本体还在画面上」的起手，本体要延后隐藏。
				// 老场景没有这个键，缺省按 0（立刻接管）处理。
				double takeoverSeconds = contract.ContainsKey("takeover_seconds")
					? contract["takeover_seconds"].AsDouble()
					: 0.0;
				ValidateContract(phase, hitSeconds, actorReleaseSeconds);

				Phase accepted = phase;
				stage.Connect("takeover_reached", Callable.From(() => Guard(accepted, () => HideBody(accepted))));
				stage.Connect("presentation_started", Callable.From(() => Guard(accepted, () => OnStarted(accepted))));
				stage.Connect("hit_reached", Callable.From(() => Guard(accepted, () => OnHitReached(accepted))));
				stage.Connect("actors_restored", Callable.From(() => Guard(accepted, () => RestoreActors(accepted))));
				stage.Connect("stage_finished", Callable.From(() => End(accepted, "finished", cancelled: false)));
				stage.Connect("resources_finished", Callable.From(() => End(accepted, "resources_finished", cancelled: false)));
				stage.TreeExiting += () => End(accepted, "tree_exit", cancelled: true);

				phase.FrameCallback = () => Guard(accepted, () => Watch(accepted));
				room.GetTree().ProcessFrame += phase.FrameCallback;

				// 借用本体。
				// 默认立刻隐藏（演出从第一帧就接管画面）；
				// 但 ux 的时间轴声明了「黑幕到 1233.3ms 才压下来」—— 在那之前官方给
				// 本体留了一段没有特效层的起手，本体必须留在画面上，
				// 所以改成等舞台发 takeover_reached 再隐藏。
				if (double.IsFinite(takeoverSeconds) && takeoverSeconds > 0.0)
				{
					Log($"本体延后隐藏，等舞台接管 route={route} takeover={takeoverSeconds:0.###}s");
				}
				else
				{
					anchor.Visible = false;
					phase.BodyHidden = true;
				}

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
	/// 把卡牌映射到演出路线；不是终结技就返回 <c>null</c>。
	///
	/// <para>「解放极光」挂 ug（终焉剑舞；身体 <c>ug_attack</c>，触发器 <c>GAttack</c>）；
	/// 「剑之乐园」挂 ux（破军；身体 <c>u3_attack_*</c>，触发器 <c>U3Attack</c>）。</para>
	///
	/// <para>2026-10-15 变更：ux 原绑在「绝响」上，按设计改挂「剑之乐园」，
	/// 绝响不再触发任何全屏演出（它的单体特效改走重·斩击 u1 档）。</para>
	/// </summary>
	private static string? ResolveRoute(CardModel? card)
	{
		return card switch
		{
			HeidemarieLiberatedAurora => RouteUg,
			HeidemarieSwordParadise => RouteUx,
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

		// 演出音效与动画同起：epoch 零点开播。音效均长于画面约 2.3s
		// （ug_attack 7.8s / 演出 5.5s，ux_attack 10.4s / 演出 8.2s），
		// 尾部在舞台收尾后自然放完 —— 攻击尾巴只等 stage_finished，不等音效。
		// 取消 / 打断路径在 End(cancelled) 里停播。
		HeidemarieCombatAudio.PlayCinematicSfx(phase.Player, phase.Route);
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

	/// <summary>
	/// 舞台已完成画面接管（黑幕开始压下来）——把本体收起来。
	///
	/// <para>ux 的官方时序在开场给本体留了一段「还没有任何特效层」的起手
	/// （0 ~ 1233.3ms：本体在演 <c>ux_attack</c>、背景还是正常战场）。
	/// 这段时间黑幕不上，本体也不能藏，否则就是一段黑屏。
	/// 时刻由舞台从 <c>bg.fade_in_at_ms</c> 推出，经 <c>takeover_reached</c> 下达。</para>
	/// </summary>
	private static void HideBody(Phase phase)
	{
		if (phase.BodyHidden || phase.ActorsReleased)
		{
			return;
		}

		try
		{
			if (GodotObject.IsInstanceValid(phase.Body))
			{
				phase.Body!.Visible = false;
			}

			phase.BodyHidden = true;
		}
		catch (Exception ex)
		{
			Warn("隐藏本体失败：" + ex.Message);
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
		// 本体已归还，晚到的 takeover_reached 不该再把它藏起来。
		phase.BodyHidden = true;
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

		// 取消 / 打断：演出音效立即停，避免画面收了声音还在响。
		// 自然播完（finished / resources_finished）不停 —— 尾部放完是设计行为。
		if (cancelled)
		{
			HeidemarieCombatAudio.StopSfx();
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

		// 组库必须一起预热：时间轴已瘦身为「组键 + 窗口」，
		// 图层构成（含骨架名）都在组库里，缺它整条路线搭不起来。
		if (paths.IndexOf(GroupLibraryPath) < 0)
		{
			paths.Add(GroupLibraryPath);
		}

		using JsonDocument document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(jsonPath));
		if (!document.RootElement.TryGetProperty("layers", out JsonElement layers))
		{
			return;
		}

		foreach (JsonElement layer in layers.EnumerateArray())
		{
			// 瘦身版每项仍带 name（一个骨架一项），所以这里两代都收；
			// 若哪天改成「一组一项」，group 分支负责从组库展开骨架名。
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
