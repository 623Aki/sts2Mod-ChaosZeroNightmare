using System;
using System.Collections.Generic;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznHeidemarie.Integration;

/// <summary>
/// 出牌特写立绘的接入层。
///
/// <para>对应参考 mod 的 <c>CznNineCharacter.Presentation.NineBattleReadyRuntime</c>。
/// 架构完全一致：实现一个 <see cref="IBattleReadyOverlayRuntimeContext{TCard}"/> 描述
/// 「什么牌该出特写、特写用哪套骨架、卡片路由到哪个动画」，
/// 再把它交给 CznCore 的 <see cref="BattleReadyOverlayRuntime{TCard}"/>，
/// 由后者负责创建节点、滑入滑出、坐标标定与生命周期。</para>
///
/// <para>事件源来自 CznCore 的 <see cref="SkinEventHub"/> —— 它由
/// <c>SkinCardTransportPatches</c> 等 Harmony 补丁从游戏原生回调里转发出来，
/// 本类只做订阅，不直接碰游戏内部。</para>
///
/// <para>相对参考 mod 做的减法：去掉了过场动画联动（NineCinematicRuntime）、
/// 观众可见性策略（CombatAudience）与「打出的牌从手牌消失」的额外包装
/// —— 那些对应本项目没有的功能。</para>
/// </summary>
internal static class HeidemarieBattleReadyRuntime
{
	/// <summary>把游戏侧事件翻译成 overlay 需要的信息。</summary>
	private sealed class Context : IBattleReadyOverlayRuntimeContext<CardModel>
	{
		/// <inheritdoc/>
		/// <remarks>
		/// 第一个委托给出当前模式，第二个委托判断「这张牌该不该出特写」：
		/// 必须是本地玩家打出的 Heidemarie 的牌，且战斗还在进行中。
		/// </remarks>
		public LeftSideCardPresentationPolicy<CardModel> PresentationPolicy { get; } = new LeftSideCardPresentationPolicy<CardModel>(
			() => HeidemarieSettings.CurrentCharacter.BattleReadyOverlayMode,
			(CardModel card) => IsLocalHeidemarie(card.Owner)
				&& !card.Owner.Creature.IsDead
				&& !CombatManager.Instance.IsOverOrEnding,
			(CardModel _) => false);

		public float BattleReadyScale => HeidemarieSettings.CurrentCharacter.BattleReadyScale;

		public float BattleReadyOpacity => HeidemarieSettings.CurrentCharacter.BattleReadyOpacity;

		public float BattleReadyOffsetX => HeidemarieSettings.CurrentCharacter.BattleReadyOffsetX;

		public float BattleReadyOffsetY => HeidemarieSettings.CurrentCharacter.BattleReadyOffsetY;

		public ISkinTimingProbe TimingProbe => Diagnostics;

