using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑雨」：1 费攻击（基础牌）。
///
/// <para>关键字<b>链接</b>：直接使用本牌时，丢弃手牌中其他所有带「链接」的牌。</para>
/// <para>造成 4 点伤害 2 次；升级后感应生成的极光剑 +1。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，生成 1 张极光剑到<strong>抽牌堆</strong>。</para>
/// </summary>
public sealed class HeidemarieSwordrain : HeidemarieCard, ILinkCard, ISenseCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(4m, ValueProp.Move),
		// 感应生成的极光剑张数（升级 +1）
		new CardsVar(1),
	};

	/// <summary>1 费、攻击、公共牌、目标为任意敌人。</summary>
	public HeidemarieSwordrain()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Draw);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 链接：先丢弃手里其他链接牌（会连带触发它们的安息 / 回收等）
		await HeidemarieCardActions.DiscardOtherLinked(this, choiceContext);

		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.Basic.HitDelaySeconds)
			.WithHitCount(2)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Cards.UpgradeValueBy(1m);
	}
}
