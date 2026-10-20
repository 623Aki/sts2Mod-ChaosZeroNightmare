using System;
using System.Collections.Generic;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「连接」视觉层根节点 —— 只为拿到每帧回调，具体逻辑都在
/// <see cref="HeidemarieLinkOverlay"/> 里。
/// </summary>
internal sealed partial class HeidemarieLinkOverlayRoot : Node2D
{
	/// <inheritdoc/>
	public override void _Process(double delta) => HeidemarieLinkOverlay.Tick(this, (float)delta);
}

/// <summary>
/// 「连接」关键字的手牌视觉层：把卡厄斯梦境原版的「卡牌连接」光带搬到 STS2 手牌上。
///
/// <para><b>观感</b>（2026-10-16 在 <c>tools/HeidemarieEffectLab/link_lab</c> 预览台里定稿）：</para>
/// <list type="number">
///   <item><b>光带</b>：相邻两张连接牌的「顶边中点」之间向上拱起一条贝塞尔光带。
///   分<b>两层</b> —— 外层光晕（厚、淡、柔、无脉冲）+ 主线（薄、亮、带自左向右扫过的脉冲），
///   颜色是「核心色（亮青）→ 边缘色（电蓝）」双色渐变，两端柔和收口；</item>
///   <item><b>贴合浮动粒子</b>：原版 <c>tri_pati_01</c> 贴在光带上，沿法线方向小幅上下浮动，
///   任意时刻线的上下两侧都有粒子，贴近线时更亮更大。</item>
/// </list>
///
/// <para><b>为什么分两层 / 为什么不用原版着色器</b>：原版 <c>card_connect_flow.gdshader</c>
/// 在 Godot 里编译不过（uniform 默认值写了算式、<c>vertex()</c> 里用了 <c>return</c>、
/// 自定义函数里用了内置变量 <c>UV</c>/<c>TIME</c>），而且它把 <c>uv.x</c> 覆盖成纯相位值，
/// 沿带方向没有变化、看不出流动。现在统一走定稿版 <c>link_ribbon.gdshader</c>。</para>
///
/// <para><b>链条顺序与拖拽</b>：手牌里所有带「连接」的牌按手牌顺序两两相邻连成一条链。</para>
/// <para><b>拖拽中的牌不在 <c>CardHolderContainer</c> 里</b>（玩家按下出牌时
/// <c>NPlayerHand.StartCardPlay</c> 会把 holder reparent 到手牌节点自身），所以这里用
/// <see cref="StartCardPlayPatch"/> 记下它的槽位索引，每帧把它插回链条对应位置 ——
/// 于是同一条光带的两个端点只是各自跟着移动，<b>光带会自然被拉长</b>，
/// 且不会因为重建节点而闪烁。</para>
///
/// <para><b>状态动画</b>：光带以「holder 对」为键常驻复用，用权重做出现 / 消失过渡：</para>
/// <list type="bullet">
///   <item><b>拖起</b>：键不变，端点跟手移动 ⇒ 光带被拉长（跨距越大拱高越平，像橡皮筋）；</item>
///   <item><b>放下（取消）</b>：holder 被放回容器，键仍不变 ⇒ 光带自己缩回去；</item>
///   <item><b>打出</b>：该牌离开链条 ⇒ 连着它的两条带淡出、并把端点收进相邻那张牌；
///   同时左右邻牌之间新出现一条带并淡入，就是「重新接上」的观感；</item>
///   <item><b>切换卡拖动</b>（数字快捷键换一张拖）：新旧 holder 的键都不变，过渡无缝。</item>
/// </list>
///
/// <para><b>挂载点与 z 序</b>：挂 <see cref="NPlayerHand"/> 自己（<c>CardHolderContainer</c> 的兄弟），
/// 用 <see cref="OverlayZIndex"/>（<b>负值</b>）压在<b>手牌之下、背景之上</b> ——
/// 于是拖动的牌（尤其是非连接牌）会自然遮住连线段，符合视角上的前后关系；
/// 光带拱起的部分在牌顶边之上的空白区，仍然看得见。</para>
///
/// <para><b>开关</b>：需要「连接动画」（本功能自己的开关）与「战斗特效」总开关同时打开；
/// 粒子再叠加「额外粒子」开关，与卡牌拖尾粒子同一口径。</para>
///
/// <para><b>已知限制</b>：静态字段只保存一份实例，多人联机下每个玩家各自有手牌 UI 时，
/// 只有最后就绪的那一份会被画上连线（本工程其余运行时同样是单实例模型）。</para>
/// </summary>
internal static class HeidemarieLinkOverlay
{
	// ──────────────────────────────────────────────────────────────────────
	// 资源路径
	// ──────────────────────────────────────────────────────────────────────

	private const string ShaderPath = "res://CznHeidemarie/link_vfx/link_ribbon.gdshader";
	private const string RibbonTexturePath = "res://CznHeidemarie/link_vfx/line_connect_1.png";
	private const string ParticleTexturePath = "res://CznHeidemarie/link_vfx/tri_pati_01.png";

	// ──────────────────────────────────────────────────────────────────────
	// 视觉标定（都按「卡牌显示尺寸的比例」写，换分辨率 / 换手牌缩放都不用改）
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 视觉层在 <see cref="NPlayerHand"/> 下的 z：取 <b>负</b> —— 压在手牌之下、背景之上。
	///
	/// <para>同层邻居的真值（都来自游戏源码）：手牌 holder 常态 <c>0</c>、hover 时 <c>1</c>；
	/// 战斗 VFX 容器 <c>-9</c>、战斗场景（背景）<c>-10</c>。所以 <c>-1</c> 正好卡在中间：
	/// 手牌（含正在拖的那张）盖住连线，背景盖不住连线。</para>
	///
	/// <para><b>为什么要低于手牌</b>：连线是连接两张牌的「光带」，视角上应该被牌压住 ——
	/// 尤其拖着一张<b>非连接牌</b>横穿过去时，那张牌挡住光带才符合前后关系；
	/// 压在牌之上的话，光带会浮在所有卡面上，看着像贴片。</para>
	///
	/// <para>拱起段本来就落在牌顶边之上的空白区，压低 z 不会被自己的牌吃掉。</para>
	/// </summary>
	private const int OverlayZIndex = -1;

	/// <summary>光带网格的曲线细分段数。</summary>
	private const int RibbonSegments = 40;

	/// <summary>主线半厚 / 卡牌显示高度。</summary>
	private const float RibbonHalfThicknessRatio = 0.03f;

