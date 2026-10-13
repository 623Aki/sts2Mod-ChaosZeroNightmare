using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace CznHeidemarie.Registration;

/// <summary>
/// 关键字「唯一」的判定与过滤工具。
///
/// <para><b>范围</b>：以「当前卡组」为准 —— 卡组里已有同 Id 的另一张，即视为已持有。
/// 战斗内生成的临时副本不计入（见 <c>HeidemarieCard.CanBeGeneratedInCombat</c>：
/// 唯一牌本来就不会被战斗内随机生成）。</para>
/// </summary>
internal static class HeidemarieUniqueRule
{
	/// <summary>这张牌是否带「唯一」。</summary>
	internal static bool IsUnique(CardModel? card) => card is IUniqueCard;

	/// <summary>卡组里是否已有同 Id 的另一张（<paramref name="except"/> 为自身实例，不算）。</summary>
	internal static bool DeckContainsCopy(Player player, ModelId id, CardModel? except)
	{
		foreach (CardModel card in player.Deck.Cards)
		{
			if (card != except && card.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>玩家卡组里是否已经持有这张「唯一」牌。</summary>
	internal static bool OwnsInDeck(Player? player, CardModel? candidate)
	{
		if (player == null || candidate == null || !IsUnique(candidate))
		{
			return false;
		}

		return DeckContainsCopy(player, candidate.Id, null);
	}

	/// <summary>从候选集合里剔除「玩家已持有」的唯一牌（保留原有顺序）。</summary>
	internal static IEnumerable<CardModel> ExcludeOwned(IEnumerable<CardModel> cards, Player? player)
	{
		if (player == null)
		{
			return cards;
		}

		return cards.Where(card => !OwnsInDeck(player, card)).ToList();
	}
}

/// <summary>
/// 唯一牌 · 奖励 / 事件 / 遗物加牌入口。
///
/// <para><c>CardFactory</c> 的奖励生成统一走
/// <see cref="CardCreationOptions.GetPossibleCards"/>，在此剔除「已经持有的唯一牌」，
/// 保证它压根不会被作为候选显示给玩家（而不是显示了却不给）。</para>
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards))]
internal static class HeidemarieUniqueRewardPatch
{
	private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
	{
		__result = HeidemarieUniqueRule.ExcludeOwned(__result, player);
	}
}

/// <summary>
/// 唯一牌 · 商店卡池。
///
/// <para>商人不走 <c>CardCreationOptions</c>，而是直接拿原始候选列表
/// （<c>CardFactory.CreateForMerchant</c> 里调用
/// <see cref="Hook.ModifyMerchantCardPool"/>），因此单独在这里过滤。</para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantCardPool))]
internal static class HeidemarieUniqueMerchantPatch
{
	private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
	{
		__result = HeidemarieUniqueRule.ExcludeOwned(__result, player);
	}
}

/// <summary>
/// 唯一牌 · 变形候选。
///
/// <para>把「已持有的唯一牌」从变形目标里剔除，既避免变出第二张，
/// 也避免下游的加牌去重把替换牌丢掉造成牌数减少。</para>
/// </summary>
[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDefaultTransformationOptions))]
internal static class HeidemarieUniqueTransformPatch
{
	private static void Postfix(CardModel original, ref IEnumerable<CardModel> __result)
	{
		__result = HeidemarieUniqueRule.ExcludeOwned(__result, original.Owner);
	}
}

/// <summary>
/// 唯一牌 · 加牌去重（安全网）。
///
/// <para>任何「把牌加进卡组」的动作最终都会走
/// <c>CardPileCmd.Add(cards, newPile, ...)</c>。这里对目标为卡组的批次做一次兜底：
/// 已持有同 Id 的唯一牌时直接丢掉（并留一条警告），
/// 覆盖那些不走 <c>CardCreationOptions</c> 的「复制 / 直接塞牌」效果。</para>
///
/// <para>用「自身实例不算重复」判断，因此「同一张牌在牌堆间移动」不会被误伤；
/// 变形（先移除原牌再加入替换牌）也不会被误伤。</para>
///
/// <para><b>为什么不用 <c>[HarmonyPatch]</c> 属性自动挂载</b>：这是本组补丁里唯一
/// 「<c>ref Task&lt;…&gt; __result</c> + 异步方法 + 需按参数类型精确指定重载」三件事叠加的补丁，
/// 一旦挂载失败，<c>Harmony.PatchAll()</c> 会整体抛错、把其它补丁一起带走。
/// 因此改为在 <see cref="Install"/> 里单独挂载，失败只告警。</para>
/// </summary>
internal static class HeidemarieUniqueDeckGuard
{
	/// <summary>单独安装加牌去重补丁；失败不抛出，只记录警告。</summary>
	internal static void Install(Harmony harmony, MegaCrit.Sts2.Core.Logging.Logger log)
	{
		try
		{
			System.Reflection.MethodBase? original = AccessTools.Method(
				typeof(CardPileCmd),
				nameof(CardPileCmd.Add),
				new[]
				{
					typeof(IEnumerable<CardModel>),
					typeof(CardPile),
					typeof(CardPilePosition),
					typeof(AbstractModel),
					typeof(bool),
					typeof(bool),
				});

			System.Reflection.MethodInfo? prefix = AccessTools.Method(typeof(HeidemarieUniqueDeckGuard), nameof(Prefix));

			if (original == null || prefix == null)
			{
				log.Warn("唯一牌加牌去重补丁：未找到目标方法，已跳过（其余拦截仍然生效）。");
				return;
			}

			harmony.Patch(original, prefix: new HarmonyMethod(prefix));
		}
		catch (System.Exception ex)
		{
			log.Warn("唯一牌加牌去重补丁挂载失败，已跳过（其余拦截仍然生效）：" + ex.Message);
		}
	}

