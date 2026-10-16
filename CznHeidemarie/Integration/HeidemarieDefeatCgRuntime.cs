using System;
using CznCore.Godot;
using CznCore.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「败北 CG」接入层 —— <b>Tiphera 类型</b>实现：直接实例化
/// <c>res://CznHeidemarie/scenes/defeat_gloom_overlay.tscn</c>
/// （6 层官方 gloom Spine FX + cha_node 骨骼时间线驱动的 12 槽 CG 演出），
/// 取代早期手搓叠层与 <c>DefeatOverlayRuntime</c> 静态立绘路线。
///
/// <para>与参考 mod（Tiphera）的同构部分：GDScript 场景自管演出
/// （黑底、取景、2s 寿命自释放），C# 侧只负责「选图 + 注入三参 + 挂载」；
/// 角色相关参数仅 <c>cg_texture_path</c> / <c>cg_outer_scale</c> /
/// <c>srue_offset</c> 三个，其余（6 层 FX、12 槽时间线、PMA 材质）是
/// 官方共享资产，角色无关。</para>
///
/// <para><b>类型映射</b>（沿用 Tiphera 的配对，offset 与贴图是配套调过的）：
/// 类型 1 → collapse_30093_02.png（0.9 倍 + 下移 160）；
/// 类型 2 → collapse_30093_01.png（1.0 倍无偏移）；随机 → 各 50%。</para>
///
/// <para><b>触发时机</b>：<c>PlayerDeathResolvedEvent</c> 在原生死亡 hook
/// 完成之后才发布（倒地动画已结束），起播延迟取
/// <c>max(DeathAnimationLength, 1.1333s)</c>（Tiphera 验证值），
/// 让败北结算界面稳住后再压演出；放弃跑图路径同样经
/// <c>GuaranteeKillAllPlayers</c> → 死亡 hook → 本事件抵达，无需额外补丁。</para>
///
/// <para><b>音频</b>：塌落音效 <c>se_30093_collapse.ogg</c> 仍由死亡占位
/// 事件拦截补丁（<c>HeidemarieCombatAudio.OnPlayerDeath</c>）在阵亡瞬间
/// 播放，CG 侧不重复出声；gloom 环境音（Tiphera 的心跳 + gloom cue）
/// czn30093 没有对应素材，未移植。</para>
/// </summary>
internal static class HeidemarieDefeatCgRuntime
{
	/// <summary>gloom 演出场景（GDScript，6 层 FX + 12 槽 CG 时间线）。</summary>
	private const string OverlayScenePath =
		"res://CznHeidemarie/scenes/defeat_gloom_overlay.tscn";

	/// <summary>类型 1 立绘（Tiphera 同款映射：类型 1 用 02 图）。</summary>
	private const string Type1TexturePath =
		"res://CznHeidemarie/images/defeat/collapse_30093_02.png";

	/// <summary>类型 2 立绘。</summary>
	private const string Type2TexturePath =
		"res://CznHeidemarie/images/defeat/collapse_30093_01.png";

	/// <summary>类型 1 外扩缩放（与 offset 配套，Tiphera 验证值）。</summary>
	private const float Type1OuterScale = 0.9f;

	/// <summary>类型 2 外扩缩放。</summary>
	private const float Type2OuterScale = 1f;

	/// <summary>类型 1 立绘微调偏移（向下，让卧姿构图落在安全区）。</summary>
	private static readonly Vector2 Type1Offset = new(0f, 160f);

	/// <summary>运行时节点名前缀（清理时按名查找叠层）。</summary>
	private const string RuntimeNodePrefix = "CznHeidemarie";

	/// <summary>叠层节点名（实例化后强制改名，保证全工程唯一可查）。</summary>
	private const string OverlayNodeName = RuntimeNodePrefix + "BattleDeadGloomOverlay";

	/// <summary>
	/// 最小起播延迟（秒）：阵亡事件在倒地动画结束后才到，参考实现的验证值；
	/// 实际延迟取 <c>max(DeathAnimationLength, 此值)</c>。
	/// </summary>
	private const float MinStartDelaySeconds = 1.1333f;

	/// <summary>重复入场抑制窗口（毫秒）：同一次败北只演一场。</summary>
	private const ulong DuplicateSuppressMs = 2000uL;

	/// <summary>去重门（对应 Tiphera 的 <c>TimedDuplicateSuppressor</c>）。</summary>
	private static readonly TimedDuplicateSuppressor DuplicateGate = new();

	/// <summary>场景缓存（启动预热时装入）。</summary>
	private static PackedScene? _overlayScene;

	private static readonly List<IDisposable> Subscriptions = new();