	/// <summary>光带拱高 / 卡牌显示高度（从卡牌顶边中点继续向上拱）。</summary>
	private const float ArchRatio = 0.30f;

	/// <summary>
	/// 拱高上限 / 卡牌显示高度：<b>方案 B</b> —— 静止时拱高随跨距增长，但不超过这个倍数。
	/// 跨得越远拱越高，保证长跨距（两张连接牌中间隔着好几张普通牌）也高过手牌、
	/// 不会变成贴着牌顶横切的一条平线。
	/// </summary>
	private const float MaxArchRatio = 1.20f;

	/// <summary>
	/// 拖拽时的拱高 / 卡牌显示高度：端点里有正在被拖动的牌时反过来「压平」，像一根绷紧的丝。
	/// </summary>
	private const float DragArchFactor = 0.10f;

	/// <summary>拱顶距离视口顶部至少留出的空白（canvas 像素）。</summary>
	private const float ArchTopMargin = 24f;

	/// <summary>外层光晕的厚度倍数（相对主线半厚）。</summary>
	private const float HaloScale = 2.6f;

	/// <summary>外层光晕的不透明度。</summary>
	private const float HaloAlpha = 0.26f;

	/// <summary>主线不透明度。</summary>
	private const float AlphaMul = 0.72f;

	/// <summary>沿带流动速度。</summary>
	private const float FlowSpeed = 0.55f;

	/// <summary>纵向波动幅度。</summary>
	private const float FlowForce = 0.06f;

	/// <summary>自左向右扫过的脉冲强度 / 速度 / 宽度。</summary>
	private const float PulseGain = 1.2f;

	/// <summary>脉冲速度。</summary>
	private const float PulseSpeed = 0.35f;

	/// <summary>脉冲宽度（越小越像一团能量包）。</summary>
	private const float PulseWidth = 0.16f;

	/// <summary>厚度方向剖面幂次：越大越细硬，越小越柔宽。</summary>
	private const float SoftPower = 1.0f;

	/// <summary>纵向取贴图中段比例。</summary>
	private const float VZoom = 0.50f;

	/// <summary>核心色收窄程度：越大只有中线是亮青。</summary>
	private const float CoreSharp = 3.5f;

	/// <summary>两端收口比例（避免相邻两条带在卡牌接点处叠加成一团白亮）。</summary>
	private const float EndFade = 0.06f;

	/// <summary>中线核心色（亮青）。</summary>
	private static readonly Color CoreColor = new(0.38f, 0.86f, 1.0f);

	/// <summary>两侧边缘色（电蓝）。</summary>
	private static readonly Color EdgeColor = new(0.02f, 0.28f, 0.95f);

	/// <summary>被拖拽拉长时线宽收敛到的最小倍率（绷紧感）。</summary>
	private const float StretchThicknessMin = 0.78f;

	/// <summary>光带出现 / 消失的过渡时长（秒）。</summary>
	private const float AppearSeconds = 0.12f;

	/// <summary>光带消失过渡时长。</summary>
	private const float DisappearSeconds = 0.18f;

	/// <summary>每条光带挂多少颗「贴合浮动」粒子。</summary>
	private const int ParticlesPerRibbon = 20;

	/// <summary>粒子大小 / 卡牌显示高度。</summary>
	private const float ParticleSizeRatio = 0.04f;

	/// <summary>粒子沿法线（上下）浮动幅度 / 卡牌显示高度。</summary>
	private const float ParticleAmpRatio = 0.04f;

	/// <summary>粒子上下浮动角速度（弧度/秒）。</summary>
	private const float ParticleBobSpeed = 4.5f;

	/// <summary>粒子沿带漂移速度（t/秒）。</summary>
	private const float ParticleAdvance = 0.10f;

	/// <summary>原版卡面高度（把原版「相对 704 卡面」的尺寸参数折算到实际卡牌用）。</summary>
	private const float ReferenceCardHeight = 704f;

	/// <summary>粒子贴图（tri_pati_01.png）原始尺寸。</summary>
	private static readonly Vector2 ParticleTextureSize = new(84f, 88f);

	// ──────────────────────────────────────────────────────────────────────
	// 状态
	// ──────────────────────────────────────────────────────────────────────

	private static HeidemarieLinkOverlayRoot? _root;
	private static Node2D? _ribbonRoot;

	/// <summary>
	/// 挂载时拿到的 NPlayerHand 实例。
	///
	/// <para>⚠️ 不能再靠 <c>NPlayerHand.Instance</c>（= <c>NCombatRoom.Instance.Ui.Hand</c>）：
	/// Godot 的 <c>_Ready</c> 是<b>子节点先于父节点</b>，而 <c>Hand</c> 是在
	/// <see cref="NCombatUi"/>（NPlayerHand 的祖先）的 <c>_Ready</c> 里才被赋值的，
	/// 所以在 <see cref="NPlayerHand"/> 的 <c>_Ready</c> 后置补丁里它是 <c>null</c>。</para>
	/// </summary>
	private static NPlayerHand? _hand;

	/// <summary>当前正在被拖动的 holder（由 <see cref="StartCardPlayPatch"/> 记录）。</summary>
	private static NHandCardHolder? _draggedHolder;

	/// <summary>本帧确认「确实挂在手牌节点下、正在被拖」的那张（判断光带要不要压平用）。</summary>
	private static NHandCardHolder? _activeDragged;

	/// <summary>拖起时它在 <c>CardHolderContainer</c> 里的槽位索引（插回链条用）。</summary>
	private static int _draggedIndex = -1;

	private static Shader? _shader;
	private static Texture2D? _ribbonTexture;
	private static Texture2D? _particleTexture;
	private static CanvasItemMaterial? _additiveMaterial;

	private static bool _loggedFailure;
	private static bool _loggedAttach;
	private static bool _loggedFallbackSize;
	private static bool _loggedMissingDragIndex;
	private static bool _loggedCoverCheck;
	private static bool _particlesVisibleCached = true;

	/// <summary>光带池：键 = （左 holder 实例 id, 右 holder 实例 id）。复用而非每帧重建，过渡才连得上。</summary>
	private static readonly Dictionary<(ulong Left, ulong Right), Ribbon> _ribbons = new();

	/// <summary>每帧的目标键集合。</summary>
	private static readonly HashSet<(ulong Left, ulong Right)> _desired = new();

	/// <summary>待回收的键（不能在遍历字典时直接删）。</summary>
	private static readonly List<(ulong Left, ulong Right)> _graveyard = new();

