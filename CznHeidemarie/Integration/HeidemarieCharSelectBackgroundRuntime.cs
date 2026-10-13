using System;
using System.Collections.Generic;
using CznCore.Godot;
using CznCore.Presentation;
using CznHeidemarie.Characters.Heidemarie;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace CznHeidemarie.Integration;

/// <summary>
/// 选人界面背景的三种变体切换。
///
/// <para>参考 mod（Tiphera / Nine）的做法是：宿主场景挂一个 GDScript，脚本按设置里的
/// 背景键 <c>load</c> 对应变体场景塞进 <c>VariantHost</c>。本项目是纯 C# 工程，
/// 因此改成用 CznCore 已经转发出来的选人事件（<see cref="CharacterSelectedEvent"/>）在
/// C# 侧做同样的事，不引入 GDScript。</para>
///
/// <para>三种变体与设置下拉里的分类一一对应：</para>
/// <list type="bullet">
///   <item><b>立绘</b>（StaticPortrait）—— 单张静态背景图；</item>
///   <item><b>动态立绘</b>（TeamJoinPortrait）—— 静态背景 + 本体骨架播 idle；</item>
///   <item><b>角色大厅</b>（LiveLobby）—— 由 CNZ 官方 30093.le 的 depth_infos
///   组装出的 7 层 liveevent 骨架，全部循环播 <c>lobby</c>。</item>
/// </list>
///
/// <para>切换时机：每次选角色（含随机角色结算）都会重新按当前设置套用一次；
/// 在设置里改完「角色背景」后，重新选择一次角色即可看到效果。</para>
/// </summary>
internal static class HeidemarieCharSelectBackgroundRuntime
{
	/// <summary>游戏放角色背景的容器节点名（<c>NCharacterSelectScreen._bgContainer</c>）。</summary>
	private const string BackgroundContainerNodeName = "AnimatedBg";

	/// <summary>宿主场景里预留给变体场景的容器节点名。</summary>
	private const string VariantHostNodeName = "VariantHost";

	/// <summary>记在 VariantHost 上的「当前已加载哪个变体」标记，避免重复加载。</summary>
	private const string LoadedVariantMeta = "czn_heidemarie_char_select_variant";

	/// <summary>记在单个 liveevent 层上的「循环动画已启动」标记。</summary>
	private const string LoopStartedMeta = "czn_heidemarie_lobby_loop_started";

	private const string StaticPortraitScenePath = "res://scenes/screens/char_select/heidemarie_static_portrait_bg.tscn";

	private const string DynamicPortraitScenePath = "res://scenes/screens/char_select/heidemarie_dynamic_portrait_bg.tscn";

	private const string LiveLobbyScenePath = "res://scenes/screens/char_select/heidemarie_live_lobby_bg.tscn";

	/// <summary>liveevent 各层共用的设计视口尺寸（取自官方 30084/30093 的 le 布局约束）。</summary>
	private const float LiveLobbyViewportWidth = 1580f;

	private const float LiveLobbyViewportHeight = 720f;

	/// <summary>liveevent 各层统一播放的循环动画名。</summary>
	private const string LiveLobbyLoopAnimation = "lobby";

	/// <summary>
	/// 立绘类变体的设计坐标系尺寸。两个立绘场景里 SpineSprite 的 position/scale
	/// 都按 1920x1080 标注，运行时再按实际视口等比映射。
	/// </summary>
	private const float DesignWidth = 1920f;

	private const float DesignHeight = 1080f;

	/// <summary>节点上缓存「作者标定的设计坐标变换」的 meta 键。</summary>
	private const string DesignPositionMeta = "czn_heidemarie_design_position";

	private const string DesignScaleMeta = "czn_heidemarie_design_scale";

	/// <summary>节点上标记「皮肤与动画已套用完成」的 meta 键。</summary>
	private const string RuntimeAppliedMeta = "czn_heidemarie_portrait_runtime_applied";

	/// <summary>场景里声明「该立绘要用哪套皮肤」的 meta 键。</summary>
	private const string SkinMeta = "czn_portrait_skin";

	/// <summary>场景里声明「该立绘播哪些动画」的 meta 键（逗号分隔，末段循环）。</summary>
	private const string AnimationMeta = "czn_portrait_animation";

	/// <summary>运行时套皮肤/动画的就绪重试次数与间隔（骨架数据异步加载）。</summary>
	private const int RuntimeApplyMaxAttempts = 60;

	private const float RuntimeApplyRetrySeconds = 0.1f;

