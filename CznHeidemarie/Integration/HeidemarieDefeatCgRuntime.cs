using System;
using System.Collections.Generic;
using CznCore.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「败北 CG」设置项的接入层：本地海德玛丽阵亡时，在整屏叠一张 CG。
///
/// <para>参考 mod（Nine / Tiphera）走的是 CznCore 的 <c>DefeatOverlayRuntime</c>，
/// 但那套运行时的构造要求一个完整的 <c>CharacterSkinProfile</c>
/// （二十来个字段：战斗骨架、形态特效、卡牌演出…），本工程没有走
/// <c>CharacterSkinModBootstrap</c> 那条大路，为了一个 CG 去伪造整份 profile 不值当，
/// 所以这里自己实现一个等价的轻量叠层：<b>行为对齐</b>（层号 / 模式 / 开关 / 淡入淡出 /
/// <b>自带寿命、自管清理</b>），不引入 profile 依赖。</para>
///
/// <para>CG 素材：<c>CNZ_Source/czn30093/cutin/</c> 下的两张 1580x720 图
/// （<c>collapse_fhd/collapse_30093.webp</c> 与 <c>fhd/30093.webp</c>，
/// 原图是动画 webp，已取末帧重编码为静态 webp）。</para>
///
/// <para><b>为什么不靠「结算界面打开」来收尾</b>：阵亡后游戏会紧接着打开败北结算界面
/// （<c>NGameOverScreen</c>），而它的背景板本身就盖满全屏；如果 CG 等到
/// <c>RunFinishedPresentationEvent</c> 就自行清理，会出现「CG 刚建好就被自己拆掉」的竞态，
/// 观感上就是完全没有败北 CG。因此改为与参考实现同构的<b>定时演出</b>：
/// 叠层按固定节奏淡入 → 停留 → 淡出 → 自释放，期间一直盖在结算界面之上。</para>
/// </summary>
internal static class HeidemarieDefeatCgRuntime
{
	/// <summary>CG 资源目录。</summary>
	private const string CgRoot = "res://CznHeidemarie/images/defeat/";

	/// <summary>叠层节点名（同名的旧节点先清掉，避免叠两张）。</summary>
	private const string LayerName = "HeidemarieDefeatCgLayer";

	/// <summary>
	/// 叠层层号。必须高于败北结算界面（<c>NGameOverScreen</c>，含其全屏背景板），
	/// 否则 CG 会被结算界面盖住而等于没播。取值与 CznCore 的
	/// <c>DefeatOverlayRuntime</c> 一致。
	/// </summary>
	private const int LayerIndex = 1000;

	/// <summary>起播延迟：留一点时间让倒地的起手动作露出来，之后 CG 再压上来。</summary>
	private const float StartDelaySeconds = 0.35f;

	/// <summary>淡入秒数。</summary>
	private const float FadeInSeconds = 0.6f;

	/// <summary>淡入后停留秒数。</summary>
	private const float HoldSeconds = 2.0f;

	/// <summary>淡出秒数；淡出走完即释放节点。</summary>
	private const float FadeOutSeconds = 0.35f;

	private static readonly List<IDisposable> Subscriptions = new();

	private static CanvasLayer? _layer;

