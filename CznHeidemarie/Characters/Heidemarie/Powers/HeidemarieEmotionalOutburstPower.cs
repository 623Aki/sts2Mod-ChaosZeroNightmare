using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「情绪爆发」：每当你丢弃 <see cref="CardsPerEnergy"/> 张牌，获得 1 点能量。
///
/// <para><b>回合结束的弃牌不算</b>：官方回合末清手牌走 <c>CardPileCmd.Add</c>，
/// 不会派发 <see cref="AbstractModel.AfterCardDiscarded"/>，因此这条判据天然成立
/// （与「壁仞千里」同一依据）。</para>
///
/// <para><b>为什么用私有计数器而不是 <see cref="PowerModel.Amount"/></b>（设计已定 Q12 = A）：
/// 层数对玩家是可见的，若把「已丢 3 张」写进层数，卡面会显示「情绪爆发 3」，
/// 玩家会误以为叠了 3 层效果。层数在这里只表示「每 N 张」的档位（当前恒为 1 层）。</para>
/// </summary>
public sealed class HeidemarieEmotionalOutburstPower : PowerModel
{
	/// <summary>累计丢弃多少张牌发 1 点能量。</summary>
	private const int CardsPerEnergy = 4;

	/// <summary>本回合已丢弃的张数（不对外显示）。</summary>
	private int _discardedSinceLastEnergy;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		// 弃牌是全场事件，会在 Hook 里广播给所有战斗模型 —— 先判归属。
		if (card.Owner != Owner.Player)
		{
			return;
		}

		// 再加一道「当前行动方」判定：敌方回合里因故弃牌不该给我方发能量。
		ICombatState? combatState = Owner.CombatState;
		if (combatState == null || Owner.Side != combatState.CurrentSide)
		{
			return;
		}

		_discardedSinceLastEnergy++;
		if (_discardedSinceLastEnergy < CardsPerEnergy)
		{
			return;
		}

		// 用减法而不是清零：一弃 8 张时应当发 2 点能量。
		_discardedSinceLastEnergy -= CardsPerEnergy;

		Flash();
		await PlayerCmd.GainEnergy(1m, Owner.Player);
	}
}