		/// <inheritdoc/>
		public bool IsTargetCard(CardModel? card)
		{
			return IsLocalHeidemarie(card?.Owner);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// 攻击牌走，技能 / 能力牌走 card_casting；
		/// 「解放极光」在特写骨架里有专属动画 <c>ug_attack</c>，单独走一条路线。
		/// </remarks>
		public CombatPresentationCardRoute ResolveRoute(CardModel card)
		{
			if (card is HeidemarieLiberatedAurora)
			{
				return CombatPresentationCardRoute.Ultimate;
			}

			return card.Type == CardType.Attack
				? CombatPresentationCardRoute.Attack
				: CombatPresentationCardRoute.Skill;
		}

		/// <inheritdoc/>
		public Node? GetOverlayParent()
		{
			return GetScreen();
		}

		// ── 特写立绘的待机特效层（剑影环绕）──

		/// <inheritdoc/>
		/// <remarks>
		/// 从 <c>heidemarie_battle_ready_effects.tscn</c> 里把 <c>Back</c> / <c>Front</c>
		/// 两个图层子树摘出来交给运行时，由它分别挂到立绘骨架的后面 / 前面。
		/// 摘出来的节点自身变换会被运行时重置，所以摆放只靠子节点的 z_index。
		/// </remarks>
		public Node2D? DetachIdleEffectLayer(Node root, string childName)
		{
			if (!GodotObject.IsInstanceValid(root))
			{
				return null;
			}

			foreach (Node child in root.GetChildren())
			{
				if (child is not Node2D layer || layer.Name != childName)
				{
					continue;
				}

				root.RemoveChild(layer);
				return layer;
			}

			return null;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// 这几层骨架一律使用普通 alpha 渲染，没有把贴图改成预乘 alpha 的素材，
		/// 因此不需要替换材质 —— 与参考 mod 的同类实现保持一致（都是空实现）。
		/// </remarks>
		public void ApplyIdleEffectMaterial(Node root)
		{
		}

		/// <inheritdoc/>
		public void ApplyIdleEffectMaterialDeferred(Node root, int frames)
		{
		}

		/// <inheritdoc/>
		/// <remarks>
		/// 每次立绘重新显示时调用，让剑影循环重播一次（否则会停在上次播完的那一帧）。
		/// </remarks>
		public void StartIdleEffects(Node root)
		{
			if (!GodotObject.IsInstanceValid(root))
			{
				return;
			}

			StartSpineLoops(root);

			static void StartSpineLoops(Node node)
			{
				foreach (Node child in node.GetChildren())
				{
					if (child.GetClass() == "SpineSprite")
					{
						StartLoop(child);
					}

					StartSpineLoops(child);
				}
			}

			static void StartLoop(Node spine)
			{
				try
				{
					MegaSprite sprite = new(spine);
					if (sprite.IsAnimationStateReady() && sprite.HasAnimation(IdleEffectAnimation))
					{
						sprite.TryGetAnimationState()?.SetAnimation(IdleEffectAnimation, loop: true, trackId: 0);
					}
				}
				catch (Exception ex)
				{
					// 骨架尚未就绪时查询会抛；等下一轮显示再试即可，不必打断立绘
					HeidemarieIntegration.Log.Warn("特写剑影启动失败: " + ex.Message);
				}
			}
		}

		public void LogInfo(string message)
		{
			HeidemarieIntegration.Log.Info("出牌特写: " + message);
		}

		public void LogWarn(string message)
		{
			HeidemarieIntegration.Log.Warn("出牌特写: " + message);
		}
	}

	/// <summary>滑入 / 滑出的动作参数，沿用参考 mod 使用的同一套适配器。</summary>
	private static readonly BattleReadyOverlayMotionAdapter Motion =
		BattleReadyOverlayMotionAdapters.CznSts2FallbackSlide();

	/// <summary>剑影层的循环动画名（animations/effects 里的骨架统一只有 animation / 数字编号）。</summary>
	private const string IdleEffectAnimation = "animation";

	/// <summary>特写立绘的完整配置。</summary>
	private static readonly BattleReadyOverlayProfile Profile = new BattleReadyOverlayProfile(
		RuntimeNodeNamePrefix: "HeidemarieBattleReady",
		ScenePath: "res://scenes/heidemarie_battle_ready.tscn",
		SkeletonDataPath: "res://CznHeidemarie/animations/battle_ready/30093_battle_ready_skel_data.tres",
		// 30093_battle_ready.skel 实际有 4 个动画：
		//   b_idle(3.3333) / card_attack(0.4667) / card_casting(0.4667) / ug_attack(0.8)
		// 因此终结技可以走专属的 ug_attack，其余攻击走 card_attack、技能走 card_casting。
		Animations: new BattleReadyOverlayAnimations(
			In: string.Empty,
			Idle: "b_idle",
			Out: string.Empty,
			Attack: "card_attack",
			Casting: "card_casting"),
		OutDelaySeconds: Motion.OutDelaySeconds,
		CancelOutDelaySeconds: Motion.CancelOutDelaySeconds,
		SlideOffsetX: Motion.SlideOffsetX,
		SlideDurationSeconds: Motion.SlideDurationSeconds,
		// 待机特效层：立绘前后各一圈「剑影」（见 scenes/heidemarie_battle_ready_effects.tscn）。
		// 位置与缩放都交给 0,0 / 1.0 —— 这几层骨架与立绘骨架共用同一套坐标单位，
		// 运行时又会把它挂到立绘骨架节点下，所以原点对齐即天然贴合。
		IdleEffect: BattleReadyOverlayIdleEffectPlan.Scene(
			"res://scenes/heidemarie_battle_ready_effects.tscn",
			rootPositionX: 0f,
			rootPositionY: 0f,
			rootScale: 1f),
		CardAnimations: new Dictionary<CombatPresentationCardRoute, string>
		{
			[CombatPresentationCardRoute.Attack] = "card_attack",
			[CombatPresentationCardRoute.Skill] = "card_casting",
			[CombatPresentationCardRoute.Ultimate] = "ug_attack"
		},
		CardUseOutDelaySeconds: -1f,
		CardUseSlideDurationSeconds: Motion.CardUseSlideDurationSeconds);

	private static readonly SkinDiagnosticsRuntime Diagnostics = new SkinDiagnosticsRuntime(() => true, 16.7);

	private static readonly Lazy<BattleReadyOverlayRuntime<CardModel>> Overlay =
		new Lazy<BattleReadyOverlayRuntime<CardModel>>(() => new BattleReadyOverlayRuntime<CardModel>(Profile, new Context()));

	/// <summary>事件订阅句柄，清理时统一释放。</summary>
	private static readonly List<IDisposable> Subscriptions = new List<IDisposable>();

	private static Node2D? _screen;

	/// <summary>
	/// 订阅游戏事件。必须在战斗开始之前调用（由 <see cref="HeidemarieIntegration.Init"/> 调）。
	/// </summary>
	internal static void Initialize()
	{
		// 进战斗时预加载，避免第一次打牌才现加载导致闪一下
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写预加载", (CombatStartingEvent e) =>
		{
			if (e.CombatState == null)
			{
				return;
			}

			foreach (Player player in e.CombatState.Players)
			{
				if (!IsLocalHeidemarie(player))
				{
					continue;
				}

				try
				{
					Overlay.Value.Preload();
				}
				catch (Exception ex)
				{
					HeidemarieIntegration.Log.Warn("特写预加载失败: " + ex.Message);
				}

				break;
			}
		}));

