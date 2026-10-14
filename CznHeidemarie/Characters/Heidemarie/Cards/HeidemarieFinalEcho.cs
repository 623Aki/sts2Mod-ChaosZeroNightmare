using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「绝响」：3 费攻击（稀有牌），对<strong>全体敌人</strong>。
///
/// <para>原生关键字 <b>消耗</b> / <b>保留</b>；自定义关键字 <b>终结</b>（用后立即结束本回合）、
/// <b>唯一</b>（卡组内仅可持有 1 张）。</para>
///
/// <para>对全体敌人造成 20 点伤害；升级后 +10。</para>
///
/// <para>若手牌中存在「解放极光」，则将其消耗；此时墓地中每有 1 张「极光剑」，
/// 本次伤害 +100%（百分点注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池，
/// 与剑光等其他百分比增伤<strong>相加</strong>）。</para>
///
/// <para>演出走骨架的 ux 终结技动作（与全屏 ux 演出对齐）。</para>
/// </summary>
public sealed class HeidemarieFinalEcho : HeidemarieCard, ITerminateCard, IUniqueCard
{
	/// <summary>每张极光剑提供的增伤百分点。</summary>
	private const decimal PercentPerSword = 100m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(20m, ValueProp.Move),
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(2)
	{
		CardKeyword.Exhaust,
		CardKeyword.Retain,
	};

	/// <summary>3 费、攻击、稀有牌、对全体敌人。</summary>
	public HeidemarieFinalEcho()
		: base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 若手牌中有解放极光 → 消耗，并按墓地里的极光剑数量叠增伤
		CardModel? liberated = PileType.Hand.GetPile(Owner).Cards
			.FirstOrDefault(c => c is HeidemarieLiberatedAurora);

		int swords = 0;
		if (liberated != null)
		{
			await CardCmd.Exhaust(choiceContext, liberated);
			swords = PileType.Discard.GetPile(Owner).Cards.Count(c => c is HeidemarieAuroraSword);
		}

		// 墓地每有 1 张极光剑 +100%；入池与清池由基类包裹
		// （见 HeidemarieCard.WithPercentDamageBoost，攻击结束必定清池）。
		await WithPercentDamageBoost(choiceContext, swords * PercentPerSword, async () =>
		{
			await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
				.FromCard(this, cardPlay)
				.WithAttackerAnim(HeidemarieAttackPlan.Ux.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Ux))
				.TargetingAllOpponents(CombatState!)
				.WithHitFx("vfx/vfx_bloody_impact")
				.Execute(choiceContext);
		});

		// 终结：打出后立即结束本回合（走游戏原生回合结束逻辑）
		PlayerCmd.EndTurn(Owner, canBackOut: false);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(10m);
	}
}