	/// <summary>容器里的 holder（手牌顺序）。</summary>
	private static readonly List<NHandCardHolder> _inContainer = new();

	/// <summary>插回被拖拽的 holder 之后的完整顺序。</summary>
	private static readonly List<NHandCardHolder> _ordered = new();

	/// <summary>链条：只保留带「连接」的牌。</summary>
	private static readonly List<NHandCardHolder> _chain = new();

	/// <summary>
	/// 一条光带 = 外层光晕 + 主线 + 一簇「贴合浮动」粒子。
	/// </summary>
	private sealed class Ribbon
	{
		/// <summary>容器节点（光带与粒子都挂在它下面）。</summary>
		public Node2D Group = null!;

		/// <summary>外层光晕 / 主线。</summary>
		public Band Halo = null!;
		public Band Core = null!;

		/// <summary>两端对应的 holder（消失过程中可能已经失效）。</summary>
		public NHandCardHolder? Left;
		public NHandCardHolder? Right;

		/// <summary>上一帧的两端点（holder 失效时用来把端点收回去）。</summary>
		public Vector2 LastFrom;
		public Vector2 LastTo;

		/// <summary>最近一次有效时的卡牌显示高度（canvas 像素），holder 失效后仍要用它算厚度。</summary>
		public float CardHeight;

		/// <summary>出现 / 消失权重（0..1）。</summary>
		public float Weight;

		/// <summary>这一帧是否已被判定为「应该消失」。</summary>
		public bool Dying;

		/// <summary>贴合浮动粒子的 Sprites 与它们的相位 / 速度 / 错位 / 沿带参数。</summary>
		public Sprite2D[] Sparks = Array.Empty<Sprite2D>();
		public float[] SparkT = Array.Empty<float>();
		public float[] SparkPhase = Array.Empty<float>();
		public float[] SparkBob = Array.Empty<float>();
		public float[] SparkJitter = Array.Empty<float>();

		/// <summary>回收整条光带（粒子是 Group 的子节点，会一起走）。</summary>
		public void Dispose()
		{
			if (GodotObject.IsInstanceValid(Group))
			{
				Group.GetParent()?.RemoveChild(Group);
				Group.QueueFree();
			}
		}
	}

	/// <summary>一层条带网格（光晕或主线）。</summary>
	private sealed class Band
	{
		public MeshInstance2D Node = null!;
		public ShaderMaterial Material = null!;
		public ArrayMesh Mesh = null!;
		public Vector3[] Verts = null!;
		public Godot.Collections.Array Arrays = null!;
	}

