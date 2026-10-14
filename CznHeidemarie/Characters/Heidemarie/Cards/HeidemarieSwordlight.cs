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
/// <para>关键字<b>感应</b>：抽取本牌后，获得能力<b>白昼</b>，
/// 使下一张<b>极光剑</b>的伤害 +50%。</para>
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
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		// 挂载「白昼」+50：只作用于下一张极光剑，打出后由该能力自行清除
		await PowerCmd.Apply<HeidemarieDaylightPower>(
			choiceContext,
			Owner.Creature,
			HeidemarieDaylightPower.SenseBoost,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 1 费单段：走打击动作（Attack）+ 6 层剑光特效，
		// 时间轴见 effects/heidemarie_moves.json 的 HeidemarieSwordlight 键
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Basic))
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
