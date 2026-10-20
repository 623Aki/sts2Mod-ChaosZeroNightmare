using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「严阵以待」的<b>延时载体</b>：记录「下一回合该给多少临时力量」，到点后转成
/// <see cref="HeidemarieTemporaryStrengthPower"/> 并自我移除。
///
/// <para><b>为什么需要这一层</b>：官方没有「下回合力量」的现成能力
/// （只有 <c>DrawCardsNextTurnPower</c> / <c>EnergyNextTurnPower</c> 这类按回合生效的写法）。
/// 而 <see cref="HeidemarieTemporaryStrengthPower"/> 一施加就会立刻加力量、并在
/// <em>当回合结束</em>扣回 —— 直接施加就变成「本回合」而不是「下一回合」了。</para>
///
/// <para><b>为什么不用额外计数</b>：本能力只可能在玩家回合中途被创建，
/// 而 <c>AfterPlayerTurnStart</c> 每回合只在该回合开始时触发一次，
/// 所以「下一次收到它」必然就是下一回合的开始，无需记回合号。</para>
/// </summary>
public sealed class HeidemarieStandReadyPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>层数 = 届时施加的临时力量点数；多次打出累加。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner.Player)
		{
			return;
		}

		decimal strength = Amount;
		if (strength <= 0m)
		{
			await PowerCmd.Remove(this);
			return;
		}

		Flash();
		await PowerCmd.Apply<HeidemarieTemporaryStrengthPower>(
			choiceContext,
			Owner,
			strength,
			Owner,
			null);

		await PowerCmd.Remove(this);
	}
}
