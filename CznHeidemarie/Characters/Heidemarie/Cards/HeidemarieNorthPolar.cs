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
/// <para>打出后获得能力<b>北极圈</b>：回合开始时在手牌生成 2 张极光剑。</para>
/// <para>升级：额外赋予生成的极光剑<b>安息</b>（经标记能力 <see cref="HeidemarieNorthPolarRestPower"/> 传递）。</para>
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

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<HeidemarieNorthPolarPower>(
			choiceContext,
			Owner.Creature,
			1m,
			Owner.Creature,
			this,
			silent: true);

		if (IsUpgraded)
		{
			await PowerCmd.Apply<HeidemarieNorthPolarRestPower>(
				choiceContext,
				Owner.Creature,
				1m,
				Owner.Creature,
				this,
				silent: true);
		}
	}
}
