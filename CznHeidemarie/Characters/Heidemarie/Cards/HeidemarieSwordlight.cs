using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑光」：1 费攻击（基础牌）。
///
/// <para>造成 8 点伤害；升级后 +2。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，获得 <see cref="SenseLayers"/> 层<b>极夜</b>
/// （每层使下一张极光剑伤害 +100%）。</para>
/// </summary>
public sealed class HeidemarieSwordlight : HeidemarieCard, ISenseCard
{
	/// <summary>
	/// 感应给的极夜层数 = 1（Common / 1 费档）。
	/// 层数按「稀有度 + 费用」定档：Common·1 费 = 1 层，Uncommon·2 费 = 2 层，Rare·2 费 = 3 层。
	/// </summary>
	private const decimal SenseLayers = 1m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/strike_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(8m, ValueProp.Move),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜+1」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>1 费、攻击、基础牌、目标为任意敌人。</summary>
	public HeidemarieSwordlight()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		// 给 1 层「极夜」：每层只作用于一张极光剑，结算后由该能力自行扣层
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			SenseLayers,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	/// <summary>演出档位：轻击（attack_ready → attack_play1）。动作与特效档位必须一致。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackLight;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 1 费单段：走打击动作（Attack）+ 6 层剑光特效，
		// 时间轴见 effects/heidemarie_moves.json 的 HeidemarieSwordlight 键
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(HeidemarieAttackPlan.Basic.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Basic))
			.Targeting(cardPlay.Target)
			.WithHitFx(VfxCmd.slashPath)
			.Execute(choiceContext);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
