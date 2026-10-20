using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「互帮互助」：0 费技能（公共牌），目标为<b>队友</b>，<b>多人模式专用</b>。
///
/// <para>队友抽 <c>Cards</c> 张牌；再从<strong>队友的抽牌堆</strong>选择 1 张，
/// 在你的手牌生成它的复制体。</para>
///
/// <para><b>单机过滤（设计 Q6 = A）</b>：通过
/// <see cref="CardModel.MultiplayerConstraint"/> 标成 <c>MultiplayerOnly</c>。
/// 官方在卡池解锁（<c>CardPoolModel.GetUnlockedCards</c>）、商店、奖励、
/// 图鉴的「显示多人牌」勾选里都按这个枚举过滤，因此单人局不会刷出本牌。</para>
///
/// <para>复制体用 <c>ICardScope.CloneCard</c>（官方 <c>DualWield</c> 那条路），
/// 它保留原牌的升级档与其他运行期状态，比手工 <c>ToMutable</c> 稳妥。</para>
/// </summary>
public sealed class HeidemarieMutualAid : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(2),
	};

	/// <summary>0 费、技能、公共牌、目标为任意队友。</summary>
	public HeidemarieMutualAid()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.AnyPlayer)
	{
	}

	/// <inheritdoc/>
	/// <remarks>多人专用：单人局不进卡池 / 商店 / 奖励。</remarks>
	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		Player? ally = cardPlay.Target.Player;
		if (ally == null || ally == Owner || CombatState is not { } combatState)
		{
			return;
		}

		// 先让队友抽牌，再选牌 —— 抽到的牌也应该在可选范围内
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, ally);

		CardPile drawPile = PileType.Draw.GetPile(ally);
		if (drawPile.Cards.Count == 0)
		{
			return;
		}

		CardModel? picked = (await CardSelectCmd.FromCombatPile(
			choiceContext,
			drawPile,
			ally,
			new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();

		if (picked == null)
		{
			return;
		}

		CardModel copy = combatState.CloneCard(picked);
		await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