	private static bool Prefix(
		ref IEnumerable<CardModel> cards,
		CardPile newPile,
		ref Task<IReadOnlyList<CardPileAddResult>> __result)
	{
		if (newPile.Type != PileType.Deck)
		{
			return true;
		}

		List<CardModel> list = cards as List<CardModel> ?? cards.ToList();
		if (list.Count == 0)
		{
			return true;
		}

		HashSet<ModelId>? seenInBatch = null;
		List<CardModel> blocked = new();

		list.RemoveAll(card =>
		{
			if (!HeidemarieUniqueRule.IsUnique(card) || card.Owner == null)
			{
				return false;
			}

			bool alreadyInDeck = HeidemarieUniqueRule.DeckContainsCopy(card.Owner, card.Id, card);
			seenInBatch ??= new HashSet<ModelId>();
			bool repeatedInBatch = !seenInBatch.Add(card.Id);

			if (!alreadyInDeck && !repeatedInBatch)
			{
				return false;
			}

			blocked.Add(card);
			return true;
		});

		if (blocked.Count == 0)
		{
			return true;
		}

		GD.PushWarning($"[Heidemarie] 唯一牌去重：拦截了 {blocked.Count} 张重复的唯一牌。");

		if (list.Count > 0)
		{
			// 同一批里还有别的牌要加：只把重复的唯一牌摘掉，其余交回原实现
			cards = list;
			return true;
		}

		// 整批都被拦下：直接给出「失败」结果。
		// 不能返回空数组 —— 单张入口 CardPileCmd.Add(card, ...) 会对结果取 [0]，空数组会抛越界。
		List<CardPileAddResult> failures = new(blocked.Count);
		foreach (CardModel card in blocked)
		{
			failures.Add(new CardPileAddResult
			{
				success = false,
				cardAdded = card,
				oldPile = card.Pile,
				targetPile = newPile.Type,
				modifyingModels = null,
			});
		}

		__result = Task.FromResult<IReadOnlyList<CardPileAddResult>>(failures);
		return false;
	}
}

/// <summary>
/// 唯一牌 · 重放赋魔拦截。
///
/// <para>唯一牌「不能附加重放和重复类似的效果」：即使被赋了重放赋魔，
/// <see cref="CardModel.GetEnchantedReplayCount"/> 也恒返回 0，
/// 这样卡面提示与结算都不会出现重放。</para>
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetEnchantedReplayCount))]
internal static class HeidemarieUniqueReplayEnchantPatch
{
	private static void Postfix(CardModel __instance, ref int __result)
	{
		if (HeidemarieUniqueRule.IsUnique(__instance))
		{
			__result = 0;
		}
	}
}

/// <summary>
/// 唯一牌 · 重放结算拦截。
///
/// <para>其他遗物 / 能力（如「本回合下一张牌打出两次」）会通过
/// <see cref="Hook.ModifyCardPlayCount"/> 抬高结算次数。唯一牌在这里被强制压回 1，
/// 只会结算一次。</para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardPlayCount))]
internal static class HeidemarieUniqueReplayCountPatch
{
	private static void Postfix(CardModel card, ref int __result)
	{
		if (HeidemarieUniqueRule.IsUnique(card) && __result > 1)
		{
			__result = 1;
		}
	}
}

/// <summary>
/// 唯一牌 · 重放消耗拦截。
///
/// <para><see cref="Hook.AfterModifyingCardPlayCount"/> 是「抬高次数的那些模型各自扣层」的时机
/// （例如「下一张牌打出两次」的能力在这里把自己消耗掉）。
/// 唯一牌跳过后处理，于是这一次加成<strong>不会被消耗</strong>，
/// 原样留给下一张真正能被打出两次的牌。</para>
///
/// <para><b>必须回填 <c>__result</c></b>：原方法是 <c>async Task</c>，跳过实现后
/// Harmony 只能返回该返回类型的默认值 —— <c>Task</c> 的默认值是 <c>null</c>，
/// 调用方（<c>CardModel.GeneratePlayCount</c>）里的 <c>await</c> 会立刻抛
/// <c>NullReferenceException</c>，表现为「打出唯一牌时报空引用」。
/// 所以这里显式给一个已完成的任务。</para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingCardPlayCount))]
internal static class HeidemarieUniqueReplayConsumePatch
{
	private static bool Prefix(CardModel card, ref Task __result)
	{
		if (!HeidemarieUniqueRule.IsUnique(card))
		{
			// 非唯一牌：走原实现
			return true;
		}

		// 唯一牌：跳过原实现（不执行任何模型的 AfterModifyingCardPlayCount），
		// 并回填 __result，避免调用方 await 到 null。
		__result = Task.CompletedTask;
		return false;
	}
}
