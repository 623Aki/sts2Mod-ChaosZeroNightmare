using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Presentation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「静雨」：1 费攻击（罕见牌）。
///
/// <para>关键字<b>连接</b> + <b>安息</b> + <b>感应</b>（设计文档 §4.1 #1）：</para>
/// <list type="bullet">
///   <item>造成 <c>Damage</c> 点伤害 2 次（基础 3×2，升级 4×2）；</item>
///   <item><b>感应</b>：在抽牌堆生成 <c>Cards</c> 张极光剑；</item>
///   <item><b>安息</b>：被丢弃时再结算一次自身效果（对随机敌人 3 点伤害 2 次）；</item>
///   <item><b>连接</b>：直接使用时丢弃手牌中其他连接牌（由基类统一派发）。</item>
/// </list>
///
/// <para><b>为什么安息效果是「随机目标」</b>：丢弃发生在手牌结算之外的时机，
/// 没有玩家指定目标的机会，与「极光剑 / 陨锋·坠月」被丢弃时的处理一致 ——
/// 走 <c>CreatureCmd.Damage</c> 逐段随机取一个可命中敌人。</para>
/// </summary>
public sealed class HeidemarieSilentRain : HeidemarieCard, ILinkCard, IRestCard, ISenseCard
{
	/// <summary>本牌每次结算的段数（打出与被丢弃时相同）。</summary>
	private const int HitCount = 2;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(3m, ValueProp.Move),
		// 感应生成的极光剑张数
		new CardsVar(2),
	};

	/// <summary>1 费、攻击、罕见牌、指向任意敌人。</summary>
	public HeidemarieSilentRain()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：轻击（与「剑雨」同档，双段轻斩）。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackLight;

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(
			this,
			choiceContext,
			DynamicVars.Cards.IntValue,
			PileType.Draw);
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => DealDiscardDamage(choiceContext);

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「连接」的连带丢弃由基类 BeforeCardPlayed 在出牌前完成
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await AttackWithPresentation(DynamicVars.Damage.BaseValue, cardPlay)
			.WithHitCount(HitCount)
			.Targeting(cardPlay.Target)
			.WithHitFx(VfxCmd.slashPath)
			.Execute(choiceContext);
	}

	/// <summary>
	/// 「安息」：被丢弃时按段数逐段造成伤害（每段各自随机取一个可命中敌人）。
	///
	/// <para>没有可命中敌人时直接返回 —— 这次结算没有发生，不额外做任何事。</para>
	/// </summary>
	private async Task DealDiscardDamage(PlayerChoiceContext choiceContext)
	{
		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		for (int i = 0; i < HitCount; i++)
		{
			Creature? target = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
			if (target == null)
			{
				return;
			}

			await CreatureCmd.Damage(
				choiceContext,
				target,
				DynamicVars.Damage,
				Owner.Creature,
				this,
				null);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}
