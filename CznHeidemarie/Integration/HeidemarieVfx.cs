using System;
using System.Collections.Concurrent;
using CznCore.Godot;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace CznHeidemarie.Integration;

/// <summary>
/// 本 mod 的 VFX 工具类，对应教程 3-3 的 <c>VFXUtil</c>。
///
/// <para><b>为什么需要它：</b>游戏的 <c>VfxCmd</c> 只认 <c>vfx/...</c> 本体路径；
/// mod 自己的 <c>res://CznHeidemarie/...</c> 场景要走 <c>WithHitVfxNode</c> 或本类。
/// 同时 mod 场景不能依赖 <c>PreloadManager</c> 的缓存（换场景会被清），
/// 所以这里维护一份独立的 <see cref="PackedScene"/> 缓存。</para>
///
/// <para><b>挂载规则（与官方 <c>VfxCmd.PlayVfx</c> 一致）：</b>
/// 优先挂 <c>creature.GetVfxContainer()</c>（战斗中是 <c>CombatVfxContainer</c>，
/// 图鉴里是图鉴容器），用世界坐标定位；不要再挂到敌人骨架节点下。</para>
/// </summary>
internal static class HeidemarieVfx
{
	/// <summary>mod 独立场景缓存：只缓存 PackedScene，不缓存节点。</summary>
	private static readonly ConcurrentDictionary<string, PackedScene> SceneCache = new(StringComparer.Ordinal);

	/// <summary>
	/// 实例化一个 mod 特效场景。缓存命中时直接 Instantiate；
	/// 场景根不是 <see cref="Node2D"/> 时返回 <c>null</c> 并释放实例。
	/// </summary>
	internal static Node2D? GenVFXNode(string scenePath)
	{
		PackedScene? scene = GetOrLoadScene(scenePath);
		if (scene is null)
		{
			return null;
		}

		Node? node = scene.Instantiate(PackedScene.GenEditState.Disabled);
		if (node is Node2D node2D)
		{
			return node2D;
		}

		node?.QueueFreeSafely();
		return null;
	}

	/// <summary>泛型版本：场景根是自定义 <see cref="Node2D"/> 子类时使用。</summary>
	internal static T? GenVFXNode<T>(string scenePath) where T : Node2D
	{
		PackedScene? scene = GetOrLoadScene(scenePath);
		return scene?.Instantiate<T>(PackedScene.GenEditState.Disabled);
	}

	/// <summary>进战斗时同步预载舞台场景，避免第一次出牌现加载。</summary>
	internal static void PreloadScene(string scenePath)
	{
		_ = GetOrLoadScene(scenePath);
	}

	/// <summary>
	/// 挂到指定单位的前台特效容器，并按世界坐标定位。
	/// 这是本工程「目标特效」的唯一推荐入口。
	/// </summary>
	/// <param name="creature">特效归属的战斗单位。</param>
	/// <param name="scenePath">mod 场景的完整 <c>res://</c> 路径。</param>
	/// <param name="anchor">锚点语义（中心 / 脚底 / 脚底+屏幕偏移 / 骨架原点）。</param>
	/// <param name="screenOffset">仅 <see cref="HeidemarieVfxAnchor.FloorScreenOffset"/> 使用，屏幕像素。</param>
	/// <param name="behind">挂后台容器（会随镜头震动；命中特效不要用）。</param>
	internal static Node2D? SpawnOnCreature(
		Creature creature,
		string scenePath,
		HeidemarieVfxAnchor anchor,
		Vector2 screenOffset = default,
		bool behind = false)
	{
		if (TestMode.IsOn || creature.IsDead)
		{
			return null;
		}

		NCreature? creatureNode = creature.GetCreatureNode();
		if (!GodotNodeUtil.IsAlive(creatureNode))
		{
			return null;
		}

		Control? container = behind ? creature.GetBackVfxContainer() : creature.GetVfxContainer();
		if (!GodotNodeUtil.IsAlive(container))
		{
			return null;
		}

		Node2D? root = GenVFXNode(scenePath);
		if (root is null)
		{
			return null;
		}

		// 先挂再设世界变换：容器 origin/scale 恰好是 0/1 时「先设」也能对，
		// 但先挂更稳，不依赖容器当前变换。
		try
		{
			container.AddChild(root, forceReadableName: false, Node.InternalMode.Disabled);
		}
		catch (Exception)
		{
			Free(root);
			return null;
		}

		try
		{
			ApplyCreatureAnchor(root, creatureNode, anchor, screenOffset);

			Node2D? body = creatureNode.Body;
			if (!GodotNodeUtil.IsAlive(body))
			{
				// Body 缺失时退到 Visuals 根，至少保住位置/朝向/缩放。
				body = creatureNode.Visuals;
			}

			if (GodotNodeUtil.IsAlive(body))
			{
				// 前后关系不靠 z 差：战斗画布里背景与角色同在 -10，
				// 再减 1 会掉到背景之后（整层看不见）。改由容器子树顺序决定：
				// 后台容器排在 BgContainer 之后、Ally/EnemyContainer 之前。
				root.ZAsRelative = false;
				root.ZIndex = Math.Clamp(EffectiveZ(body), -4096, 4096);
			}
		}
		catch (Exception)
		{
			// 任何一个锚点/变换步骤失败都不留下半成品节点。
			Free(root);
			return null;
		}

		return root;
	}

