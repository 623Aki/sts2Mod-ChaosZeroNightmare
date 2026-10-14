using System;
using System.Collections.Generic;
using Godot;

namespace CznHeidemarie.Integration;

/// <summary>
/// 「特效亮度 / 特效不透明度」两个设置项的接入层。
///
/// <para><b>为什么不用 CznCore 的 <c>CombatEffectMaterialRuntime</c></b>：
/// 那套实现会给所有 SpineSprite 换成 <b>预乘 alpha</b> 材质（配合 CZN 原版
/// 预乘贴图）。本工程的骨架贴图是普通 alpha（见
/// <see cref="HeidemarieBattleReadyRuntime"/> 里对材质替换的说明），
/// 强行替换会让特效发黑 / 发白。所以这里只做它两者共有的、
/// 与素材无关的那一半：<b>给特效子树写 modulate</b>。</para>
///
/// <para>只改「最外层 CanvasItem」的 modulate，靠 Godot 的父子继承往下传 ——
/// 逐层都写会变成亮度三次方（0.8³=0.51），观感完全不是滑条上的数值。</para>
///
/// <para>应用点：出牌特写的剑影层、单体技能特效舞台、全屏演出舞台。</para>
/// </summary>
internal static class HeidemarieEffectMaterials
{
	/// <summary>已经套过 modulate 的根节点，设置变更时重新套一遍。</summary>
	private static readonly List<Node> Applied = new();

	/// <summary>把当前的「特效亮度 / 不透明度」套到一棵特效子树上。</summary>
	/// <param name="root">特效子树的根（舞台节点或特效层节点）。</param>
	public static void Apply(Node? root)
	{
		if (root is null || !GodotObject.IsInstanceValid(root))
		{
			return;
		}

		PruneApplied();

		Color factor = CurrentFactor();
		if (root is CanvasItem rootItem)
		{
			rootItem.Modulate = factor;
		}
		else
		{
			// 根不是 CanvasItem：退一格，给直接子节点各写一次
			foreach (Node child in root.GetChildren())
			{
				if (child is CanvasItem item)
				{
					item.Modulate = factor;
				}
			}
		}

		if (!Applied.Contains(root))
		{
			Applied.Add(root);
		}
	}

	/// <summary>
	/// 设置面板改了亮度 / 不透明度后调用（挂到
	/// <c>SkinSettingsUiRuntimeHooks.ApplyEffectBrightnessSettings</c>）：
	/// 对当前还在画面上的特效重新套一遍。
	/// </summary>
	public static void Refresh()
	{
		try
		{
			PruneApplied();
			Color factor = CurrentFactor();
			foreach (Node node in Applied)
			{
				if (node is CanvasItem item)
				{
					item.Modulate = factor;
				}
				else
				{
					foreach (Node child in node.GetChildren())
					{
						if (child is CanvasItem childItem)
						{
							childItem.Modulate = factor;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("特效亮度刷新失败：" + ex.Message);
		}
	}

	/// <summary>清空登记表（换房 / 战斗结束时调用，避免持有已释放节点）。</summary>
	public static void Clear() => Applied.Clear();

	private static Color CurrentFactor()
	{
		// 特效通道整体关掉时不参与运算，交由各自的开关处理（这里只调明暗）
		float brightness = Mathf.Clamp(HeidemarieSettings.CurrentCharacter.EffectBrightness, 0f, 2f);
		float opacity = Mathf.Clamp(HeidemarieSettings.CurrentCharacter.EffectOpacity, 0f, 1f);
		return new Color(brightness, brightness, brightness, opacity);
	}

	private static void PruneApplied()
	{
		for (int i = Applied.Count - 1; i >= 0; i--)
		{
			if (!GodotObject.IsInstanceValid(Applied[i]))
			{
				Applied.RemoveAt(i);
			}
		}
	}
}
