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
/// <para>关键字<b>连接</b>：直接使用本牌时，丢弃手牌中其他所有带「连接」的牌。</para>
/// <para>造成 6 点伤害 2 次；升级后费用 -1。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，生成 2 张极光剑到<strong>手牌</strong>。</para>
/// <para>关键字<b>安息</b>：非回合结束被丢弃时（例如被别的连接牌连带丢弃），
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

	/// <summary>2 费、攻击、公共牌、随机目标（触发安息时无目标可选，故主伤害也改为随机）。</summary>
	public HeidemarieRequiemBlade()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
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
		// 「连接」的连带丢弃由 HeidemarieCard.BeforeCardPlayed 统一在出牌前完成

		// 随机目标：cardPlay.Target 恒为 null，目标由 TargetingRandomOpponents 逐段随机分配
		// 2 费双段 → 走 Attack2 纵劈分支（attack_ready → attack_play2），比打击更重
		// 特效时间轴见 effects/heidemarie_moves.json 的 HeidemarieRequiemBlade 键（8 层）
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Heavy.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Heavy))
			.WithHitCount(2)
			.TargetingRandomOpponents(CombatState!)
			.WithHitFx("vfx/vfx_heavy_blunt")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}
}
