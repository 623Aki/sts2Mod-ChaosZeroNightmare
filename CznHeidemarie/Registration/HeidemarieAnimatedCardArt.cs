using System;
using CznHeidemarie.Characters.Heidemarie.Cards;
using CznHeidemarie.Integration;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace CznHeidemarie.Registration;

/// <summary>
/// 本角色「古代牌」的<b>动态卡面</b>：把 CZN 的 Spine 骨架渲染成实时贴图，塞给卡牌的
/// <c>%AncientPortrait</c>。
///
/// <para>做法与参考 mod 的 <c>NineAnimatedCardArt</c>（「超究武神霸斩」）一致：
/// 卡面场景 <c>heidemarie_card_art.tscn</c> 是一个挂在 <c>SceneTree.Root</c> 下的普通 Node，
/// 内部按「卡面档案」建 <c>SubViewport</c> + <c>SpineSprite</c>（一个档案一个 viewport），
/// 循环播放骨架动画，并把 viewport 的贴图交给注册进来的那张卡的立绘节点。</para>
///
/// <para><b>卡牌 → 档案的映射</b>写在 <see cref="ResolveProfile"/>：
/// 目前两张古代牌各用一个档案 ——「解放极光」单骨架、
/// 「剑之乐园」三张 ux 插画骨架组装（见 <c>heidemarie_card_art.gd</c> 的 <c>PROFILES</c>）。</para>
///
/// <para><b>为什么挂在 <c>NCard.UpdatePortrait</c> 上</b>：这个私有方法才是把
/// <c>Model.Portrait</c> 写进 <c>%AncientPortrait</c> 的地方，作为 Postfix 接在它后面，
/// 才能保证我们的实时贴图是「最后写入」的那个。参数 <c>____ancientPortrait</c>
/// 是 Harmony 的私有字段注入（四个下划线 = 私有字段），不必用反射。</para>
///
/// <para>卡牌节点是池化复用的，所以换回别的牌时要主动摘掉；<c>OnFreedToPool</c>
/// 再兜一道，避免渲染器一直持有已经回收的节点。</para>
/// </summary>
[HarmonyPatch]
internal static class HeidemarieAnimatedCardArt
{
	/// <summary>卡面渲染器场景（<c>Node</c> + 按档案建 <c>SubViewport</c> + <c>SpineSprite</c>）。</summary>
	private const string ScenePath = "res://CznHeidemarie/scenes/cards/heidemarie_card_art.tscn";

	/// <summary>全局唯一一份渲染器（所有卡面共用同一个实例，内部按档案分 viewport）。</summary>
	private static Node? _renderer;

	/// <summary>刷完立绘后，把动态卡面接上（或摘掉）。</summary>
	[HarmonyPatch(typeof(NCard), "UpdatePortrait")]
	[HarmonyPostfix]
	private static void UpdatePortrait(NCard __instance, TextureRect ____ancientPortrait)
	{
		try
		{
			if (____ancientPortrait is null)
			{
				return;
			}

			string? profile = ResolveProfile(__instance.Model);
			if (profile is not null)
			{
				EnsureRenderer()?.Call("register_portrait", ____ancientPortrait, profile);
			}
			else if (GodotObject.IsInstanceValid(_renderer))
			{
				_renderer!.Call("unregister_portrait", ____ancientPortrait);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("动态卡面注册失败：" + ex.Message);
		}
	}

	/// <summary>卡牌节点被回收时摘掉，别拿着已经不在树里的 TextureRect。</summary>
	[HarmonyPatch(typeof(NCard), "OnFreedToPool")]
	[HarmonyPrefix]
	private static void Release(TextureRect ____ancientPortrait)
	{
		try
		{
			if (GodotObject.IsInstanceValid(_renderer) && ____ancientPortrait is not null)
			{
				_renderer!.Call("unregister_portrait", ____ancientPortrait);
			}
		}
		catch (Exception ex)
		{
			HeidemarieIntegration.Log.Warn("动态卡面注销失败：" + ex.Message);
		}
	}

	/// <summary>
	/// 卡牌类型 → 卡面档案 id。
	///
	/// <para>档案 id 必须与 <c>heidemarie_card_art.gd</c> 的 <c>PROFILES</c> 键逐字一致，
	/// 否则渲染器只会 <c>push_warning</c> 并放弃接管（卡面回落到静态兜底图）。</para>
	/// </summary>
	private static string? ResolveProfile(CardModel? model) => model switch
	{
		HeidemarieLiberatedAurora => "liberated_aurora",
		HeidemarieSwordParadise => "sword_paradise",
		HeidemarieTerminus => "terminus",
		_ => null,
	};

	/// <summary>取渲染器；第一次用到时创建并挂到场景树根上。</summary>
	private static Node? EnsureRenderer()
	{
		if (GodotObject.IsInstanceValid(_renderer))
		{
			return _renderer;
		}

		if (Engine.GetMainLoop() is not SceneTree tree
			|| ResourceLoader.Load<PackedScene>(ScenePath, null, ResourceLoader.CacheMode.Reuse) is not { } scene)
		{
			return null;
		}

		_renderer = scene.Instantiate(PackedScene.GenEditState.Disabled);
		tree.Root.AddChild(_renderer, forceReadableName: false, Node.InternalMode.Disabled);
		HeidemarieIntegration.Log.Info("动态卡面渲染器已创建：" + ScenePath);
		return _renderer;
	}
}
