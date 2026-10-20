using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace CznMonster.Content.Vfx;

/// <summary>
/// 波勒米卡「处决 / 换防」的三层全屏特效（设计文档 §5.5 / §1.2.1 ③）。
///
/// <para><b>素材</b>：<c>effect/1001013_fatal_eff_b</c>（z -600）、<c>_eff_f</c>（z 3）、
/// <c>_sub</c>（z 5，动画 <c>normal_fatal</c>）。已用
/// <c>SpineSkeletonDataConverter -v 4.2.11</c> 转到 4.2 并生成 <c>_skel_data.tres</c>。</para>
///
/// <para><b>为什么拆成两个场景</b>：STS2 的 <c>z_index</c> 只在同一父节点内排序，
/// 而 <c>CombatVfxContainer</c> 整体位于角色之上。要让背景层落在「背景之上、角色之下」，
/// 只能挂 <c>BackCombatVfxContainer</c>；前景两层挂 <c>CombatVfxContainer</c> 并保留
/// .cfx 的相对顺序（Sub 5 &gt; EffF 3）。</para>
///
/// <para><b>编排</b>：STS2 没有 CZN 的自由镜头系统，所以 <c>camera_path/*.json</c>
/// 与 <c>1001013_fatal_node.json</c> 只作为时序参考，不逐帧复刻；
/// 本次实现是「三层同时起播 + 4.4 s 后自动释放」。镜头抖动交给
/// <c>NGame.ScreenShake</c>（由伤害结算自动触发）。</para>
///
/// <para><b>标定</b>：三个常量（<see cref="BackScale"/> / <see cref="FrontScale"/> /
/// 两个 offset）是留给 CZN_AnimLab / 实机微调的；当前值按
/// <c>polemica.tscn</c> 的 SpineSprite 标定（Boss 包围盒高 556 px、scale 0.95）取 1.0，
/// 即「特效骨架单位 ≈ 屏幕像素」。</para>
///
/// <para><b>注意</b>：Boss 视觉已缩到 scale 0.85（包围盒高 497.5），本特效相对 Boss
/// 会比标定时大约 10%；实机若显得过大，把 <see cref="BackScale"/> /
/// <see cref="FrontScale"/> 调到 0.895 即可，offset 不需要动。</para>
/// </summary>
internal static class PolemicaFatalVfx
{
	/// <summary>背景层场景（EffB，挂 <c>BackCombatVfxContainer</c>）。</summary>
	private const string BackScenePath = "res://scenes/vfx/polemica_fatal_b.tscn";

	/// <summary>前景层场景（EffF + Sub，挂 <c>CombatVfxContainer</c>）。</summary>
	private const string FrontScenePath = "res://scenes/vfx/polemica_fatal_f.tscn";

	/// <summary>三层里最长的 timeline 是 4.167 s，留 0.2 s 余量。</summary>
	private const double LifetimeSeconds = 4.4;

	// ── 标定常量（素材坐标 → 战斗屏幕坐标）──
	private const float BackScale = 1f;

	private const float FrontScale = 1f;

	private static readonly Vector2 BackOffset = Vector2.Zero;

	private static readonly Vector2 FrontOffset = Vector2.Zero;

	/// <summary>每个图层自己的动画名（与素材骨架一致）。</summary>
	private static readonly Dictionary<string, string> LayerAnimations = new(StringComparer.Ordinal)
	{
		["EffB"] = "animation",
		["EffF"] = "animation",
		["Sub"] = "normal_fatal",
	};

	/// <summary>正在播放的根节点，用于「新一次处决顶掉上一次」与离房清理。</summary>
	private static readonly List<Node2D> Active = new();

	private static bool _missingSceneWarned;

	/// <summary>
	/// 播一次三层处决特效。<paramref name="anchor"/> 是定位基准（通常是首领）；
	/// 取不到节点时退回敌人侧中心。
	/// </summary>
	public static void Play(Creature anchor)
	{
		try
		{
			if (TestMode.IsOn)
			{
				return;
			}

			NCombatRoom? room = NCombatRoom.Instance;
			if (room == null || room.CombatVfxContainer == null)
			{
				return;
			}

			// 同一时间只留一套，避免换防连发时叠三份全屏特效
			Clear();

			Vector2 position = anchor.GetCreatureNode()?.VfxSpawnPosition
				?? room.CombatVfxContainer.GlobalPosition;

			Spawn(BackScenePath, room.BackCombatVfxContainer, position + BackOffset, BackScale);
			Spawn(FrontScenePath, room.CombatVfxContainer, position + FrontOffset, FrontScale);
		}
		catch (Exception ex)
		{
			MainFile.Log.Warn($"CznMonster 处决特效播放失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>立即释放全部在播特效（换房 / 战斗结束时可调用）。</summary>
	public static void Clear()
	{
		foreach (Node2D root in Active)
		{
			if (GodotObject.IsInstanceValid(root))
			{
				root.QueueFree();
			}
		}
		Active.Clear();
	}

	private static void Spawn(string scenePath, Control? container, Vector2 globalPosition, float scale)
	{
		if (container == null)
		{
			return;
		}

		PackedScene? scene = ResourceLoader.Load<PackedScene>(scenePath);
		if (scene == null)
		{
			if (!_missingSceneWarned)
			{
				_missingSceneWarned = true;
				MainFile.Log.Warn($"CznMonster 处决特效场景缺失：{scenePath}");
			}
			return;
		}

		Node2D root = scene.Instantiate<Node2D>();
		container.AddChild(root);
		root.GlobalPosition = globalPosition;
		root.Scale = Vector2.One * scale;
		Active.Add(root);

		// 骨架是异步加载的：等 SpineSprite 真正就绪再设动画（GDExtension 边界安全）
		foreach (Node child in root.GetChildren())
		{
			if (child.GetClass() != "SpineSprite")
			{
				continue;
			}

			string animation = LayerAnimations.TryGetValue(child.Name.ToString(), out string? found)
				? found
				: "animation";
			Node spineNode = child;
			root.RunWhenSpineReady(new MegaSprite(spineNode), state =>
			{
				state.SetAnimation(animation, loop: false);
			});
		}

		SceneTree? tree = root.GetTree();
		if (tree == null)
		{
			return;
		}
		tree.CreateTimer(LifetimeSeconds).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(root))
			{
				root.QueueFree();
			}
			Active.Remove(root);
		};
	}
}
