using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「叠光奏浪」：2 费攻击（公共牌）。
///
/// <para>关键字<b>连接</b>。造成 14 点伤害；打出前手牌中带「连接」的牌（含自身）
/// 数量 ≥3 时，伤害变为 3 倍（在基础 / 动态伤害上×3，再叠加外部百分比）。</para>
/// </summary>
public sealed class HeidemarieTripleGlow : HeidemarieCard, ILinkCard
{
	/// <summary>触发倍率所需的连接牌数量。</summary>
	private const int Threshold = 3;

	/// <summary>达标后的伤害倍率。</summary>
	private const decimal Multiplier = 3m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(14m, ValueProp.Move),
	};

	/// <summary>2 费、攻击、公共牌、目标为任意敌人。</summary>
	public HeidemarieTripleGlow()
		: base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「连接」的连带丢弃由 HeidemarieCard.BeforeCardPlayed 统一在出牌前完成；
		// 计数读的是那里取好的「打出前」快照。卡面写了「打出前」→ 含自身
		// （口径见 ResolveLinkedCountBeforePlay）。
		int linked = ResolveLinkedCountBeforePlay(includeSelf: true);
		decimal damage = DynamicVars.Damage.BaseValue;
		if (linked >= Threshold)
		{
			damage *= Multiplier;
		}

		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// Common 2 费 → 中·纵劈档（Attack2，原 U1 斩击档下调）
		// 特效时间轴见 effects/heidemarie_moves.json 的 HeidemarieTripleGlow 键
		await DamageCmd.Attack(damage)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Heavy.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Heavy))
			.Targeting(cardPlay.Target)
			// 重击手感：钝击命中特效比斩击更沉
			.WithHitFx("vfx/vfx_heavy_blunt")
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(8m);
}
