using System;
using System.Collections.Generic;
using Godot;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「额外粒子」设置项的接入层。
///
/// <para>本工程的「额外粒子」就是卡牌拖尾场景
/// （<c>res://scenes/vfx/card_trail_heidemarie.tscn</c>）里的
/// <c>Sprites/BigSparks</c> 与 <c>Sprites/LittleSparks</c> 两个 <c>CPUParticles2D</c>
/// —— 它们纯属观感加成，关掉不影响任何战斗表现，正是面板上那句
/// 「显示额外粒子特效。默认开启；低配电脑可关闭以降低性能开销」。</para>
///
/// <para>拖尾是游戏自己实例化的（<c>NCardTrailVfx.Create</c>），所以靠
/// <c>Registration/HeidemarieCombatPresentationPatch.cs</c> 里那个 Postfix
/// 在创建瞬间套一次；设置中途变更时由 <see cref="Refresh"/> 对已经在场上的拖尾重套。</para>
/// </summary>
internal static class HeidemarieTrailParticles
{
	/// <summary>拖尾场景里两个粒子节点的名字（取自 <c>card_trail_heidemarie.tscn</c>）。</summary>
	private static readonly HashSet<string> ParticleNodeNames = new(StringComparer.Ordinal)
	{
		"BigSparks",
		"LittleSparks",
	};

	/// <summary>已经套过的拖尾根节点，设置变更时重新套。</summary>
	private static readonly List<Node> Trails = new();

	/// <summary>当前设置下额外粒子应不应该发射。</summary>
	internal static bool Enabled
	{
		get
		{
			try
			{
				return HeidemariePresentationHost.Gates.CombatParticlesEnabled;
			}
			catch (Exception ex)
			{
				HeidemarieIntegration.Log.Warn("读取额外粒子设置失败：" + ex.Message);
				return true;
			}
		}
	}

	/// <summary>给一条刚建出来的拖尾套上当前设置。</summary>
	/// <param name="trailRoot">拖尾场景根节点（<c>NCardTrailVfx</c>）。</param>
	internal static void ApplyToTrail(Node? trailRoot)
	{
		if (trailRoot is null || !GodotObject.IsInstanceValid(trailRoot))
		{
			return;
		}

		try
		{
			PruneTrails();
			Toggle(trailRoot, Enabled);
			if (!Trails.Contains(trailRoot))
			{
				Trails.Add(trailRoot);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("额外粒子开关失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 设置面板里改了「额外粒子」后调用（挂到
	/// <c>SkinSettingsUiRuntimeHooks.CleanupSkinVisualEffectsRuntime</c>）：
	/// 对已经在场上的拖尾重套一遍。
	/// </summary>
	internal static void Refresh()
	{
		try
		{
			PruneTrails();
			bool enabled = Enabled;
			foreach (Node trail in Trails)
			{
				Toggle(trail, enabled);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("额外粒子刷新失败：" + ex.Message);
		}
	}

	/// <summary>清空登记表（换房 / 战斗结束时调用）。</summary>
	internal static void Clear() => Trails.Clear();

	/// <summary>把一棵子树里所有拖尾粒子节点置为 <paramref name="enabled"/>。</summary>
	private static void Toggle(Node root, bool enabled)
	{
		if (ParticleNodeNames.Contains(root.Name))
		{
			switch (root)
			{
				case CpuParticles2D cpu:
					cpu.Emitting = enabled;
					cpu.Visible = enabled;
					break;
				case GpuParticles2D gpu:
					gpu.Emitting = enabled;
					gpu.Visible = enabled;
					break;
			}
		}

		foreach (Node child in root.GetChildren())
		{
			Toggle(child, enabled);
		}
	}

	private static void PruneTrails()
	{
		for (int i = Trails.Count - 1; i >= 0; i--)
		{
			if (!GodotObject.IsInstanceValid(Trails[i]))
			{
				Trails.RemoveAt(i);
			}
		}
	}
}
