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
/// 所以这里自己实现一个等价的轻量叠层：<b>行为对齐</b>（模式 / 开关 / 淡入 / 清理时机），
/// 不引入 profile 依赖。</para>
///
/// <para>CG 素材：<c>CNZ_Source/czn30093/cutin/</c> 下的两张 1580x720 图
/// （<c>collapse_fhd/collapse_30093.webp</c> 与 <c>fhd/30093.webp</c>，
/// 原图是动画 webp，已取末帧重编码为静态 webp）。</para>
/// </summary>
internal static class HeidemarieDefeatCgRuntime
{
	/// <summary>CG 资源目录。</summary>
	private const string CgRoot = "res://CznHeidemarie/images/defeat/";

	/// <summary>叠层节点名（同名的旧节点先清掉，避免叠两张）。</summary>
	private const string LayerName = "HeidemarieDefeatCgLayer";

	/// <summary>淡入秒数。</summary>
	private const float FadeInSeconds = 0.6f;

	private static readonly List<IDisposable> Subscriptions = new();

	private static CanvasLayer? _layer;

	/// <summary>待播的延迟句柄计数，用于「延迟中又被清理」的判定。</summary>
	private static int _generation;

	/// <summary>
	/// 订阅阵亡与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG", (PlayerDeathResolvedEvent e) => Queue(e)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 入房", (RoomEnteringEvent _) => Cleanup()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 胜利", (PlayerCombatVictoryEvent _) => Cleanup()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 收局", (RunFinishedPresentationEvent _) => Cleanup()));
	}

	/// <summary>清掉叠层（换房 / 胜利 / 收局 / 设置关闭时调用）。</summary>
	internal static void Cleanup()
	{
		_generation++;

		if (GodotObject.IsInstanceValid(_layer))
		{
			_layer!.QueueFree();
		}

		_layer = null;
	}

	/// <summary>阵亡后等死亡动画播完再淡入 CG（立即叠上会盖住死亡演出）。</summary>
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

		// Creature 是数据模型不是 Godot 节点，取树只能走主循环
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}

		int generation = ++_generation;

		if (e.DeathAnimationLength <= 0f)
		{
			Build(path, generation);
			return;
		}

		SceneTreeTimer timer = tree.CreateTimer(e.DeathAnimationLength);
		timer.Timeout += () =>
		{
			if (generation == _generation)
			{
				Build(path, generation);
			}
		};
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

	/// <summary>真正把叠层建出来。</summary>
	private static void Build(string texturePath, int generation)
	{
		try
		{
			if (generation != _generation || GodotObject.IsInstanceValid(_layer))
			{
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
				Layer = 100,
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

			Tween tween = rect.CreateTween();
			tween.TweenProperty(rect, "modulate:a", 1.0f, FadeInSeconds);

			HeidemarieIntegration.Log.Info("败北 CG 已显示：" + texturePath);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 显示失败：" + ex.Message);
		}
	}
}
