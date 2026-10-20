using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「倒挂的天选者」：2 费攻击（稀有牌），目标为任意敌人。
///
/// <para>造成 <c>Damage</c> 点伤害；若手牌中有带「连接」的牌，
/// <b>弃掉全部</b>连接牌 —— 只要有弃到，伤害次数 +1（基础 1 次）。</para>
///
/// <para><b>弃全部</b>：连接牌在手牌里堆的越多，兑付的价值越大
/// —— 于是它同时是「连接牌的一次性兑付出口」：攒够再打。</para>
///
/// <para>本牌<b>不带</b>「连接」，所以不会触发基类
/// <c>BeforeCardPlayed</c> 里的连带丢弃 —— 它自己是「吃连接牌」的出口，
/// 与「葬光」「守望」构成同一类弃牌消费端。</para>
/// </summary>
public sealed class HeidemarieHangedChosen : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(15m, ValueProp.Move),
	};

	/// <summary>2 费、攻击、稀有牌、指向任意敌人。</summary>
	public HeidemarieHangedChosen()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：重击（2 费稀有，走 u1 分支）。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackHeavy;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		int hitCount = 1;

		// 手牌快照：出牌时本牌已离开手牌，这里数到的就是「其余手牌」
		List<CardModel> linked = PileType.Hand.GetPile(Owner).Cards
			.Where(c => c is HeidemarieCard holder && holder.IsLinked)
			.ToList();

		if (linked.Count > 0)
		{
			// 丢弃「全部」连接牌：只要丢到就只 +1 次（不论几张；一次批量弃，禁止循环单张）
			await CardCmd.Discard(choiceContext, linked);
			hitCount++;
		}

		await AttackWithPresentation(DynamicVars.Damage.BaseValue, cardPlay)
			.WithHitCount(hitCount)
			.Targeting(cardPlay.Target)
			.WithHitFx(VfxCmd.heavyBluntPath)
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
