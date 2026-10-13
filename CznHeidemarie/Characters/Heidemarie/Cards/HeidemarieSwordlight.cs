using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑光」：1 费攻击（基础牌）。
///
/// <para>造成 8 点伤害；升级后 +2。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，使下一张攻击牌伤害 +50%
/// （注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池）。</para>
/// </summary>
public sealed class HeidemarieSwordlight : HeidemarieCard, ISenseCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(8m, ValueProp.Move),
	};

	/// <summary>1 费、攻击、基础牌、目标为任意敌人。</summary>
	public HeidemarieSwordlight()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		// +50 入增伤池；命中下一张攻击后由该能力自行清空
		await PowerCmd.Apply<HeidemarieDamagePercentPower>(
			choiceContext,
			Owner.Creature,
			HeidemarieDamagePercentPower.SenseBoost,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.Basic.HitDelaySeconds)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