	// ──────────────────────────────────────────────────────────────────────
	// 接入
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 手牌就绪时把手牌视觉层挂上去。
	///
	/// <para>挂 <see cref="NPlayerHand"/> 自己（而不是战斗 VFX 容器 <c>CombatVfxContainer</c>）：
	/// 一是<b>坐标系</b> —— 连线端点要按 holder 的全局变换换算，挂在手牌下最省事；
	/// 二是<b>层级</b> —— VFX 容器被游戏设成 <c>z = -9</c>（背景 <c>-10</c>），挂过去就沉到底了，
	/// 而现在要的是「比手牌低、比背景高」，由 <see cref="OverlayZIndex"/>（<c>-1</c>）单独控制。</para>
	/// </summary>
	internal static void Attach(NPlayerHand hand)
	{
		try
		{
			if (!GodotObject.IsInstanceValid(hand) || !HeidemarieLinkOverlaySettings.Enabled)
			{
				return;
			}

			// 幂等：本方法会被两个补丁各调一次（NPlayerHand._Ready + NCombatUi._Ready 兜底），
			// 已经挂在同一只手牌上就不用重建。
			if (_hand == hand && GodotObject.IsInstanceValid(_root) && _root!.IsInsideTree())
			{
				return;
			}

			// 先记住手牌实例：本方法之后要一直用它，不再依赖 NPlayerHand.Instance（见字段注释）。
			_hand = hand;

			EnsureResources();
			if (_shader == null || _ribbonTexture == null)
			{
				return;
			}

			// 旧节点还在（重进战斗 / 换手牌）时先清干净，避免重复叠加。
			ClearAll();
			if (GodotObject.IsInstanceValid(_root))
			{
				_root!.QueueFree();
			}

			_root = new HeidemarieLinkOverlayRoot
			{
				Name = "HeidemarieLinkOverlay",
				// 负 z：压在手牌之下、背景之上 —— 拖动的牌会遮住连线段，符合前后关系。
				ZIndex = OverlayZIndex,
			};
			hand.AddChild(_root, forceReadableName: false, Node.InternalMode.Disabled);

			_ribbonRoot = new Node2D { Name = "Ribbons" };
			_root.AddChild(_ribbonRoot, forceReadableName: false, Node.InternalMode.Disabled);

			_draggedHolder = null;
			_draggedIndex = -1;

			if (!_loggedAttach)
			{
				_loggedAttach = true;
				HeidemarieIntegration.Log.Info("连接视觉层已挂载到 NPlayerHand。");
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("连接视觉层挂载失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 设置面板改了与本功能相关的开关后调用（挂在
	/// <c>SkinSettingsUiRuntimeHooks.CleanupSkinVisualEffectsRuntime</c> 上）：
	/// 中途关掉就立刻收掉视觉层，中途打开就补挂一次（否则要等下一场战斗才生效）。
	/// </summary>
	internal static void Refresh()
	{
		try
		{
			if (!HeidemarieLinkOverlaySettings.Enabled)
			{
				SetVisible(false);
				return;
			}

			if (GodotObject.IsInstanceValid(_root) && _root!.IsInsideTree())
			{
				return;
			}

			NPlayerHand? hand = _hand;
			if (!GodotObject.IsInstanceValid(hand))
			{
				hand = NPlayerHand.Instance;
			}

			if (GodotObject.IsInstanceValid(hand))
			{
				Attach(hand!);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("连接视觉层刷新失败：" + ex.Message);
		}
	}

	/// <summary>记录「这张牌开始被拖了」，以及它在手牌容器里的槽位索引。</summary>
	internal static void OnDragStarted(NHandCardHolder holder)
	{
		try
		{
			_draggedHolder = holder;
			// ⚠️ 必须在 reparent 之前取索引：StartCardPlay 里紧接着就把它移出容器了。
			_draggedIndex = GodotObject.IsInstanceValid(holder) ? holder.GetIndex() : -1;
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("连接视觉层记录拖拽失败：" + ex.Message);
		}
	}

	/// <summary>由 <see cref="HeidemarieLinkOverlayRoot"/> 每帧调用。</summary>
	internal static void Tick(HeidemarieLinkOverlayRoot self, float delta)
	{
		try
		{
			if (!GodotObject.IsInstanceValid(self) || !self.IsInsideTree() || _ribbonRoot == null)
			{
				return;
			}

			// 有整屏 UI 盖在战斗界面上时整个收掉（否则光带会画在它们上面 —— 穿模）
			if (!EffectsEnabled || ScreenCovered)
			{
				ClearAll();
				return;
			}

			NPlayerHand? hand = ResolveHand();
			if (!GodotObject.IsInstanceValid(hand))
			{
				ClearAll();
				return;
			}

			CollectChain(hand!);
			SyncRibbons(delta);
			UpdateRibbons(delta);

			// 「额外粒子」是设置项，玩家可能战斗中途改，变化时才重套
			bool particles = ParticlesEnabled;
			if (particles != _particlesVisibleCached)
			{
				_particlesVisibleCached = particles;
				ApplyParticleToggle(particles);
			}
		}
		catch (Exception ex)
		{
			// 每帧都可能抛，只报第一次，避免刷日志
			if (!_loggedFailure)
			{
				_loggedFailure = true;
				HeidemarieIntegration.Log.Warn("连接视觉层刷新失败：" + ex);
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────
	// 采集链条
	// ──────────────────────────────────────────────────────────────────────

	private static NPlayerHand? ResolveHand()
	{
		NPlayerHand? hand = _hand;
		if (!GodotObject.IsInstanceValid(hand))
		{
			// 兜底：重进战斗后旧实例被回收时，再尝试从游戏侧取当前手牌
			hand = NPlayerHand.Instance;
			_hand = GodotObject.IsInstanceValid(hand) ? hand : null;
		}

		return hand;
	}

	/// <summary>
	/// 按手牌顺序取出带「连接」的卡牌 holder，并把<b>正在被拖拽的那张</b>插回它原来的槽位。
	/// </summary>
	private static void CollectChain(NPlayerHand hand)
	{
		_chain.Clear();
		_ordered.Clear();
		_inContainer.Clear();

		if (!GodotObject.IsInstanceValid(hand.CardHolderContainer))
		{
			return;
		}

		foreach (Node child in hand.CardHolderContainer.GetChildren())
		{
			if (child is NHandCardHolder holder)
			{
				_inContainer.Add(holder);
			}
		}

		_ordered.AddRange(_inContainer);

		// 拖拽中的 holder 已被 reparent 到手牌节点自身（不在容器里），
		// 按拖起时记下的槽位插回来，链条顺序才不会跳。
		NHandCardHolder? dragged = ResolveDragged(hand);
		_activeDragged = dragged;
		if (dragged != null)
		{
			int index = Mathf.Clamp(_draggedIndex, 0, _ordered.Count);
			_ordered.Insert(index, dragged);
		}
		else
		{
			WarnIfUnmanagedDrag(hand);
		}

		foreach (NHandCardHolder holder in _ordered)
		{
			// 只认「看得见」的 holder（与游戏自己的 ActiveHolders 同口径）：
			// 选牌模式 / 手牌被收起时，卡还在容器里但已经不可见，不该再给它们画光带。
			if (!holder.IsVisibleInTree())
			{
				continue;
			}

			// ⚠️ 必须走类型判定后读 IsLinked：手牌里可能有诅咒 / 状态等非本角色卡。
			// IsLinked 是虚属性（「拾星」升级后会覆盖它），因此这里读到的是最终判定。
			if (holder.CardModel is HeidemarieCard card && card.IsLinked)
			{
				_chain.Add(holder);
			}
		}
	}

	/// <summary>
	/// 兜底诊断：手牌节点下明明挂着 holder（说明有人正在拖牌），却没被记录到槽位。
	/// 正常情况下不会发生 —— 一旦出现就说明 <c>StartCardPlay</c> 补丁没打上，
	/// 表现是「拖牌时光带不跟着延展」，只报一次，方便定位。
	/// </summary>
	private static void WarnIfUnmanagedDrag(NPlayerHand hand)
	{
		// 只有真的在出牌 / 拖牌时才可能出问题：平时直接短路，免得每帧都去取一次子节点列表。
		if (_loggedMissingDragIndex || !hand.InCardPlay)
		{
			return;
		}

		foreach (Node child in hand.GetChildren())
		{
			if (child is NHandCardHolder)
			{
				_loggedMissingDragIndex = true;
				HeidemarieIntegration.Log.Warn(
					"连接视觉层：检测到拖拽中的 holder 未被记录，光带不会跟随拖动"
						+ "（多半是 NPlayerHand.StartCardPlay 补丁没生效）。");
				return;
			}
		}
	}

	/// <summary>
	/// 只有「拖起时记下的那张、且此刻仍挂在手牌节点下」才算拖拽中。
	///
	/// <para>放下（取消）时它会被放回容器，打出后会被回收 —— 两种情况都会自然落到 <c>null</c>，
	/// 顺手也把「排队等待出牌」的那些 holder 排除在链条之外。</para>
	/// </summary>
	private static NHandCardHolder? ResolveDragged(NPlayerHand hand)
	{
		NHandCardHolder? holder = _draggedHolder;
		if (holder == null || !GodotObject.IsInstanceValid(holder))
		{
			_draggedHolder = null;
			return null;
		}

		Node? parent = holder.GetParent();
		if (parent == null || parent.GetInstanceId() != hand.GetInstanceId())
		{
			return null;
		}

		return holder;
	}

	// ──────────────────────────────────────────────────────────────────────
	// 光带池：按 holder 对复用，用权重做出现 / 消失
	// ──────────────────────────────────────────────────────────────────────

	private static void SyncRibbons(float delta)
	{
		_desired.Clear();
		for (int i = 0; i + 1 < _chain.Count; i++)
		{
			_desired.Add((_chain[i].GetInstanceId(), _chain[i + 1].GetInstanceId()));
		}

		// 新建缺失的
		foreach ((ulong left, ulong right) key in _desired)
		{
			if (!_ribbons.ContainsKey(key))
			{
				NHandCardHolder? a = FindInChain(key.left);
				NHandCardHolder? b = FindInChain(key.right);
				if (a != null && b != null)
				{
					_ribbons[key] = CreateRibbon(_ribbons.Count, a, b);
				}
			}
		}

		// 标记该消失的
		foreach (KeyValuePair<(ulong Left, ulong Right), Ribbon> pair in _ribbons)
		{
			pair.Value.Dying = !_desired.Contains(pair.Key);
		}
	}

	private static NHandCardHolder? FindInChain(ulong id)
	{
		foreach (NHandCardHolder holder in _chain)
		{
			if (holder.GetInstanceId() == id)
			{
				return holder;
			}
		}

		return null;
	}

	/// <summary>造一条光带（外层光晕 + 主线 + 一簇贴合浮动粒子）。</summary>
	private static Ribbon CreateRibbon(int index, NHandCardHolder left, NHandCardHolder right)
	{
		Node2D group = new() { Name = $"Link{index}" };
		_ribbonRoot!.AddChild(group, forceReadableName: false, Node.InternalMode.Disabled);

		Ribbon ribbon = new()
		{
			Group = group,
			Halo = MakeBand(group, index, glow: true),
			Core = MakeBand(group, index, glow: false),
			Left = left,
			Right = right,
			Weight = 0f,
		};

		BuildSparks(ribbon);
		return ribbon;
	}

	/// <summary>造一层条带网格：UV.x = 曲线参数 t，UV.y = 0 / 1（横向沿法线撑开）。</summary>
	private static Band MakeBand(Node parent, int index, bool glow)
	{
		int pointCount = RibbonSegments + 1;
		Vector2[] uvs = new Vector2[pointCount * 2];
		int[] indices = new int[RibbonSegments * 6];

		for (int p = 0; p < pointCount; p++)
		{
			float t = (float)p / RibbonSegments;
			uvs[p * 2] = new Vector2(t, 0f);
			uvs[p * 2 + 1] = new Vector2(t, 1f);

			if (p >= RibbonSegments)
			{
				continue;
			}

			int v = p * 2;
			indices[p * 6] = v;
			indices[p * 6 + 1] = v + 1;
			indices[p * 6 + 2] = v + 2;
			indices[p * 6 + 3] = v + 1;
			indices[p * 6 + 4] = v + 3;
			indices[p * 6 + 5] = v + 2;
		}

		Vector3[] verts = new Vector3[pointCount * 2];
		Godot.Collections.Array arrays = new();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		ArrayMesh mesh = new();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		ShaderMaterial material = new() { Shader = _shader };
		material.SetShaderParameter("u_texture", _ribbonTexture!);
		ApplyShader(material, index, glow);

		MeshInstance2D node = new()
		{
			Name = glow ? "Halo" : "Core",
			Mesh = mesh,
			Material = material,
			// 颜色交给着色器的「核心色 / 边缘色」双色渐变，节点 modulate 只用来做权重淡入淡出，
			// 所以这里不要再乘一层染色（加算下会过曝成纯白）。
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		parent.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);

		return new Band { Node = node, Material = material, Mesh = mesh, Verts = verts, Arrays = arrays };
	}

	/// <summary>把定稿参数套到某一层材质上；<paramref name="glow"/> 为真时按外层光晕处理。</summary>
	private static void ApplyShader(ShaderMaterial material, int index, bool glow)
	{
		material.SetShaderParameter("u_flow_speed", FlowSpeed);
		material.SetShaderParameter("u_flow_force", FlowForce);
		material.SetShaderParameter("u_pulse_speed", PulseSpeed);
		material.SetShaderParameter("u_pulse_width", PulseWidth);
		material.SetShaderParameter("u_pulse_gain", glow ? 0f : PulseGain);
		material.SetShaderParameter("u_core_color", new Vector3(CoreColor.R, CoreColor.G, CoreColor.B));
		material.SetShaderParameter("u_edge_color", new Vector3(EdgeColor.R, EdgeColor.G, EdgeColor.B));
		material.SetShaderParameter("u_alpha_mul", glow ? HaloAlpha : AlphaMul);
		material.SetShaderParameter("u_soft_power", glow ? SoftPower * 0.6f : SoftPower);
		material.SetShaderParameter("u_v_zoom", VZoom);
		material.SetShaderParameter("u_core_sharp", CoreSharp);
		material.SetShaderParameter("u_end_fade", EndFade);
		material.SetShaderParameter("u_glow_mix", glow ? 1f : 0f);
		// 每条错开相位，避免多条光带整齐划一地同步流动
		material.SetShaderParameter("u_time_offset", index * 0.37f);
	}

	// ──────────────────────────────────────────────────────────────────────
	// 每帧更新
	// ──────────────────────────────────────────────────────────────────────

	private static void UpdateRibbons(float delta)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		// 换算基准：连线层的 canvas 变换。同一帧内不变，取一次即可。
		Transform2D toCanvas = _root!.GetGlobalTransformWithCanvas();
		Transform2D inverseRoot = toCanvas.AffineInverse();
		// 连线层本身可能被父级缩放过，几何量（拱高 / 半厚 / 粒子）都按 canvas 像素算好后折算回本地单位
		float rootScale = Mathf.Abs(toCanvas.Scale.X);
		if (rootScale < 0.0001f)
		{
			rootScale = 1f;
		}

		_graveyard.Clear();

		foreach (KeyValuePair<(ulong Left, ulong Right), Ribbon> pair in _ribbons)
		{
			Ribbon ribbon = pair.Value;

			if (ribbon.Dying)
			{
				ribbon.Weight -= delta / DisappearSeconds;
				if (ribbon.Weight <= 0f)
				{
					_graveyard.Add(pair.Key);
					continue;
				}
			}
			else
			{
				ribbon.Weight = Mathf.Min(1f, ribbon.Weight + delta / AppearSeconds);
			}

			UpdateRibbon(ribbon, inverseRoot, rootScale, delta);
		}

		foreach ((ulong Left, ulong Right) key in _graveyard)
		{
			_ribbons[key].Dispose();
			_ribbons.Remove(key);
		}

		if (_graveyard.Count > 0)
		{
			_graveyard.Clear();
		}
	}

	private static void UpdateRibbon(Ribbon ribbon, Transform2D inverseRoot, float rootScale, float delta)
	{
		bool leftValid = ribbon.Left != null && GodotObject.IsInstanceValid(ribbon.Left);
		bool rightValid = ribbon.Right != null && GodotObject.IsInstanceValid(ribbon.Right);

		Vector2 from;
		Vector2 to;
		float cardHeightCanvas;

		if (leftValid && rightValid)
		{
			from = TopCenter(ribbon.Left!, inverseRoot);
			to = TopCenter(ribbon.Right!, inverseRoot);
			cardHeightCanvas = GetCanvasSize(ribbon.Left!).Y;
			ribbon.LastFrom = from;
			ribbon.LastTo = to;
			ribbon.CardHeight = cardHeightCanvas;
		}
		else
		{
			// 有一端已经没了（例如牌被打出、holder 被回收）：把端点朝另一端收回去，
			// 淡出的同时看起来就是「光带缩回邻牌」，而不是原地僵住。
			float retreat = 1f - ribbon.Weight;
			from = ribbon.LastFrom;
			to = ribbon.LastTo.Lerp(ribbon.LastFrom, retreat);
			cardHeightCanvas = ribbon.CardHeight;
		}

		if (cardHeightCanvas < 1f)
		{
			cardHeightCanvas = ReferenceCardHeight;
		}

		// ── 拱高：方案 B ──
		// 静止时拱高随跨距「增长」并封顶（跨得越远拱越高，保证高过手牌、不会横切卡面）；
		// 只有端点里含「正在被拖拽的那张牌」时才压平 —— 那是跟手绷紧的丝，不该拱起来。
		float span = from.DistanceTo(to);
		float spanRatio = span / Mathf.Max(cardHeightCanvas, 1f);
		bool dragged = IsDragged(ribbon);
		// 张力 0..1：跨距从 1 个卡高涨到 4 个卡高时，从「基础拱」压到「拖拽拱」
		float taut = dragged ? Mathf.Clamp((spanRatio - 1f) / 3f, 0f, 1f) : 0f;

		float archRatio = dragged
			? Mathf.Lerp(ArchRatio, DragArchFactor, taut)
			: Mathf.Clamp(ArchRatio * spanRatio, ArchRatio, MaxArchRatio);

		float arch = cardHeightCanvas * archRatio / rootScale;

		// 顶端留白保护：三次贝塞尔在 t = 0.5 处的实际峰值只有 0.75 × arch，
		// 别让长跨距把拱顶顶出屏幕外。
		float topLocalY = (inverseRoot * Vector2.Zero).Y;
		float archMax = (Mathf.Min(from.Y, to.Y) - topLocalY - (ArchTopMargin / rootScale)) / 0.75f;
		arch = Mathf.Min(arch, archMax);
		arch = Mathf.Max(arch, cardHeightCanvas * 0.06f / rootScale);

		float thicknessScale = dragged ? Mathf.Lerp(1f, StretchThicknessMin, taut) : 1f;
		float half = cardHeightCanvas * RibbonHalfThicknessRatio * thicknessScale * ribbon.Weight;

		// 控制点抬到两张牌上方（Godot 的 y 向下，所以「向上拱」= y 取更小的值）
		float topY = Mathf.Min(from.Y, to.Y) - arch;
		Vector2 c1 = new(Mathf.Lerp(from.X, to.X, 1f / 3f), topY);
		Vector2 c2 = new(Mathf.Lerp(from.X, to.X, 2f / 3f), topY);

		FillBand(ribbon.Core, from, c1, c2, to, half);
		FillBand(ribbon.Halo, from, c1, c2, to, half * HaloScale);

		float alpha = ribbon.Weight;
		ribbon.Core.Node.Modulate = new Color(1f, 1f, 1f, alpha);
		ribbon.Halo.Node.Modulate = new Color(1f, 1f, 1f, alpha);

		UpdateSparks(ribbon, from, c1, c2, to, cardHeightCanvas / rootScale, delta);
	}

	/// <summary>这条光带的端点里，有没有「正在被拖拽的那张牌」。</summary>
	private static bool IsDragged(Ribbon ribbon)
	{
		NHandCardHolder? dragged = _activeDragged;
		if (dragged == null || !GodotObject.IsInstanceValid(dragged))
		{
			return false;
		}

		ulong id = dragged.GetInstanceId();
		return (ribbon.Left != null && ribbon.Left.GetInstanceId() == id)
			|| (ribbon.Right != null && ribbon.Right.GetInstanceId() == id);
	}

	/// <summary>一张手牌 holder 的「顶边中点」在连线层本地坐标下的位置。</summary>
	private static Vector2 TopCenter(NHandCardHolder holder, Transform2D inverseRoot)
	{
		// ⚠️ 手牌 holder 的「原点」就是卡牌中心，不能再加 size * 0.5 的偏移：
		//    hand_card_holder.tscn 的根 Control 没有尺寸（Size = 0），Hitbox / Flash / card.tscn
		//    的子节点全部围绕原点居中。所以「顶边中点」= 原点沿局部 −y 半个卡高。
		Vector2 size = GetCardSize(holder);
		Transform2D toCanvas = holder.GetGlobalTransformWithCanvas();
		return inverseRoot * (toCanvas * new Vector2(0f, -size.Y * 0.5f));
	}

	/// <summary>把一条曲线采样成三角条带，写进顶点缓冲并重传网格。</summary>
	private static void FillBand(Band band, Vector2 from, Vector2 c1, Vector2 c2, Vector2 to, float half)
	{
		int pointCount = RibbonSegments + 1;
		for (int i = 0; i < pointCount; i++)
		{
			float t = (float)i / RibbonSegments;
			Vector2 point = Bezier(from, c1, c2, to, t);
			Vector2 tangent = BezierTangent(from, c1, c2, to, t);
			Vector2 normal = new(-tangent.Y, tangent.X);
			normal = normal.LengthSquared() > 0.00000001f ? normal.Normalized() : Vector2.Down;
			Vector2 h = normal * half;

			band.Verts[i * 2] = new Vector3(point.X + h.X, point.Y + h.Y, 0f);
			band.Verts[i * 2 + 1] = new Vector3(point.X - h.X, point.Y - h.Y, 0f);
		}

		band.Arrays[(int)Mesh.ArrayType.Vertex] = band.Verts;
		band.Mesh.ClearSurfaces();
		band.Mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, band.Arrays);
	}

	// ──────────────────────────────────────────────────────────────────────
	// 贴合浮动粒子
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 按原版 <c>card_connected_pati1</c> 的贴图建一簇「贴合光带」的粒子。
	///
	/// <para>与旧实现的区别：位移<b>只沿曲线法线方向</b>（即沿带的上下），幅度很小，
	/// 相位均匀铺开 ⇒ 任意时刻线的上下两侧都有粒子；贴近线时更亮更大。</para>
	/// </summary>
	private static void BuildSparks(Ribbon ribbon)
	{
		if (_particleTexture == null)
		{
			return;
		}

		int count = ParticlesPerRibbon;
		ribbon.Sparks = new Sprite2D[count];
		ribbon.SparkT = new float[count];
		ribbon.SparkPhase = new float[count];
		ribbon.SparkBob = new float[count];
		ribbon.SparkJitter = new float[count];

		for (int i = 0; i < count; i++)
		{
			Sprite2D sprite = new()
			{
				Texture = _particleTexture,
				Material = _additiveMaterial,
				Centered = true,
				Visible = ParticlesEnabled,
			};
			ribbon.Group.AddChild(sprite, forceReadableName: false, Node.InternalMode.Disabled);

			ribbon.Sparks[i] = sprite;
			// t 均匀铺满整条带（另加一点随机，避免排成一条直线）
			ribbon.SparkT[i] = ((float)i / count) + GD.Randf() * 0.05f;
			ribbon.SparkPhase[i] = GD.Randf() * Mathf.Tau;
			ribbon.SparkBob[i] = 0.7f + GD.Randf() * 0.6f;
			ribbon.SparkJitter[i] = (GD.Randf() - 0.5f) * 1.2f;
		}
	}

	private static void UpdateSparks(Ribbon ribbon, Vector2 from, Vector2 c1, Vector2 c2, Vector2 to,
		float cardHeightLocal, float delta)
	{
		int count = ribbon.Sparks.Length;
		if (count == 0)
		{
			return;
		}

		float amp = cardHeightLocal * ParticleAmpRatio;
		float baseScale = cardHeightLocal * ParticleSizeRatio / ParticleTextureSize.Y;

		for (int i = 0; i < count; i++)
		{
			// 沿带缓慢漂移（PosMod 保证无缝循环）
			float t = Mathf.PosMod(ribbon.SparkT[i] + ParticleAdvance * delta, 1f);
			ribbon.SparkT[i] = t;
			ribbon.SparkPhase[i] += ParticleBobSpeed * ribbon.SparkBob[i] * delta;

			float phase = ribbon.SparkPhase[i];
			Vector2 point = Bezier(from, c1, c2, to, t);
			Vector2 tangent = BezierTangent(from, c1, c2, to, t);
			tangent = tangent.LengthSquared() > 0.00000001f ? tangent.Normalized() : Vector2.Right;
			Vector2 normal = new(-tangent.Y, tangent.X);

			// 核心：位移只沿法线（上下），幅度小；另加一点点沿带的固定错位
			float offNormal = Mathf.Sin(phase) * amp;
			float offAlong = ribbon.SparkJitter[i] * amp * 1.5f;
			float depth = Mathf.Cos(phase);

			Sprite2D sprite = ribbon.Sparks[i];
			sprite.Position = point + (normal * offNormal) + (tangent * offAlong);

			// 越贴近线越亮越大，远离时收小变暗 → 紧贴感
			float near = 1f - (Mathf.Abs(Mathf.Sin(phase)) * 0.5f);
			float s = baseScale * near;
			sprite.Scale = new Vector2(s, s);
			sprite.Rotation = phase * 0.5f;

			// 沿带两端淡入淡出（不完全淡没，免得看着像断了）+ 权重
			float fade = 0.4f + (0.6f * Mathf.Sin(Mathf.Pi * t));
			float alpha = fade * (0.45f + (0.55f * near)) * (depth < 0f ? 0.8f : 1f) * ribbon.Weight;
			sprite.Modulate = new Color(0.58f, 0.92f, 1.0f, alpha);

			// 前半圈压在带之上，后半圈藏在带之下
			sprite.ZIndex = depth > 0f ? 1 : -1;
		}
	}

	/// <summary>按当前设置套一次粒子（「额外粒子」关掉时不再显示）。</summary>
	private static void ApplyParticleToggle(bool enabled)
	{
		foreach (KeyValuePair<(ulong Left, ulong Right), Ribbon> pair in _ribbons)
		{
			foreach (Sprite2D sprite in pair.Value.Sparks)
			{
				if (GodotObject.IsInstanceValid(sprite))
				{
					sprite.Visible = enabled;
				}
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────
	// 尺寸
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 取一张手牌 holder 的<b>卡牌局部尺寸</b>。
	///
	/// <para>⚠️ <c>holder.Size</c> 恒为 <c>(0,0)</c> —— <c>hand_card_holder.tscn</c> 的根 Control
	/// 没有定义尺寸，游戏自己读的都是 <c>Hitbox.Size</c>（<c>300×422</c>，正好是卡牌可视尺寸）。
	/// 这里逐级兜底：<c>holder.Size</c> → <c>Hitbox.Size</c> → <c>CardNode.Size</c> → 原版设计尺寸。</para>
	/// </summary>
	private static Vector2 GetCardSize(NHandCardHolder holder)
	{
		Vector2 size = holder.Size;
		if (IsUsableSize(size))
		{
			return size;
		}

		NClickableControl? hitbox = holder.Hitbox;
		if (hitbox != null && GodotObject.IsInstanceValid(hitbox))
		{
			size = hitbox.Size;
			if (IsUsableSize(size))
			{
				return size;
			}
		}

		NCard? cardNode = holder.CardNode;
		if (cardNode != null && GodotObject.IsInstanceValid(cardNode))
		{
			size = cardNode.Size;
			if (IsUsableSize(size))
			{
				return size;
			}
		}

		if (!_loggedFallbackSize)
		{
			_loggedFallbackSize = true;
			HeidemarieIntegration.Log.Warn(
				"连接视觉层：holder.Size / Hitbox.Size / CardNode.Size 全部为空，"
					+ "回退到原版设计尺寸 490x704。");
		}

		return new Vector2(490f, ReferenceCardHeight);
	}

	/// <summary>尺寸是否可用（非零、非负、没有 NaN）。</summary>
	private static bool IsUsableSize(Vector2 size)
	{
		return size.X > 1f && size.Y > 1f && float.IsFinite(size.X) && float.IsFinite(size.Y);
	}

	/// <summary>卡牌在 <b>canvas 坐标</b>下的显示尺寸（= 局部尺寸 × 手牌缩放）。</summary>
	private static Vector2 GetCanvasSize(NHandCardHolder holder)
	{
		Vector2 size = GetCardSize(holder);
		Vector2 scale = holder.GetGlobalTransformWithCanvas().Scale;
		return new Vector2(Mathf.Abs(size.X * scale.X), Mathf.Abs(size.Y * scale.Y));
	}

	// ──────────────────────────────────────────────────────────────────────
	// 曲线
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>三次贝塞尔求值（与着色器里的 <c>cubic_bezier</c> 同算法）。</summary>
	private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float mt = 1f - t;
		return (mt * mt * mt * p0)
			+ (3f * mt * mt * t * p1)
			+ (3f * mt * t * t * p2)
			+ (t * t * t * p3);
	}

	/// <summary>三次贝塞尔切线（与着色器里的 <c>cubic_bezier_tangent</c> 同算法）。</summary>
	private static Vector2 BezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float mt = 1f - t;
		return (3f * mt * mt * (p1 - p0))
			+ (6f * mt * t * (p2 - p1))
			+ (3f * t * t * (p3 - p2));
	}

	// ──────────────────────────────────────────────────────────────────────
	// 资源 / 节点管理
	// ──────────────────────────────────────────────────────────────────────

	private static void EnsureResources()
	{
		if (_shader != null)
		{
			return;
		}

		_shader = ResourceLoader.Load<Shader>(ShaderPath);
		_ribbonTexture = ResourceLoader.Load<Texture2D>(RibbonTexturePath);
		_particleTexture = ResourceLoader.Load<Texture2D>(ParticleTexturePath);

		_additiveMaterial = new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
		};

		if (_shader == null)
		{
			HeidemarieIntegration.Log.Warn($"连接光带着色器缺失：{ShaderPath}");
		}

		if (_ribbonTexture == null)
		{
			HeidemarieIntegration.Log.Warn($"连接光带贴图缺失：{RibbonTexturePath}");
		}
	}

	private static void ClearAll()
	{
		foreach (KeyValuePair<(ulong Left, ulong Right), Ribbon> pair in _ribbons)
		{
			pair.Value.Dispose();
		}

		_ribbons.Clear();
		_desired.Clear();
		_graveyard.Clear();
		_chain.Clear();
		_ordered.Clear();
		_inContainer.Clear();
		_activeDragged = null;
	}

	private static void SetVisible(bool visible)
	{
		foreach (KeyValuePair<(ulong Left, ulong Right), Ribbon> pair in _ribbons)
		{
			if (GodotObject.IsInstanceValid(pair.Value.Group))
			{
				pair.Value.Group.Visible = visible;
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────
	// 设置开关
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 有没有「整屏 UI」盖在战斗界面之上（抽牌堆 / 弃牌堆 / 消耗堆 / 地图 / 各种浮层）。
	///
	/// <para>⚠️ 本视觉层是 <see cref="NPlayerHand"/> 的子节点（z = <see cref="OverlayZIndex"/>），
	/// 和战斗 UI、手牌处在<b>同一块画布</b>里。整屏 UI 走的是独立画布层，
	/// z 序跟它比不出结果，只要不主动让位，光带就会画在它们上面 —— 就是「穿模」。</para>
	///
	/// <para>判据沿用游戏自己的那套（<c>NOverlayStack.StackIsCovered</c> /
	/// <c>NPlayerHand.OnHolderPressed</c>）：兜底容器在用时把光带整个收掉；
	/// 整屏 UI 关掉后下一帧自动重建，观感无缝。</para>
	/// </summary>
	private static bool ScreenCovered
	{
		get
		{
			try
			{
				if (NCapstoneContainer.Instance is { InUse: true })
				{
					return true;
				}

				if (NMapScreen.Instance is { IsOpen: true })
				{
					return true;
				}

				if (NOverlayStack.Instance is { ScreenCount: > 0 })
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				// 判定本身不该把战斗弄崩：读不到就当没盖住
				if (!_loggedCoverCheck)
				{
					_loggedCoverCheck = true;
					HeidemarieIntegration.Log.Warn("连接视觉层：整屏 UI 判定失败：" + ex.Message);
				}
			}

			return false;
		}
	}

	/// <summary>总开关：本功能的「显示连接动画」+ 角色「战斗特效」总开关，两个都要开。</summary>
	private static bool EffectsEnabled
	{
		get
		{
			try
			{
				return HeidemarieLinkOverlaySettings.Enabled
					&& HeidemarieSettings.CurrentCharacter.EffectiveEffectsEnabled;
			}
			catch (Exception ex)
			{
				HeidemarieIntegration.Log.Warn("读取连接视觉层开关失败：" + ex.Message);
				return false;
			}
		}
	}

	/// <summary>粒子开关：跟随设置里的「额外粒子」（与卡牌拖尾粒子同一口径）。</summary>
	private static bool ParticlesEnabled
	{
		get
		{
			try
			{
				return HeidemariePresentationHost.Gates.CombatParticlesEnabled;
			}
			catch (Exception ex)
			{
				HeidemarieIntegration.Log.Warn("读取连接粒子开关失败：" + ex.Message);
				return true;
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────
	// 补丁
	// ──────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 手牌节点就绪时挂载。选这个时机而不是战斗房间 <c>_Ready</c>：
	/// 手牌自身就绪时它的坐标系已经可用，不必再猜 <c>NPlayerHand.Instance</c> 有没有赋值
	/// （实际上那时它还真没赋值，所以 <see cref="Attach"/> 会把实例存下来）。
	/// </summary>
	[HarmonyPatch(typeof(NPlayerHand), "_Ready")]
	private static class PlayerHandReadyPatch
	{
		private static void Postfix(NPlayerHand __instance) => Attach(__instance);
	}

	/// <summary>
	/// 兜底挂载点。<see cref="NCombatUi"/> 的 <c>Hand</c> 正是在它自己的 <c>_Ready</c> 里被赋值的，
	/// 所以这里是「能确定拿到手牌实例」的最早时机。两个补丁都调 <see cref="Attach"/>（幂等），
	/// 只要其中任一生效，连线就会挂上。
	/// </summary>
	[HarmonyPatch(typeof(NCombatUi), "_Ready")]
	private static class CombatUiReadyPatch
	{
		private static void Postfix(NCombatUi __instance)
		{
			if (__instance.Hand != null)
			{
				Attach(__instance.Hand);
			}
		}
	}

	/// <summary>
	/// 记录「哪张牌开始被拖了」以及它的槽位索引。
	///
	/// <para>这个补丁<b>只读状态、不改行为</b>：不调用任何原逻辑、不返回值，
	/// 对其它角色同样只是记一笔，且只有 <see cref="HeidemarieCard"/> 会被画上连线（零侵入）。</para>
	///
	/// <para>⚠️ 必须前置：<c>StartCardPlay</c> 内部紧接着就 <c>holder.Reparent(this)</c>
	/// 把它移出 <c>CardHolderContainer</c>，之后再问 <c>GetIndex()</c> 就不是手牌槽位了。</para>
	/// </summary>
	[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
	private static class StartCardPlayPatch
	{
		private static void Prefix(NHandCardHolder holder) => OnDragStarted(holder);
	}
}
