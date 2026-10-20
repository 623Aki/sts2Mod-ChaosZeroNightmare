using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「梦之边境」：3 费攻击（稀有牌），目标为<b>随机敌人</b>。
///
/// <para><b>主动打出</b>：只对随机敌人造成 <c>Damage</c> 点伤害
/// （安息只在被丢弃时派发，出牌不触发弃牌效果）。</para>
///
/// <para><b>被丢弃时</b>：两个效果都触发，顺序为<b>先伤害、后弃牌</b>：</para>
/// <list type="number">
///   <item>对<strong>随机</strong>敌人造成 <c>Damage</c> 点伤害 —— 与极光剑同构，
///   <strong>自己写在 <see cref="AfterCardDiscarded"/> 里</strong>；</item>
///   <item>安息效果：随机丢弃抽牌堆的 <c>Cards</c> 张牌 —— 仍走
///   <see cref="HeidemarieCard.AfterCardDiscarded"/> 的安息派发
///   （<see cref="OnRest"/>），<b>不</b>像极光剑那样覆盖
///   <c>OnRestEffect</c> 屏蔽掉。</item>
/// </list>
///
/// <para>「随机位置取牌」是设计定稿口径（Q7 = A）：抽牌堆是有序堆，
/// 用 <c>Rng.CombatCardSelection</c> 从中随机取，而不是固定取顶部，
/// 这样它不会和「窥星择光」这类看顶牌的效果变成同一件事。</para>
///
/// <para>⚠️ 丢弃<b>必须走批量重载</b> <c>CardCmd.Discard(ctx, IEnumerable)</c>：
/// 循环调用单张版会错乱 <c>Sly</c>（被弃自动打出）的时机。</para>
/// </summary>
public sealed class HeidemarieDreamBorder : HeidemarieCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(20m, ValueProp.Move),
		// 安息时丢弃的抽牌堆张数（升级不改）
		new CardsVar(2),
	};

	/// <summary>3 费、攻击、稀有牌、随机敌人。</summary>
	public HeidemarieDreamBorder()
		: base(3, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：顶击（3 费重牌，走 u3 分支）。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackTop;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 随机目标：cardPlay.Target 恒为 null，逐段随机分配命中对象
		await AttackWithPresentation(DynamicVars.Damage.BaseValue, cardPlay)
			.TargetingRandomOpponents(CombatState!)
			.WithHitFx(VfxCmd.heavyBluntPath)
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 被丢弃：先对随机敌人结算一次伤害，再走基类的安息派发（锁墓 + 弃抽牌堆 2 张）。
	///
	/// <para>伤害段与极光剑同构：自己写在 <c>AfterCardDiscarded</c> 里，
	/// 不借「安息」关键字触发。与极光剑不同的是：本牌<b>保留</b>「安息」，
	/// 所以基类派发（弃抽牌堆）照常要走，这里只把伤害段插在它前面。</para>
	/// </remarks>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (card == this)
		{
			await DealDiscardDamage(choiceContext);
		}

		// 基类负责「安息」派发：置 RestLockedThisTurn 并调用 OnRest（弃抽牌堆 2 张）
		await base.AfterCardDiscarded(choiceContext, card);
	}

	/// <summary>
	/// 被丢弃：对随机敌人造成一次伤害（与极光剑的丢弃伤害同构）。
	///
	/// <para>没有可命中的敌人 = 这次伤害结算没发生，直接跳过
	/// （不会阻塞后面的安息弃牌）。</para>
	/// </summary>
	private async Task DealDiscardDamage(PlayerChoiceContext choiceContext)
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
	}

	/// <inheritdoc/>
	/// <remarks>关键字「安息」：被丢弃时随机弃掉抽牌堆的若干张牌。</remarks>
	public async Task OnRest(PlayerChoiceContext choiceContext)
	{
		// 快照：下面边挑边弃会改动抽牌堆，直接遍历活集合会抛异常
		List<CardModel> pool = PileType.Draw.GetPile(Owner).Cards.ToList();
		if (pool.Count == 0)
		{
			// 抽牌堆无牌可丢，直接返回
			return;
		}

		List<CardModel> picked = new();

		for (int i = 0; i < DynamicVars.Cards.IntValue && pool.Count > 0; i++)
		{
			// 同一张牌不会被重复选中：选完从候选池摘掉
			CardModel? card = Owner.RunState.Rng.CombatCardSelection.NextItem(pool);
			if (card == null)
			{
				break;
			}

			picked.Add(card);
			pool.Remove(card);
		}

		if (picked.Count > 0)
		{
			await CardCmd.Discard(choiceContext, picked);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(10m);
}
