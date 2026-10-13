using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「安魂剑」：2 费攻击（公共牌），目标为任意敌人。
///
/// <para>关键字<b>链接</b>：直接使用本牌时，丢弃手牌中其他所有带「链接」的牌。</para>
/// <para>造成 6 点伤害 2 次；升级后费用 -1。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，生成 2 张极光剑到<strong>手牌</strong>。</para>
/// <para>关键字<b>安息</b>：非回合结束被丢弃时（例如被别的链接牌连带丢弃），
/// 照样发动效果 —— 对一个随机敌人造成 6 点伤害 2 次；且本回合内不能被移出墓地。</para>
/// </summary>
public sealed class HeidemarieRequiemBlade : HeidemarieCard, ILinkCard, IRestCard, ISenseCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(6m, ValueProp.Move),
		// 感应生成的极光剑张数
		new CardsVar(2),
	};

	/// <summary>2 费、攻击、公共牌、目标为任意敌人。</summary>
	public HeidemarieRequiemBlade()
		: base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Hand);
	}

	/// <inheritdoc/>
	/// <remarks>安息：被丢弃时对随机敌人结算两次伤害（无目标选择，取随机可命中敌人）。</remarks>
	public async Task OnRest(PlayerChoiceContext choiceContext)
	{
		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		Creature? target = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
		if (target == null)
		{
			return;
		}

		await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, Owner.Creature, this, null);
		await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, Owner.Creature, this, null);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
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
		EnergyCost.UpgradeBy(-1);
	}
}
