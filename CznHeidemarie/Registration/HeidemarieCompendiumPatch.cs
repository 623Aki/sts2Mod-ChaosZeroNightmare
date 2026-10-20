using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CznHeidemarie.Characters.Heidemarie;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace CznHeidemarie.Registration;

/// <summary>
/// 把本角色的<strong>衍生牌池</strong>挂进游戏的共享牌池列表。
///
/// <para><c>ModelDb.AllCardPools = AllCharacterCardPools + AllSharedCardPools</c>，
/// 而 <c>ModelDb.AllCards</c> 又是从 <c>AllCardPools</c> 展平来的。
/// 「极光剑 / 凝聚极光 / 解放极光」刻意不进角色池（否则奖励与商店会刷出来），
/// 于是它们从 <c>AllCards</c> 里消失了，表现为控制台 <c>card</c> 指令
/// 报 <c>Card 'HEIDEMARIE_CONDENSED_AURORA' not found</c>、
/// 百科大全里也看不到这三张牌。</para>
///
/// <para>原版的衍生牌（飞刀 / 主权之刃等）正是放在 <c>TokenCardPool</c> 这个
/// 「共享池」里的，这里照同一思路加一个本角色的衍生牌池。</para>
/// </summary>
[HarmonyPatch(typeof(ModelDb), "get_AllSharedCardPools")]
internal static class HeidemarieTokenPoolPatch
{
	private static void Postfix(ref IEnumerable<CardPoolModel> __result)
	{
		__result = __result
			.Append(ModelDb.CardPool<HeidemarieTokenPool>())
			.Distinct();
	}
}

/// <summary>
/// 兜底：保证衍生牌一定出现在 <c>ModelDb.AllCards</c> 里。
///
/// <para><c>AllCardPools</c> / <c>AllCards</c> 都是「首次访问时缓存」的
/// （<c>_allCards ??= …</c>）。万一缓存是在本补丁生效之前建立的，
/// 上面那条补丁就不会体现在结果里。这条 Postfix 直接在取值时补上，
/// 与缓存状态无关，因此控制台的 <c>card</c> 指令总能找到这三张牌。</para>
/// </summary>
[HarmonyPatch(typeof(ModelDb), "get_AllCards")]
internal static class HeidemarieAllCardsPatch
{
	private static void Postfix(ref IEnumerable<CardModel> __result)
	{
		__result = __result.Concat(HeidemarieTokenCards).Distinct();
	}

	/// <summary>本角色的衍生牌（与 <see cref="HeidemarieTokenPool"/> 保持一致）。</summary>
	private static IEnumerable<CardModel> HeidemarieTokenCards => new CardModel[]
	{
		ModelDb.Card<HeidemarieAuroraSword>(),
		ModelDb.Card<HeidemarieCondensedAurora>(),
		ModelDb.Card<HeidemarieLiberatedAurora>(),
		ModelDb.Card<HeidemarieSwordParadise>(),
		// 第 2 轮：状态牌「骇人的记忆」
		ModelDb.Card<HeidemarieHorrificMemory>(),
	};
}

/// <summary>
/// 百科大全（卡牌图鉴）里补一个本角色的牌池筛选按钮。
///
/// <para>原版图鉴的池筛选按钮是<strong>写死在场景里</strong>的八个
/// （铁甲 / 静默 / 缺陷 / 君权 / 骸骨 / 无色 / 远古 / 其他），
/// 并且默认选中「铁甲战士」，因此 mod 角色的卡牌永远筛不出来。</para>
///
/// <para>做法：直接实例化官方那个筛选按钮场景
/// （<c>res://scenes/screens/card_library/library_pool_toggle.tscn</c>）挂到同一个容器里，
/// 再把「筛选谓词」和「角色 → 按钮」两张表（<c>NCardLibrary</c> 的私有字段）补上，
/// 这样原版的单选 / 过滤流程照常生效，不需要改动游戏的任何资源。</para>
/// </summary>
[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class HeidemarieCardLibraryFilterPatch
{
	/// <summary>筛选按钮的场景（原版角色池按钮用的就是它）。</summary>
	private const string ToggleScenePath = "res://scenes/screens/card_library/library_pool_toggle.tscn";

	/// <summary>本角色头像，用作筛选按钮图标。</summary>
	private const string CharacterIconPath = "res://images/ui/top_panel/character_icon_heidemarie.png";

	/// <summary>新增按钮的节点名（用于幂等判断）。</summary>
	private const string FilterNodeName = "HeidemariePool";

	private static readonly FieldInfo? PoolFiltersField =
		AccessTools.Field(typeof(NCardLibrary), "_poolFilters");

	private static readonly FieldInfo? CardPoolFiltersField =
		AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters");

	private static readonly MethodInfo? UpdateCardPoolFilterMethod =
		AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter");

	private static void Postfix(NCardLibrary __instance)
	{
		try
		{
			Inject(__instance);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Heidemarie] 卡牌图鉴角色筛选注入失败：{ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void Inject(NCardLibrary library)
	{
		// 以原版第一个角色按钮作为「容器锚点」：新按钮挂在它旁边
		NCardPoolFilter? anchor = library.GetNodeOrNull<NCardPoolFilter>("%IroncladPool");
		Node? container = anchor?.GetParent();
		if (anchor == null || container == null)
		{
			return;
		}

		// 角色 → 按钮表：哪怕按钮没建出来也要先登记，
		// 否则以本角色开局后打开图鉴会在 OnSubmenuOpened 里抛 KeyNotFoundException。
		if (CardPoolFiltersField?.GetValue(library)
			is Dictionary<CharacterModel, NCardPoolFilter> cardPoolFilters)
		{
			cardPoolFilters.TryAdd(ModelDb.Character<Heidemarie>(), anchor);
		}

		if (container.HasNode(FilterNodeName))
		{
			return;
		}

		if (GD.Load<PackedScene>(ToggleScenePath) is not { } toggleScene)
		{
			return;
		}

		NCardPoolFilter filter = toggleScene.Instantiate<NCardPoolFilter>();
		filter.Name = FilterNodeName;

		// 换成角色头像（场景自带的材质是 resource_local_to_scene，实例之间互不影响）
		if (GD.Load<Texture2D>(CharacterIconPath) is { } icon)
		{
			if (filter.GetNodeOrNull<TextureRect>("Image") is { } image)
			{
				image.Texture = icon;
			}

			if (filter.GetNodeOrNull<TextureRect>("Image/Shadow") is { } shadow)
			{
				shadow.Texture = icon;
			}
		}

		container.AddChild(filter);

		// 「池筛选谓词」表：本角色的卡（含衍生牌，它们的 Pool 也指向角色池）
		if (PoolFiltersField?.GetValue(library)
			is Dictionary<NCardPoolFilter, Func<CardModel, bool>> poolFilters)
		{
			poolFilters[filter] = static card => card.Pool is HeidemarieCardPool;
		}

		// 角色 → 按钮表：覆盖成新建的按钮，这样以本角色开局时默认就选中它
		if (CardPoolFiltersField?.GetValue(library)
			is Dictionary<CharacterModel, NCardPoolFilter> cardPoolFiltersAgain)
		{
			cardPoolFiltersAgain[ModelDb.Character<Heidemarie>()] = filter;
		}

		// 点击时复用原版的「单选 + 刷新」逻辑
		if (UpdateCardPoolFilterMethod != null)
		{
			filter.Connect(
				NCardPoolFilter.SignalName.Toggled,
				Callable.From<NCardPoolFilter>(toggled =>
					UpdateCardPoolFilterMethod.Invoke(library, new object[] { toggled })));
		}
	}
}
