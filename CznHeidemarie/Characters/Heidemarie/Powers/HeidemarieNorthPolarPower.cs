using System;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「北极圈」：我方回合开始时，在手牌生成 2 张「极光剑」。
///
/// <para>可叠加：层数为 <c>StackType.Counter</c>，每层提供 2 张，
/// 因此打出 N 次「北极圈」后回合开始会获得 <c>2 × N</c> 张极光剑。</para>
///
/// <para><b>生成的剑不带任何额外关键字</b>。升级档「赋予安息」作用于
/// 「北极圈」<b>卡牌自身</b>（见 <see cref="HeidemarieNorthPolar.IsRestCard"/>），
/// 与本能力生成的剑无关。</para>
/// </summary>
public sealed class HeidemarieNorthPolarPower : PowerModel
{
	/// <summary>每层能力在回合开始时生成的极光剑张数。</summary>
	private const int SwordsPerTurnPerStack = 2;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner.Player || CombatState is not { } combatState)
		{
			return;
		}

		// 按层数叠加：多次打出「北极圈」时本能力层数累加，回合开始生成的剑数随之翻倍
		int swordCount = SwordsPerTurnPerStack * Math.Max(1, (int)Amount);

		for (int i = 0; i < swordCount; i++)
		{
			HeidemarieAuroraSword sword = combatState.CreateCard<HeidemarieAuroraSword>(player);
			await CardPileCmd.AddGeneratedCardToCombat(sword, PileType.Hand, player);
		}
	}
}
