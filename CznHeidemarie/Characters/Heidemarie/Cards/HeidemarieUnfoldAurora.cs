using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「展开极光」：2 费技能（公共牌）。
///
/// <para>关键字<b>安息</b>。在手牌生成 <c>Cards</c> 张极光剑，
/// 并获得 <see cref="NightLayers"/> 层<b>极夜</b>（每层使下一张极光剑伤害 +100%）。</para>
/// </summary>
public sealed class HeidemarieUnfoldAurora : HeidemarieCard, IRestCard
{
	/// <summary>
	/// 给的极夜层数 = 2（2 费档）。
	/// 层数按「稀有度 + 费用」定档：Common·1 费 = 1 层，2 费档 = 2 层，Rare·2 费 = 3 层。
	///
	/// <para><b>本牌已降为 Common（仍为 2 费）</b>：层数沿用 2 费档不回调 ——
	/// 它是 Common 池里唯一「无条件、入手牌」的大产量剑源，前期手感的支点就在这张牌上。</para>
	/// </summary>
	private const decimal NightLayers = 2m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(2),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜+2」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>2 费、技能、公共牌（Common）、目标为自身。</summary>
	/// <remarks>
	/// 稀有度由 Uncommon 降为 Common：让「核心卡前期摸不到」的问题缓解 ——
	/// 前期掉落权重最大的是 Common，而原本 Common 池里只有凝锋一张无条件下产剑。
	///
	/// <para>演出不受影响：默认档位按「稀有度 == Uncommon 或费用 ≥ 2」判为结阵档，
	/// 本牌 2 费仍然命中后半条，因此仍是 <c>u2_buff_ready → u2_buff_play</c>。</para>
	/// </remarks>
	public HeidemarieUnfoldAurora()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Generate(choiceContext);

	private async Task Generate(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Hand);
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			NightLayers,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
