using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「内在觉醒」：2 费技能（稀有牌），关键字<b>安息</b>。
///
/// <para>丢弃手牌中的 1 张牌（<b>由玩家选择</b>）；随机抽取 1 张攻击牌入手，
/// 并在本回合内赋予它<b>连接</b>。</para>
///
/// <para><b>「丢弃1」是手牌自选</b>：卡面语义要求玩家自己决定牺牲哪张，
/// 因此弹选择界面（与「星落」那种「抽牌堆随机弃」的牌刻意区分开）。</para>
///
/// <para>安息与打出效果<strong>完全一致</strong>：被连接牌连带丢弃时，
/// 相当于「弃 1 换 1 张连接攻击牌」，是本角色的手牌循环零件。</para>
/// </summary>
public sealed class HeidemarieInnerAwakening : HeidemarieCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		// 抽取的攻击牌张数
		new CardsVar(1),
		// 从手牌丢弃的张数
		new DynamicVar("Discard", 1m),
	};

	/// <summary>2 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieInnerAwakening()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Awaken(choiceContext);

	/// <inheritdoc/>
	/// <remarks>关键字「安息」：被丢弃时发动同一效果。</remarks>
	public Task OnRest(PlayerChoiceContext choiceContext) => Awaken(choiceContext);

	/// <summary>打出与安息共用的效果。</summary>
	private async Task Awaken(PlayerChoiceContext choiceContext)
	{
		// ① 先从手牌里选 1 张丢掉（本牌打出时已离开手牌，这里数到的是「其余手牌」）
		int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
		int want = DynamicVars["Discard"].IntValue;

		if (handCount > 0 && want > 0)
		{
			// 手牌不足时按实际张数要（MinSelect 不能大于可选数）
			int max = Math.Min(want, handCount);

			List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
				choiceContext,
				Owner,
				new CardSelectorPrefs(SelectionScreenPrompt, max, max),
				null,
				this)).ToList();

			if (discarded.Count > 0)
			{
				// 批量弃牌：禁止循环单张（Sly 时机）
				await CardCmd.Discard(choiceContext, discarded);
			}
		}

		// ② 从抽牌堆随机抽攻击牌入手
		List<CardModel> attacks = PileType.Draw.GetPile(Owner).Cards
			.Where(c => c.Type == CardType.Attack)
			.ToList();

		for (int i = 0; i < DynamicVars.Cards.IntValue; i++)
		{
			CardModel? attack = Owner.RunState.Rng.CombatCardSelection.NextItem(attacks);
			if (attack == null)
			{
				break;
			}

			attacks.Remove(attack);
			await CardPileCmd.Add(attack, PileType.Hand);

			// 本回合内赋予「连接」（卡面没写"本场战斗"，走本回合口径）
			if (attack is HeidemarieCard holder)
			{
				holder.GrantRuntimeLinkThisTurn();
			}
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
