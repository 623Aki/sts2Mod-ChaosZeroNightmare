using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Registration;

/// <summary>
/// 「安息」锁墓 · 移堆兜底。
///
/// <para>「守墓」只需要在洗牌时把自己从列表里剔除，而「安息」要求更硬：
/// 本回合内这张牌<strong>不能被以任何方式移出弃牌堆</strong>。
/// 引擎没有可取消的移堆钩子（<c>AfterCardChangedPiles</c> 是事后通知），
/// 因此这里对 <see cref="CardPileCmd.Add"/> 的批量入口做统一拦截：</para>
/// <list type="bullet">
///   <item>目标堆仍是弃牌堆（同堆重排 / 调整位置）→ 放行；</item>
///   <item>处于 <see cref="HeidemarieRestLock.Bypass"/> 作用域（「坠星」）→ 放行；</item>
///   <item>战斗不在进行中 / 正在收尾 → 放行，避免战斗结束的清理被卡住；</item>
///   <item>其余情况：把被锁的牌从本批里摘掉；整批都被摘掉时返回「失败」结果
///   （不能返回空数组 —— 单张入口会对结果取 <c>[0]</c>）。</item>
/// </list>
///
/// <para>各取墓牌自己的候选列表仍应优先过滤（见 <see cref="HeidemarieRecover"/>），
/// 让玩家压根看不到拿不走的牌；本补丁只是兜底。</para>
///
/// <para><b>为什么单独挂载</b>：与「唯一牌加牌去重」同一原因 —— 需要按参数类型精确指定
/// 重载，并改写异步返回值；失败时不应牵连其它补丁。</para>
/// </summary>
internal static class HeidemarieRestLockGuard
{
	/// <summary>单独安装锁墓兜底补丁；失败只告警。</summary>
	internal static void Install(Harmony harmony, Logger log)
	{
		try
		{
			MethodBase? original = AccessTools.Method(
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

			MethodInfo? prefix = AccessTools.Method(typeof(HeidemarieRestLockGuard), nameof(Prefix));

			if (original == null || prefix == null)
			{
				log.Warn("安息锁墓补丁：未找到目标方法，已跳过。");
				return;
			}

			harmony.Patch(original, prefix: new HarmonyMethod(prefix));
		}
		catch (Exception ex)
		{
			log.Warn("安息锁墓补丁挂载失败，已跳过：" + ex.Message);
		}
	}

	private static bool Prefix(
		ref IEnumerable<CardModel> cards,
		CardPile newPile,
		ref Task<IReadOnlyList<CardPileAddResult>> __result)
	{
		// 同堆移动（弃牌堆内部重排 / 调整位置）不算「移出墓地」
		if (newPile.Type == PileType.Discard)
		{
			return true;
		}

		// 只枚举一次：后续判定与筛选都用这份快照
		List<CardModel> list = cards.ToList();
		if (list.Count == 0)
		{
			return true;
		}

		// 「坠星」这类刻意绕过的作用域按牌主判定（见 HeidemarieRestLock.Bypass）
		if (HeidemarieRestLock.IsBypassedFor(list[0]?.Owner))
		{
			return true;
		}

		// 战斗外 / 收尾阶段放行：此时牌要回牌组、要清理战斗牌堆，不应被锁墓挡住
		if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
		{
			return true;
		}

		List<CardModel> blocked = list.Where(card => HeidemarieRestLock.IsLocked(card)).ToList();
		if (blocked.Count == 0)
		{
			return true;
		}

		list.RemoveAll(card => HeidemarieRestLock.IsLocked(card));

		if (list.Count > 0)
		{
			// 同一批里还有别的牌要移动：只摘掉被锁的，其余交回原实现
			cards = list;
			return true;
		}

		// 整批都被拦下：显式给出「失败」结果
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
