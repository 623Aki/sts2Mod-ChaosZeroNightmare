using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「极光之光」。
///
/// <para>当层数达到 <see cref="Threshold"/> 层时：</para>
/// <list type="number">
///   <item>将「解放极光」加入手牌；</item>
///   <item>移除<strong>全部战斗牌堆</strong>中的「凝聚极光」；</item>
///   <item>移除<strong>所有</strong>「极光之光」（含自身）。</item>
/// </list>
///
/// <para>层数增长点是「凝聚极光进入墓地」时（见 <c>HeidemarieCondensedAurora</c>），
/// 由卡牌自己判定是否达标并调用 <see cref="TriggerAsync"/>。</para>
/// </summary>
public sealed class HeidemarieAuroraLightPower : PowerModel
{
	/// <summary>触发阈值：层数达到该值即发动。</summary>
	public const int Threshold = 3;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <summary>
	/// 层数达标时执行收束：解放极光入手 → 清掉场上凝聚极光 → 清掉所有极光之光。
	/// </summary>
	/// <param name="choiceContext">上下文。</param>
	internal async Task TriggerAsync(PlayerChoiceContext choiceContext)
	{
		Player? player = Owner.Player;
		ICombatState? combatState = Owner.CombatState;
		if (player == null || combatState == null)
		{
			return;
		}

		// 1) 解放极光加入手牌（唯一获取渠道）
		CardModel liberated = combatState.CreateCard<HeidemarieLiberatedAurora>(player);
		await CardPileCmd.AddGeneratedCardToCombat(liberated, PileType.Hand, player);

		// 2) 移除全部战斗牌堆里的「凝聚极光」
		List<CardModel> condensed = new();
		if (player.PlayerCombatState is { } combatCards)
		{
			foreach (CardPile pile in combatCards.AllPiles)
			{
				condensed.AddRange(pile.Cards.Where(c => c is HeidemarieCondensedAurora));
			}
		}

		if (condensed.Count > 0)
		{
			await CardPileCmd.RemoveFromCombat(condensed);
		}

		// 3) 移除所有「极光之光」（含自身）
		foreach (HeidemarieAuroraLightPower power in Owner.GetPowerInstances<HeidemarieAuroraLightPower>().ToList())
		{
			await PowerCmd.Remove(power);
		}
	}
}
