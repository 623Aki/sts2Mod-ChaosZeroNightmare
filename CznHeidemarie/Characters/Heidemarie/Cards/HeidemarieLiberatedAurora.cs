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
/// 「解放极光」：0 费攻击（稀有牌），目标为任意敌人。
///
/// <para>原生关键字 <b>虚无</b>；自定义关键字 <b>唯一</b>、<b>焚烧</b>。</para>
///
/// <para>造成 10 点伤害；消耗墓地中的<strong>所有</strong>「极光剑」，每消耗 1 张，
/// 基础伤害 +100%（百分点注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池）。</para>
///
/// <para>关键字<b>焚烧</b>：本牌被消耗时（虚无的回合末消耗，或被绝响消耗），
/// 在手牌中加入一张「凝聚极光」，重启引擎循环。</para>
///
/// <para>唯一获取渠道 = 能力「极光之光」：禁止随机生成（见 <c>CanBeGeneratedInCombat</c>/
/// <c>CanBeGeneratedByModifiers</c>），只由能力的代码直接创建。</para>
///
/// <para>演出走骨架的 ug 终极技动作（并挂载 ug 全屏演出）。</para>
/// </summary>
public sealed class HeidemarieLiberatedAurora : HeidemarieCard, IUniqueCard, IBurnCard
{
	/// <summary>每张被消耗的极光剑提供的增伤百分点。</summary>
	private const decimal PercentPerSword = 100m;

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
		CardKeyword.Ethereal,
	};

	/// <inheritdoc/>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>
	/// 不能通过除「极光之光」外任何渠道获得：禁止随机生成
	/// （战斗内随机生成由 <see cref="IUniqueCard"/> 在基类统一禁掉；这里再禁掉 modifier 池）。
	/// 能力自己的 <c>CreateCard</c> 不受这两个开关限制。
	/// </remarks>
	public override bool CanBeGeneratedByModifiers => false;

	/// <summary>0 费、攻击、稀有牌、目标为任意敌人。</summary>
	public HeidemarieLiberatedAurora()
		: base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnBurn(PlayerChoiceContext choiceContext, bool causedByEthereal)
	{
		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		// 焚烧：手牌加入一张凝聚极光，重启引擎
		CardModel condensed = combatState.CreateCard<HeidemarieCondensedAurora>(Owner);
		await CardPileCmd.AddGeneratedCardToCombat(condensed, PileType.Hand, Owner);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 消耗墓地中所有极光剑，每张 +100%
		List<CardModel> swords = PileType.Discard.GetPile(Owner).Cards
			.Where(c => c is HeidemarieAuroraSword)
			.ToList();

		if (swords.Count > 0)
		{
			foreach (CardModel sword in swords)
			{
				await CardCmd.Exhaust(choiceContext, sword);
			}

			await PowerCmd.Apply<HeidemarieDamagePercentPower>(
				choiceContext,
				Owner.Creature,
				swords.Count * PercentPerSword,
				Owner.Creature,
				this,
				silent: true);
		}

		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Ug.Trigger, HeidemarieAttackPlan.Ug.HitDelaySeconds)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_rock_shatter")
			.Execute(choiceContext);
	}
}
