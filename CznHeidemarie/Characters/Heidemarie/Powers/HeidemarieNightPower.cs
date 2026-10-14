using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「极夜」：**本回合内**极光剑的伤害提高 <see cref="PowerModel.Amount"/>%。
///
/// <para><b>因子由「极光剑增伤区」统一提供</b>（见 <see cref="HeidemarieAuroraDamagePoolPower"/>）：
/// 本能力只保存层数与生命周期，<strong>不再自己 override 乘算 modifier</strong>，
/// 也不需要再判断「场上有没有白昼」——「谁提供因子」的协调已收敛到该能力一处。</para>
///
/// <para>回合结束清除（实现为「下个我方回合开始时移除」，对本回合伤害而言等价）。</para>
/// </summary>
public sealed class HeidemarieNightPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>首次获得时确保「极光剑增伤区」已启用（幂等，重复获得不会重复挂载）。</remarks>
	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
		=> HeidemarieAuroraDamagePoolPower.EnsureAsync(Owner);

	/// <inheritdoc/>
	/// <remarks>本回合结束后清除。</remarks>
	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner.Player)
		{
			return PowerCmd.Remove(this);
		}

		return Task.CompletedTask;
	}
}
