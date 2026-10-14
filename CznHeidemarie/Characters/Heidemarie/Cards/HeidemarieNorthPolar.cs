using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「北极圈」：2 费能力牌（稀有牌）。
///
/// <para>打出后获得能力<b>北极圈</b>：回合开始时在手牌生成 2 张极光剑（可叠加）。</para>
///
/// <para>升级：<b>本卡牌自身</b>获得关键字「安息」（见 <see cref="IsRestCard"/>）——
/// 即本牌被<b>非回合结束</b>丢弃时发动其效果（与打出一样获得「北极圈」能力），
/// 且本回合不能被移出墓地。</para>
///
/// <para>注意：升级<strong>不影响</strong>能力生成的极光剑 —— 那些剑不带任何额外关键字，
/// 生成逻辑见 <see cref="HeidemarieNorthPolarPower"/>。</para>
/// </summary>
public sealed class HeidemarieNorthPolar : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>2 费、能力、稀有牌、目标为自身。</summary>
	public HeidemarieNorthPolar()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <summary>
	/// 升级「赋予关键字安息」：只让<b>本卡牌</b>成为安息牌。
	///
	/// <para>设计文档约定升级档为「赋予此卡牌关键字<b>安息</b>」，因此这里把
	/// <see cref="CardModel.IsUpgraded"/> 纳入判定 —— 升级后卡面显示「安息」词条，
	/// 且丢弃时走 <see cref="HeidemarieCard.AfterCardDiscarded"/> 的安息派发。</para>
	/// </summary>
	public override bool IsRestCard => base.IsRestCard || IsUpgraded;

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> ApplyNorthPolar(choiceContext);

	/// <summary>
	/// 关键字「安息」：本牌被非回合结束丢弃时发动其效果 —— 与打出一致，获得「北极圈」能力。
	/// </summary>
	protected override Task OnRestEffect(PlayerChoiceContext choiceContext)
		=> ApplyNorthPolar(choiceContext);

	/// <summary>本牌效果（打出 / 安息发动共用）：获得 1 层能力「北极圈」。</summary>
	private async Task ApplyNorthPolar(PlayerChoiceContext choiceContext)
	{
		await PowerCmd.Apply<HeidemarieNorthPolarPower>(
			choiceContext,
			Owner.Creature,
			1m,
			Owner.Creature,
			this,
			silent: true);
	}
}
