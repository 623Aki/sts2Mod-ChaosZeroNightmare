using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Presentation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑潮」：2 费攻击（稀有牌）。
///
/// <para>造成 <c>Damage</c> 点伤害；<b>打出前</b>手牌中每有 1 张「极光剑」，
/// 额外造成 1 次（最多额外 <c>Swords</c> 次）。</para>
///
/// <para>关键字<b>感应</b>：在抽牌堆生成 <c>Cards</c> 张极光剑 ——
/// 自己的感应给下一张「剑潮」备好弹药。</para>
///
/// <para><b>计数口径</b>：写的是「打出前手牌中」，而本牌不是连接牌、不会连带丢弃，
/// 出牌瞬间（<c>OnPlay</c>）手牌里的极光剑数与「打出前」一致；
/// 本牌自身不是极光剑，因此不需要额外排除自己。</para>
///
/// <para><b>封顶用动态变量</b>（§7.3 #6）：「最多额外 3 次」写成
/// <c>DynamicVar("Swords", 3)</c>，卡面写 <c>{Swords:diff()}</c>，避免无上限滚雪球。</para>
/// </summary>
public sealed class HeidemarieDownpour : HeidemarieCard, ISenseCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(3)
	{
		new DamageVar(6m, ValueProp.Move),
		// 额外段数上限（不随升级变化）
		new DynamicVar("Swords", 3m),
		// 感应生成的极光剑张数
		new CardsVar(1),
	};

	/// <summary>2 费、攻击、稀有牌、指向任意敌人。</summary>
	public HeidemarieDownpour()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：重击（多段大伤害，与「绝响」同档）。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackHeavy;

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(
			this,
			choiceContext,
			DynamicVars.Cards.IntValue,
			PileType.Draw);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 打出前手牌中的极光剑数量，按 Swords 封顶后折算成额外段数。
		int swordsInHand = PileType.Hand.GetPile(Owner).Cards
			.Count(card => card is HeidemarieAuroraSword);
		int extraHits = Math.Min(swordsInHand, DynamicVars["Swords"].IntValue);
		int hitCount = 1 + extraHits;

		await AttackWithPresentation(DynamicVars.Damage.BaseValue, cardPlay)
			.WithHitCount(hitCount)
			.Targeting(cardPlay.Target)
			.WithHitFx(VfxCmd.slashPath)
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