	/// <summary>
	/// 订阅阵亡与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG", (PlayerDeathResolvedEvent e) => Queue(e)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 入房", (RoomEnteringEvent _) => Cleanup()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 胜利", (PlayerCombatVictoryEvent _) => Cleanup()));
	}

	/// <summary>立刻清掉叠层（换房 / 胜利 / 设置关闭时调用）。</summary>
	internal static void Cleanup()
	{
		if (GodotObject.IsInstanceValid(_layer))
		{
			_layer!.QueueFree();
		}

		_layer = null;
	}

	/// <summary>阵亡后按设置决定要不要播 CG。</summary>
	private static void Queue(PlayerDeathResolvedEvent e)
	{
		if (!HeidemariePresentationHost.IsLocalHeidemarie(e.Player))
		{
			return;
		}

		string? path = ResolveTexturePath();
		if (path is null)
		{
			return;
		}

		Build(path);
	}

	/// <summary>
	/// 按设置解析用哪张 CG；返回 <c>null</c> 表示这次不显示。
	/// </summary>
	private static string? ResolveTexturePath()
	{
		// 能力声明（有素材且做了）→ 角色开关 → 模式，三者都过才显示
		if (!HeidemariePresentationHost.Features.DeathCgOverlay
			|| !HeidemarieSettings.CurrentCharacter.ShowDefeatCgOverlay)
		{
			return null;
		}

		return HeidemarieSettings.CurrentCharacter.DefeatCgOverlayMode switch
		{
			DefeatCgOverlayMode.Off => null,
			DefeatCgOverlayMode.Type1 => CgRoot + "heidemarie_defeat_cg_1.webp",
			DefeatCgOverlayMode.Type2 => CgRoot + "heidemarie_defeat_cg_2.webp",
			// 随机：两张各 50%
			_ => CgRoot + (Random.Shared.Next(2) == 0
				? "heidemarie_defeat_cg_1.webp"
				: "heidemarie_defeat_cg_2.webp"),
		};
	}

	/// <summary>
	/// 真正把叠层建出来。整段演出（延迟 → 淡入 → 停留 → 淡出 → 释放）全交给一条
	/// 绑在节点上的 Tween，所以不需要额外的定时器与「延迟中又被清理」的代际判定。
	/// </summary>
	private static void Build(string texturePath)
	{
		try
		{
			if (GodotObject.IsInstanceValid(_layer))
			{
				// 已经有一张在播（同一局里的重复阵亡事件）：不叠第二张
				return;
			}

			Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath, null, ResourceLoader.CacheMode.Reuse);
			if (texture is null)
			{
				HeidemarieIntegration.Log.Warn("败北 CG 加载失败：" + texturePath);
				return;
			}

			NCombatRoom? room = NCombatRoom.Instance;
			SceneTree? tree = room?.GetTree() ?? (Engine.GetMainLoop() as SceneTree);
			if (tree?.Root is not { } root)
			{
				return;
			}

			// 不挡输入：CG 只是观感层，死亡结算与结算界面照常
			CanvasLayer layer = new()
			{
				Name = LayerName,
				Layer = LayerIndex,
			};

			Control host = new()
			{
				Name = "CgHost",
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			host.SetAnchorsPreset(Control.LayoutPreset.FullRect);

			TextureRect rect = new()
			{
				Name = "Cg",
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// 从全透明淡入，避免瞬间闪一张大图
				Modulate = new Color(1f, 1f, 1f, 0f),
			};
			rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

			host.AddChild(rect, forceReadableName: false, Node.InternalMode.Disabled);
			layer.AddChild(host, forceReadableName: false, Node.InternalMode.Disabled);
			root.AddChild(layer, forceReadableName: false, Node.InternalMode.Disabled);
			_layer = layer;

			CanvasLayer captured = layer;
			Tween tween = rect.CreateTween();
			tween.TweenInterval(StartDelaySeconds);
			tween.TweenProperty(rect, "modulate:a", 1.0f, FadeInSeconds);
			tween.TweenInterval(HoldSeconds);
			tween.TweenProperty(rect, "modulate:a", 0.0f, FadeOutSeconds);
			tween.TweenCallback(Callable.From(() => Release(captured)));

			HeidemarieIntegration.Log.Info("败北 CG 已显示：" + texturePath);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 显示失败：" + ex.Message);
		}
	}

	/// <summary>演出走完，释放叠层（只有还是当前那一张时才动 <see cref="_layer"/>）。</summary>
	private static void Release(CanvasLayer layer)
	{
		if (GodotObject.IsInstanceValid(layer))
		{
			layer.QueueFree();
		}

		if (ReferenceEquals(_layer, layer))
		{
			_layer = null;
		}
	}
}
