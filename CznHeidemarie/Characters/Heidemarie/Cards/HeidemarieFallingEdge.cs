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
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「落刃」：1 费攻击（罕见牌）。
///
/// <para>造成 <c>Damage</c> 点伤害；随后丢弃手牌中最多 <c>Cards</c> 张牌，
/// 每弃 1 张抽 1 张。官方 <c>Acrobatics</c> 的角色化改写：攻击 + 弃牌 + 抽牌三合一。</para>
///
/// <para><b>刻意不带「连接」</b>：带连接的话，基类的连带丢弃会在出牌前把手牌清空，
/// 这张牌的「选择丢弃」就没有可选范围了。</para>
/// </summary>
public sealed class HeidemarieFallingEdge : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(8m, ValueProp.Move),
		// 最多可弃 / 可抽的张数（升级 +1）
		new CardsVar(2),
	};

	/// <summary>1 费、攻击、罕见牌、目标为任意敌人。</summary>
	public HeidemarieFallingEdge()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			// Uncommon 1 费：中·纵劈档（Attack2），比轻档多一记纵劈
			.WithAttackerAnim(HeidemarieAttackPlan.Heavy.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Heavy))
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);

		// 伤害结算完再弃牌：先弃会打乱攻击演出，也会让可选范围少一张。
		if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
		{
			return;
		}

		// MinSelect = 0：允许一张都不弃（`RequireManualConfirmation` 会自动开启确认按钮）。
		List<CardModel> discarded = (await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
			null,
			this)).ToList();

		if (discarded.Count > 0)
		{
			// 批量弃牌必须一次性给出：循环调用单张版会破坏 Sly（狡黠）的自动打出时机。
			await CardCmd.Discard(choiceContext, discarded);
			await CardPileCmd.Draw(choiceContext, discarded.Count, Owner);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
