using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「砺锋」：1 费技能（公共牌）。
///
/// <para>丢弃最多 2 张牌，每丢弃 1 张，本回合内<b>极光剑</b>的伤害提高
/// <c>Percent</c>%。</para>
///
/// <para><b>增伤通道复用「极夜」（设计 Q13 = A）</b>：给
/// <see cref="HeidemarieNightPower"/> 叠加 <c>Percent × 张数</c> 层。
/// 两者语义完全一致（本回合 / 极光剑 / 百分比相加），复用同一个池就不会出现
/// 「两个图标描述同一件事」的歧义，也自动共享「白昼 + 极夜相加」的既有协调逻辑
/// （见 <see cref="HeidemarieAuroraDamagePoolPower"/>）。</para>
/// </summary>
public sealed class HeidemarieWhetEdge : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		// 每弃 1 张提供的增伤百分点
		new DynamicVar("Percent", 60m),
		// 最多可弃的张数
		new CardsVar(2),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieWhetEdge()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
		{
			return;
		}

		// MinSelect = 0：可以一张都不弃，此时不加增伤
		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
			null,
			this)).ToList();

		if (discarded.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, discarded);

		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Percent"].BaseValue * discarded.Count,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
