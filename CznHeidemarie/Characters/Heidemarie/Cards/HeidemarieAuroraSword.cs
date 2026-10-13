using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「极光剑」：1 费攻击（衍生牌 Token），<strong>非指向</strong>。
///
/// <para>关键字<b>链接</b>：直接使用本牌时，丢弃手牌中其他所有带「链接」的牌。</para>
/// <para>对<strong>随机敌人</strong>造成 3 点伤害 —— <c>TargetType.RandomEnemy</c>，
/// 出牌时不选目标，伤害由 <c>TargetingRandomOpponents</c> 逐次随机分配。</para>
/// <para>关键字<b>守墓</b>：洗牌时不会移动至抽牌堆，会一直留在墓地。</para>
///
/// <para>不可升级、不可被战斗内随机生成（只能由本角色的卡牌 / 能力生成）。</para>
/// </summary>
public sealed class HeidemarieAuroraSword : HeidemarieCard, ILinkCard, IGraveKeepCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(3m, ValueProp.Move),
	};

	/// <inheritdoc/>
	/// <remarks>衍生牌不可升级。</remarks>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>避免被「攻击药水 / Discovery」之类的战斗内随机生成效果抽到。</remarks>
	public override bool CanBeGeneratedInCombat => false;

	/// <summary>1 费、攻击、衍生牌、非指向（伤害随机分配给敌人）。</summary>
	public HeidemarieAuroraSword()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.RandomEnemy)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await HeidemarieCardActions.DiscardOtherLinked(this, choiceContext);

		// 非指向牌：cardPlay.Target 恒为 null，目标完全交给 TargetingRandomOpponents 随机分配
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.Basic.HitDelaySeconds)
			.TargetingRandomOpponents(CombatState!)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}
}