	/// <summary>当前立绘变体的根节点，供视口尺寸变化时重排。</summary>
	private static Node? _portraitRoot;

	/// <summary>循环动画的就绪重试次数与间隔（骨架加载时机不确定）。</summary>
	private const int LoopStartMaxAttempts = 60;

	private const float LoopStartRetrySeconds = 0.1f;

	/// <summary>事件订阅句柄，保持引用以防被回收。</summary>
	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>
	/// 订阅选人事件。由 <see cref="HeidemarieIntegration.Init"/> 在补丁应用后调用。
	/// </summary>
	internal static void Initialize()
	{
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 选人背景", (CharacterSelectedEvent e) =>
			Apply(e.Screen, e.Character)));

		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 随机角色背景", (RandomCharacterResolvedEvent e) =>
			Apply(e.Screen, e.Character)));
	}

	/// <summary>
	/// 供选人界面的设置抽屉在改完「角色背景」之后立即刷新背景。
	/// </summary>
	/// <param name="screen">选人界面。</param>
	/// <param name="characterModel">当前选中的角色。</param>
	internal static void Refresh(NCharacterSelectScreen screen, CharacterModel? characterModel)
	{
		Apply(screen, characterModel);
	}

	/// <summary>
	/// 按当前设置把选中角色的背景换成对应变体。非本角色一律放手，交回游戏原生流程。
	/// </summary>
	private static void Apply(NCharacterSelectScreen screen, CharacterModel? characterModel)
	{
		if (characterModel is not Heidemarie || !GodotObject.IsInstanceValid(screen))
		{
			return;
		}

		Control? variantHost = FindVariantHost(screen);
		if (variantHost is null)
		{
			// 背景场景还没挂上来（或不是本 mod 的宿主场景），本次跳过
			return;
		}

		string scenePath = ResolveScenePath();
		if (variantHost.HasMeta(LoadedVariantMeta)
			&& variantHost.GetMeta(LoadedVariantMeta).AsString() == scenePath)
		{
			return;
		}

		// 清掉上一个变体（宿主场景自带的静态立绘也在这里被换掉）
		foreach (Node child in variantHost.GetChildren())
		{
			variantHost.RemoveChild(child);
			child.QueueFree();
		}

		variantHost.SetMeta(LoadedVariantMeta, scenePath);

		if (ResourceLoader.Load<PackedScene>(scenePath) is not { } packed)
		{
			HeidemarieIntegration.Log.Warn("选人背景场景缺失：" + scenePath);
			return;
		}

		Node instance = packed.Instantiate();
		variantHost.AddChild(instance, false, Node.InternalMode.Disabled);

		if (scenePath == LiveLobbyScenePath)
		{
			// 大厅是多层骨架拼装，需要按视口重新标定并启动循环动画
			LayoutLiveLobby(instance);
			StartLiveLobbyLoops(instance);
		}
		else
		{
			// 立绘 / 动态立绘：设计坐标映射 + 运行时套皮肤与动画
			PreparePortraitVariant(instance);
		}
	}

	/// <summary>
	/// 立绘类变体（立绘 / 动态立绘）的接入。
	///
	/// <para>两个场景里 SpineSprite 的 position/scale 都按 1920x1080 设计坐标标注，
	/// 这里先把它们缓存成 meta，再按实际视口做等比映射。</para>
	///
	/// <para><b>皮肤与动画必须运行时套</b>：.tscn 里的 <c>preview_skin</c> /
	/// <c>preview_animation</c> 只对编辑器预览生效 —— 不套的话官方立绘会退化成
	/// default 皮肤、并且停在第一帧。参考 mod 的同名脚本也是这么做的。</para>
	/// </summary>
	private static void PreparePortraitVariant(Node root)
	{
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		foreach (Node child in root.GetChildren())
		{
			if (child is not Node2D layer || child.GetClass() != "SpineSprite")
			{
				continue;
			}

			if (!layer.HasMeta(DesignPositionMeta))
			{
				layer.SetMeta(DesignPositionMeta, Variant.From(layer.Position));
				layer.SetMeta(DesignScaleMeta, Variant.From(layer.Scale));
			}
		}

		_portraitRoot = root;
		LayoutPortraitVariant(root);
		ApplyPortraitRuntime(root, 0);

		if (root is CanvasItem canvas)
		{
			Viewport? viewport = canvas.GetViewport();
			if (viewport is not null)
			{
				viewport.SizeChanged += OnViewportSizeChanged;
				canvas.TreeExiting += () => viewport.SizeChanged -= OnViewportSizeChanged;
			}
		}
	}

	private static void OnViewportSizeChanged()
	{
		if (_portraitRoot is not null && GodotObject.IsInstanceValid(_portraitRoot))
		{
			LayoutPortraitVariant(_portraitRoot);
		}
	}

	/// <summary>
	/// 把设计坐标 (1920x1080) 上的立绘节点按 cover 比例映射到实际视口，
	/// 视口中心与设计中心对齐 —— 与参考 mod 的立绘布局算法一致。
	/// </summary>
	private static void LayoutPortraitVariant(Node root)
	{
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		Vector2 target = root is Control control && control.Size.X > 1f && control.Size.Y > 1f
			? control.Size
			: Vector2.Zero;

		if (target.X <= 1f || target.Y <= 1f)
		{
			target = (root as CanvasItem)?.GetViewportRect().Size ?? Vector2.Zero;
		}

		if (target.X <= 1f || target.Y <= 1f)
		{
			target = new Vector2(DesignWidth, DesignHeight);
		}

		float ratio = Mathf.Max(target.X / DesignWidth, target.Y / DesignHeight);
		Vector2 targetCenter = target * 0.5f;
		Vector2 designCenter = new(DesignWidth * 0.5f, DesignHeight * 0.5f);

		foreach (Node child in root.GetChildren())
		{
			if (child is not Node2D layer || child.GetClass() != "SpineSprite")
			{
				continue;
			}

			Vector2 designPosition = layer.HasMeta(DesignPositionMeta)
				? layer.GetMeta(DesignPositionMeta).AsVector2()
				: layer.Position;

			Vector2 designScale = layer.HasMeta(DesignScaleMeta)
				? layer.GetMeta(DesignScaleMeta).AsVector2()
				: layer.Scale;

			layer.Position = targetCenter + (designPosition - designCenter) * ratio;
			layer.Scale = designScale * ratio;
		}
	}

	/// <summary>
	/// 运行时套皮肤与动画。骨架数据是异步加载的，第一次调用时可能还没就绪，
	/// 因此按固定间隔重试；每个节点用 meta 标记完成后跳过，
	/// 避免反复 <c>SetAnimation</c> 把动画重置回开头。
	/// </summary>
	private static void ApplyPortraitRuntime(Node root, int attempt)
	{
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		int pending = 0;

		foreach (Node child in root.GetChildren())
		{
			if (child is not Node2D layer || child.GetClass() != "SpineSprite" || layer.HasMeta(RuntimeAppliedMeta))
			{
				continue;
			}

			MegaSprite sprite = new(layer);
			if (!sprite.IsAnimationStateReady())
			{
				pending++;
				continue;
			}

			ApplySkin(sprite, layer);
			ApplyAnimations(sprite, layer);
			layer.SetMeta(RuntimeAppliedMeta, Variant.From(true));
		}

		if (pending > 0 && attempt < RuntimeApplyMaxAttempts)
		{
			GodotScheduler.Delay(RuntimeApplyRetrySeconds, () => ApplyPortraitRuntime(root, attempt + 1));
		}
	}

	private static void ApplySkin(MegaSprite sprite, Node layer)
	{
		if (!layer.HasMeta(SkinMeta))
		{
			return;
		}

		string skin = layer.GetMeta(SkinMeta).AsString();
		if (string.IsNullOrWhiteSpace(skin))
		{
			return;
		}

		try
		{
			MegaSkeleton? skeleton = sprite.GetSkeleton();
			if (skeleton is null)
			{
				return;
			}

			skeleton.SetSkinByName(skin);
			// 换皮肤后把插槽恢复成 setup pose，否则会残留旧皮肤的附件
			skeleton.SetSlotsToSetupPose();
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn($"立绘皮肤设置失败（{skin}）：{ex.Message}");
		}
	}

	private static void ApplyAnimations(MegaSprite sprite, Node layer)
	{
		string spec = layer.HasMeta(AnimationMeta) ? layer.GetMeta(AnimationMeta).AsString() : string.Empty;
		if (string.IsNullOrWhiteSpace(spec))
		{
			return;
		}

		string[] names = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		MegaAnimationState? state = sprite.TryGetAnimationState();
		if (state is null || names.Length == 0)
		{
			return;
		}

		try
		{
			// 单段（idle）直接循环；多段（intro,loop）第一段单次播放，
			// 后面依次排队，末段循环 —— 与"入场演完接循环"的意图一致。
			if (names.Length == 1)
			{
				state.SetAnimation(names[0], loop: true, trackId: 0);
				return;
			}

			state.SetAnimation(names[0], loop: false, trackId: 0);
			for (int i = 1; i < names.Length; i++)
			{
				state.AddAnimation(names[i], 0f, loop: i == names.Length - 1, trackId: 0);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn($"立绘动画启动失败（{spec}）：{ex.Message}");
		}
	}

	/// <summary>
	/// 在角色的背景容器下找到宿主场景里的 <c>VariantHost</c>。
	/// </summary>
	private static Control? FindVariantHost(NCharacterSelectScreen screen)
	{
		Control? container = screen.GetNodeOrNull<Control>(BackgroundContainerNodeName);
		if (container is null)
		{
			return null;
		}

		// 容器里可能同时挂着别的角色背景，挑带 VariantHost 的那一个
		foreach (Node child in container.GetChildren())
		{
			if (child.GetNodeOrNull<Control>(VariantHostNodeName) is { } host)
			{
				return host;
			}
		}

		return null;
	}

	/// <summary>把设置里的背景分类翻译成场景路径。</summary>
	private static string ResolveScenePath()
	{
		return HeidemarieSettings.CurrentCharacter.CharacterSelectBackgroundVariant switch
		{
			CharacterSelectBackgroundVariant.LiveLobby => LiveLobbyScenePath,
			CharacterSelectBackgroundVariant.TeamJoinPortrait => DynamicPortraitScenePath,
			_ => StaticPortraitScenePath,
		};
	}

	/// <summary>
	/// 按视口尺寸把 liveevent 的各层统一摆到屏幕中心并等比缩放。
	///
	/// <para>逻辑照搬参考 mod 的 live 布局脚本：
	/// <c>ratio = max(视口宽/1580, 视口高/720)</c>，各层位置一律取屏幕中心
	/// （因为官方 le 布局把视口原点摆在 (-790,-360)，其中心正好是 (0,0)）。</para>
	/// </summary>
	private static void LayoutLiveLobby(Node root)
	{
		Vector2 target = root is Control control ? control.Size : Vector2.Zero;
		if (target.X <= 1f || target.Y <= 1f)
		{
			target = (root as Control)?.GetViewportRect().Size ?? Vector2.Zero;
		}

		if (target.X <= 1f || target.Y <= 1f)
		{
			target = new Vector2(1920f, 1080f);
		}

		float ratio = Mathf.Max(target.X / LiveLobbyViewportWidth, target.Y / LiveLobbyViewportHeight);
		Vector2 origin = target * 0.5f;

		foreach (Node child in root.GetChildren())
		{
			if (child is Node2D layer)
			{
				layer.Position = origin;
				layer.Scale = Vector2.One * ratio;
			}
		}
	}

	/// <summary>
	/// 让 liveevent 的可见层循环播放 <c>lobby</c>。
	///
	/// <para>骨架就绪时机不确定（第一次调用时 <see cref="MegaSprite.HasAnimation"/> 可能还没数据），
	/// 因此按固定间隔重试，直到全部起来或超过上限；已启动的层用 meta 标记跳过，
	/// 避免重复 <c>SetAnimation</c> 把动画重置回开头。</para>
	/// </summary>
	private static void StartLiveLobbyLoops(Node root, int attempt = 0)
	{
		// 重试回调可能在玩家离开选人界面之后才触发，此时 root 已被释放。
		// 直接访问会抛 ObjectDisposedException（曾把日志刷满），所以先做有效性检查。
		if (!GodotObject.IsInstanceValid(root))
		{
			return;
		}

		int pending = 0;

		foreach (Node child in root.GetChildren())
		{
			if (child is not Node2D layer || (layer is CanvasItem item && !item.Visible))
			{
				continue;
			}

			if (layer.HasMeta(LoopStartedMeta))
			{
				continue;
			}

			MegaSprite sprite = new(layer);
			bool hasLoop;
			try
			{
				hasLoop = sprite.HasAnimation(LiveLobbyLoopAnimation);
			}
			catch (Exception)
			{
				// 骨架数据尚未加载时查询会抛，按「还没就绪」处理，稍后重试
				hasLoop = false;
			}

			if (!hasLoop)
			{
				pending++;
				continue;
			}

			sprite.GetAnimationState()?.SetAnimation(LiveLobbyLoopAnimation, true, 0);
			layer.SetMeta(LoopStartedMeta, true);
		}

		if (pending > 0 && attempt < LoopStartMaxAttempts)
		{
			// 回调里不再判有效性，统一交给方法入口判断，避免 TOCTOU
			GodotScheduler.Delay(LoopStartRetrySeconds, () => StartLiveLobbyLoops(root, attempt + 1));
		}
	}
}
