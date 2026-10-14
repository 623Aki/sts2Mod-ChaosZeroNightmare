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
/// Heidemarie 的「打击」：1 费攻击，对单个敌人造成 6 点伤害，升级后 9 点。
/// </summary>
public sealed class HeidemarieStrike : HeidemarieCard
{
	/// <inheritdoc/>
	/// <remarks>专属卡面上线前，临时复用战士打击的立绘。</remarks>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(6m, ValueProp.Move),
	};

	/// <summary>1 费、攻击、基础牌、目标为任意敌人。</summary>
	public HeidemarieStrike()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			// 显式指定攻击触发器：与「重斩」共用 attack_ready 起手，走 attack_play1 横挥分支
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Basic))
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