	/// <summary>
	/// 把已有特效根节点重新对到单位的当前锚点。用于命中帧（敌人可能已抖动/位移）。
	/// 只改根节点变换，不动图层；图层 offset 本来就是相对根节点标定的。
	/// </summary>
	internal static void ApplyCreatureAnchor(
		Node2D root,
		NCreature creature,
		HeidemarieVfxAnchor anchor,
		Vector2 screenOffset = default)
	{
		if (!GodotNodeUtil.IsAlive(root) || !GodotNodeUtil.IsAlive(creature))
		{
			return;
		}

		Vector2 position = ResolveAnchor(creature, anchor, screenOffset);
		Node2D? body = creature.Body;
		if (!GodotNodeUtil.IsAlive(body))
		{
			body = creature.Visuals;
		}

		if (GodotNodeUtil.IsAlive(body))
		{
			// 复刻 Body 的全局基（缩放 / 镜像 / 旋转 / 相机缩放），只替换原点。
			// 这是「与旧骨架挂载逐像素一致」的关键：图层的局部 offset 仍乘同一套基。
			Transform2D bodyGlobal = body.GetGlobalTransform();
			root.GlobalTransform = new Transform2D(bodyGlobal.X, bodyGlobal.Y, position);
		}
		else
		{
			root.GlobalPosition = position;
		}
	}

	/// <summary>通用入口：挂到任意父节点并设世界坐标（非战斗 UI/图鉴等）。</summary>
	internal static Node2D? SpawnAt(string scenePath, Node parent, Vector2 globalPosition)
	{
		if (TestMode.IsOn || !GodotNodeUtil.IsAlive(parent))
		{
			return null;
		}

		Node2D? root = GenVFXNode(scenePath);
		if (root is null)
		{
			return null;
		}

		parent.AddChild(root, forceReadableName: false, Node.InternalMode.Disabled);
		root.GlobalPosition = globalPosition;
		return root;
	}

	/// <summary>
	/// 教程 <c>VFXUtil.PlaySimple</c> 的等价实现：挂前台容器、到点释放。
	/// </summary>
	internal static Node2D? PlaySimple(string scenePath, Vector2 position, float lifetime = 2f)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (TestMode.IsOn || !GodotNodeUtil.IsAlive(room) || !GodotNodeUtil.IsAlive(room.CombatVfxContainer))
		{
			return null;
		}

		Node2D? node = SpawnAt(scenePath, room.CombatVfxContainer, position);
		if (node is null)
		{
			return null;
		}

		SceneTreeTimer timer = node.GetTree().CreateTimer(Math.Max(0.01f, lifetime));
		timer.Timeout += () => Free(node);
		return node;
	}

	/// <summary>
	/// 给根节点接「播完释放」：优先本工程舞台的 <c>finished</c> 信号，
	/// 其次场景里 <see cref="AnimationPlayer"/> 的 <c>AnimationFinished</c>。
	/// 释放整个根节点，不保留子节点。
	/// </summary>
	internal static void FreeWhenFinished(Node root)
	{
		if (!GodotNodeUtil.IsAlive(root))
		{
			return;
		}

		if (root.HasSignal("finished"))
		{
			root.Connect(
				"finished",
				Callable.From(() => Free(root)),
				(uint)GodotObject.ConnectFlags.OneShot);
			return;
		}

		foreach (Node child in root.GetChildren())
		{
			if (child is not AnimationPlayer player
				|| !player.HasSignal(AnimationMixer.SignalName.AnimationFinished))
			{
				continue;
			}

			player.Connect(
				AnimationMixer.SignalName.AnimationFinished,
				Callable.From<StringName>(_ => Free(root)),
				(uint)GodotObject.ConnectFlags.OneShot);
			return;
		}
	}

	/// <summary>幂等释放：已经失效的节点直接忽略。</summary>
	internal static void Free(Node? root)
	{
		if (GodotNodeUtil.IsAlive(root))
		{
			root.QueueFreeSafely();
		}
	}

	/// <summary>沿父链累加 z_index，遇到 z_as_relative=false 停止；与 gd 侧算法一致。</summary>
	internal static int EffectiveZ(CanvasItem? item)
	{
		int z = 0;
		CanvasItem? current = item;
		while (GodotNodeUtil.IsAlive(current))
		{
			z += current.ZIndex;
			if (!current.ZAsRelative)
			{
				break;
			}

			current = current.GetParent() as CanvasItem;
		}

		return z;
	}

	private static Vector2 ResolveAnchor(NCreature creature, HeidemarieVfxAnchor anchor, Vector2 screenOffset)
	{
		return anchor switch
		{
			HeidemarieVfxAnchor.Center => creature.VfxSpawnPosition,
			HeidemarieVfxAnchor.Floor => creature.GetBottomOfHitbox(),
			HeidemarieVfxAnchor.FloorScreenOffset => creature.GetBottomOfHitbox() + screenOffset,
			HeidemarieVfxAnchor.BodyOrigin => creature.Body is { } body ? body.GlobalPosition : creature.GlobalPosition,
			_ => creature.VfxSpawnPosition,
		};
	}

	private static PackedScene? GetOrLoadScene(string scenePath)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
		{
			return null;
		}

		if (SceneCache.TryGetValue(scenePath, out PackedScene? cached) && GodotObject.IsInstanceValid(cached))
		{
			return cached;
		}

		PackedScene? scene = ResourceLoader.Load<PackedScene>(scenePath, null, ResourceLoader.CacheMode.Reuse);
		if (scene is null)
		{
			return null;
		}

		SceneCache[scenePath] = scene;
		return scene;
	}
}
