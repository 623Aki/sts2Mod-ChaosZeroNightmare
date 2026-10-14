using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「一缕光芒」：1 费攻击（公共牌）。
///
/// <para>关键字<b>连接</b>。造成 8 点伤害；打出前手牌中每有 1 张带「连接」的牌
/// （含自身），伤害增加 <c>Percent</c>%（百分点注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池）。
/// 增伤只计算本牌的连接加成，不含白昼 / 极夜（两者只作用于极光剑）。</para>
/// </summary>
public sealed class HeidemarieRayOfLight : HeidemarieCard, ILinkCard
{
	/// <summary>每张连接牌提供的增伤百分点（升级后 +50）。</summary>
	private readonly DynamicVar _percent = new("Percent", 50m);

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DamageVar(8m, ValueProp.Move),
		_percent,
	};

	/// <summary>1 费、攻击、公共牌、目标为任意敌人。</summary>
	public HeidemarieRayOfLight()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「连接」的连带丢弃由 HeidemarieCard.BeforeCardPlayed 统一在出牌前完成；
		// 计数同样在那里、丢弃之前取好快照，否则数到的是已被丢空的手牌。
		// 卡面写了「打出前」→ 含自身（口径见 ResolveLinkedCountBeforePlay）。
		int linked = ResolveLinkedCountBeforePlay(includeSelf: true);

		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 入池量 = 连接牌数 × Percent 个百分点；入池与清池由基类包裹
		// （见 HeidemarieCard.WithPercentDamageBoost，攻击结束必定清池）。
		// 1 费单段：打击动作（Attack）+ 5 层「施法光效 + 斩击」特效，
		// 时间轴见 effects/heidemarie_moves.json 的 HeidemarieRayOfLight 键
		await WithPercentDamageBoost(choiceContext, linked * _percent.BaseValue, async () =>
		{
			await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
				.FromCard(this, cardPlay)
				.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Basic))
				.Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_attack_slash")
				.Execute(choiceContext);
		});
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => _percent.UpgradeValueBy(50m);
}