	/// <summary>
	/// 订阅阵亡与生命周期事件。由 <see cref="HeidemarieIntegration.Init"/> 调用。
	/// </summary>
	internal static void Initialize()
	{
		// 启动即按当前设置预热（关着就不预热）：场景 + 两张立绘
		Prewarm();

		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG", (PlayerDeathResolvedEvent e) => OnDeath(e)));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 入房", (RoomEnteringEvent _) => Cleanup()));
		Subscriptions.Add(SkinEventHub.Subscribe("海德玛丽 败北 CG 胜利", (PlayerCombatVictoryEvent _) => Cleanup()));
	}

	/// <summary>
	/// 预热败北 CG 资源（设置变更后由外部调用也行，加载一次即进缓存）。
	/// </summary>
	internal static void Prewarm()
	{
		try
		{
			if (_overlayScene == null)
			{
				_overlayScene = ResourceLoader.Load<PackedScene>(
					OverlayScenePath, null, ResourceLoader.CacheMode.Reuse);
			}
			PrewarmTexture(Type1TexturePath);
			PrewarmTexture(Type2TexturePath);
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 预热失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 立刻掐掉正在播放的叠层（换房 / 胜利时调用）。
	/// </summary>
	/// <remarks>
	/// 演出场景 2s 后自释放，正常路径不用管；这里兜底换房 / 胜利时还挂着的情况。
	/// </remarks>
	internal static void Cleanup()
	{
		try
		{
			Node? overlay = FindOverlay();
			if (overlay is not null && GodotObject.IsInstanceValid(overlay))
			{
				overlay.QueueFree();
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 清理失败：" + ex.Message);
		}
	}

	/// <summary>阵亡后按设置决定要不要播 CG。</summary>
	private static void OnDeath(PlayerDeathResolvedEvent e)
	{
		if (!HeidemariePresentationHost.IsLocalHeidemarie(e.Player))
		{
			return;
		}
		if (!HeidemariePresentationHost.Features.DeathCgOverlay
			|| !HeidemarieSettings.CurrentCharacter.ShowDefeatCgOverlay)
		{
			return;
		}
		TryPlay(MathF.Max(e.DeathAnimationLength, MinStartDelaySeconds), "combat_death");
	}

	/// <summary>去重 + 预热 + 入队。</summary>
	private static void TryPlay(float startDelaySeconds, string source)
	{
		try
		{
			if (!DuplicateGate.TryEnter(Time.GetTicksMsec(), DuplicateSuppressMs))
			{
				HeidemarieIntegration.Log.Info("跳过重复败北 CG（source=" + source + "）。");
				return;
			}
			Prewarm();
			QueueOverlay(startDelaySeconds);
			HeidemarieIntegration.Log.Info(
				"败北 CG 已入队（source=" + source
				+ " 延迟=" + startDelaySeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
				+ " 模式=" + HeidemarieSettings.CurrentCharacter.DefeatCgOverlayMode + "）。");
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 播放失败（source=" + source + "）：" + ex.Message);
		}
	}

	/// <summary>延迟到点后把演出场景挂上视口。</summary>
	private static void QueueOverlay(float startDelaySeconds)
	{
		Viewport? viewport = GetOverlayViewport();
		if (viewport == null)
		{
			HeidemarieIntegration.Log.Warn("败北 CG 跳过：找不到视口。");
			return;
		}

		(string TexturePath, float OuterScale, Vector2 Offset) selection =
			ResolveOverlaySelection(HeidemarieSettings.CurrentCharacter.DefeatCgOverlayMode);
		if (string.IsNullOrWhiteSpace(selection.TexturePath))
		{
			return;
		}

		if (startDelaySeconds <= 0.01f)
		{
			ShowOverlay();
		}
		else
		{
			GodotScheduler.OneShot(viewport, startDelaySeconds, ShowOverlay);
		}

		void ShowOverlay()
		{
			if (!GodotObject.IsInstanceValid(viewport))
			{
				return;
			}
			PackedScene? scene = _overlayScene
				?? ResourceLoader.Load<PackedScene>(OverlayScenePath, null, ResourceLoader.CacheMode.Reuse);
			_overlayScene = scene;
			Node? overlay = scene?.Instantiate();
			if (overlay == null)
			{
				HeidemarieIntegration.Log.Warn("败北 CG 场景缺失：" + OverlayScenePath);
				return;
			}
			overlay.Name = OverlayNodeName;
			// 三个角色相关参数在 _ready 之前注入（场景 _ready 读到的就是目标值）
			overlay.Set("cg_texture_path", selection.TexturePath);
			overlay.Set("cg_outer_scale", selection.OuterScale);
			overlay.Set("srue_offset", selection.Offset);
			viewport.AddChild(overlay, false, Node.InternalMode.Disabled);
		}
	}

	/// <summary>演出挂载点：战斗房间视口优先，退回场景树根。</summary>
	private static Viewport? GetOverlayViewport()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		Viewport? viewport = room?.GetViewport();
		if (viewport is not null && GodotObject.IsInstanceValid(viewport))
		{
			return viewport;
		}
		SceneTree? tree = Engine.GetMainLoop() as SceneTree;
		if (tree is null || !GodotObject.IsInstanceValid(tree.Root))
		{
			return null;
		}
		return tree.Root;
	}

	/// <summary>
	/// 按设置选图：类型 1 → 02 图（0.9 + 下移 160）；类型 2 → 01 图（1.0）；
	/// 随机 → 各 50%。与 Tiphera 的 <c>ResolveOverlaySelection</c> 同映射。
	/// </summary>
	private static (string TexturePath, float OuterScale, Vector2 Offset) ResolveOverlaySelection(
		DefeatCgOverlayMode mode)
	{
		if (mode != DefeatCgOverlayMode.Type2
			&& (mode != DefeatCgOverlayMode.Random || Random.Shared.Next(2) != 1))
		{
			return (Type1TexturePath, Type1OuterScale, Type1Offset);
		}
		return (Type2TexturePath, Type2OuterScale, Vector2.Zero);
	}

	/// <summary>按约定节点名找当前叠层；没有则返回 null。</summary>
	private static Node? FindOverlay()
	{
		SceneTree? tree = Engine.GetMainLoop() as SceneTree;
		if (tree?.Root is not { } root)
		{
			return null;
		}
		// 演出挂在根视口上（NCombatRoom 的 GetViewport() 就是根视口）
		return root.FindChild(OverlayNodeName, recursive: false, owned: false);
	}

	private static void PrewarmTexture(string path)
	{
		if (!string.IsNullOrWhiteSpace(path))
		{
			ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
		}
	}
}
