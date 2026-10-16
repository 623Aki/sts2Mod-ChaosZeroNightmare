using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「绝响」：1 费攻击（<strong>基础牌</strong>），对<strong>全体敌人</strong>。
///
/// <para>原生关键字 <b>保留</b>。</para>
///
/// <para>对全体敌人造成 10 点伤害；升级后费用 -1（变为 0 费）。</para>
///
/// <para><b>初始卡组自带</b>（见 <see cref="Heidemarie.StartingDeck"/>）——
/// 由角色卡调整为基础牌，因此不再参与奖励 / 商店抽取，也不列在
/// <see cref="HeidemarieCardPool"/> 中。</para>
///
/// <para>完整旧效果在先古牌「终焉」，由本牌经「太古之牙」淬炼而来
/// （见 <c>Registration/HeidemarieAncientTranscendencePatch.cs</c>）。</para>
///
/// <para>演出走骨架的重·斩击档（<c>U1Attack</c>）。</para>
/// </summary>
public sealed class HeidemarieFinalEcho : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(10m, ValueProp.Move),
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Retain,
	};

	/// <summary>1 费、攻击、基础牌、对全体敌人。</summary>
	public HeidemarieFinalEcho()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AllEnemies)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 对全体敌人造成 10 点伤害（无附加增伤逻辑）。
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			// Basic 1 费：重·斩击档（U1Attack）
			.WithAttackerAnim(HeidemarieAttackPlan.U1.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.U1))
			.TargetingAllOpponents(CombatState!)
			.WithHitFx("vfx/vfx_bloody_impact")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		// 升级：费用 -1（1 费 → 0 费）
		EnergyCost.UpgradeBy(-1);
	}
}