		// 手牌聚焦 / 失焦
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写聚焦", (HandCardFocusedEvent e) => Show(e.Holder.CardModel)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写失焦", (HandCardUnfocusedEvent e) => Hide(e.Holder.CardModel)));

		// 悬停
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写悬停", (HandCardHoverEffectsEvent e) =>
		{
			if (e.IsHovered)
			{
				Show(e.Holder.CardModel);
			}
			else
			{
				Hide(e.Holder.CardModel);
			}
		}));

		// 鼠标按下 / 拖拽开始 / 指向目标 / 多目标选择：都算「正在考虑出这张牌」
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写按下", (HandCardMousePressedEvent e) => Show(e.Holder.CardModel)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写拖拽", (MouseCardPlayStartedEvent e) => Show(e.Node.Holder?.CardModel)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写指向", (CardSelectionPresentationStartedEvent e) => Show(e.Card)));

		// 取消
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写取消", (CardPlayCancelledEvent e) =>
		{
			if (Overlay.IsValueCreated)
			{
				Overlay.Value.CancelForCard(e.Node.Holder?.CardModel);
			}
		}));

		// 真正打出去：播对应动作
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写出牌", (CardPlayCommittedEvent e) =>
		{
			if (Overlay.Value.CanShow(e.Card))
			{
				Overlay.Value.PlayForCard(e.Card);
			}
		}));

		// 生命周期清理
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写进房间", (RoomEnteringEvent _) => Cleanup(releaseResources: true)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写阵亡", (PlayerDeathResolvedEvent e) => CleanupFor(e.Player)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 特写胜利", (PlayerCombatVictoryEvent e) => CleanupFor(e.Player)));
	}

	/// <summary>设置里改了缩放 / 透明度 / 偏移之后重新套用。</summary>
	internal static void ApplyTransform()
	{
		if (Overlay.IsValueCreated)
		{
			Overlay.Value.ApplyTransformFromSettings();
		}
	}

	/// <summary>设置里关掉「出牌特写」时调用。</summary>
	internal static void ApplyCardPlayPresentation(bool enabled)
	{
		if (!enabled)
		{
			Cleanup(releaseResources: false);
		}
	}

	private static void Show(CardModel? card)
	{
		if (Overlay.Value.CanShow(card))
		{
			Overlay.Value.ShowIdleForCard(card);
		}
	}

	private static void Hide(CardModel? card)
	{
		if (Overlay.IsValueCreated)
		{
			Overlay.Value.HideForCard(card);
		}
	}

	private static void CleanupFor(Player? player)
	{
		if (IsLocalHeidemarie(player))
		{
			Cleanup(releaseResources: true);
		}
	}

	/// <summary>清理特写节点；<paramref name="releaseResources"/> 为真时连容器一起释放。</summary>
	internal static void Cleanup(bool releaseResources = false)
	{
		if (Overlay.IsValueCreated)
		{
			Overlay.Value.Cleanup(releaseResources: false);
			if (releaseResources)
			{
				Overlay.Value.Cleanup();
			}
		}

		if (GodotObject.IsInstanceValid(_screen))
		{
			_screen.Hide();
			if (releaseResources)
			{
				_screen.QueueFree();
			}
		}

		if (releaseResources)
		{
			_screen = null;
		}
	}

	/// <summary>
	/// 是不是本地玩家操作的 Heidemarie 的牌。
	/// </summary>
	private static bool IsLocalHeidemarie(Player? player)
	{
		if (player?.Character is not Heidemarie)
		{
			return false;
		}

		return LocalContext.IsMe(player);
	}

	/// <summary>
	/// 取得特写立绘的挂载容器。
	///
	/// <para>把容器按 1920x1080 设计坐标等比映射到实际窗口 ——
	/// 场景里的 position/scale 都是按设计分辨率标的，
	/// 不做映射的话换个分辨率位置就跑了。</para>
	/// </summary>
	private static Node? GetScreen()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (!GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
		{
			return null;
		}

		Control parent = room.CombatVfxContainer;

		if (!GodotObject.IsInstanceValid(_screen))
		{
			_screen = new Node2D
			{
				Name = "HeidemarieBattleReadyScreen",
				Visible = false
			};
			parent.AddChild(_screen, forceReadableName: false, Node.InternalMode.Disabled);

			Node2D screen = _screen;
			Viewport viewport = room.GetViewport();
			viewport.SizeChanged += Layout;
			room.TreeExiting += OnRoomExit;
			screen.TreeExiting += () =>
			{
				if (GodotObject.IsInstanceValid(viewport))
				{
					viewport.SizeChanged -= Layout;
				}

				if (GodotObject.IsInstanceValid(room))
				{
					room.TreeExiting -= OnRoomExit;
				}
			};
			Layout();
			void Layout()
			{
				Vector2 size = viewport.GetVisibleRect().Size;
				float uniform = Math.Min(size.X / 1920f, size.Y / 1080f);
				Transform2D design = new Transform2D(
					Vector2.Right * uniform,
					Vector2.Down * uniform,
					new Vector2(0f, size.Y - 1080f * uniform));
				screen.Transform = parent.GetGlobalTransformWithCanvas().AffineInverse() * design;
			}
			static void OnRoomExit()
			{
				Cleanup(releaseResources: true);
			}
		}

		_screen.Show();
		return _screen;
	}
}
