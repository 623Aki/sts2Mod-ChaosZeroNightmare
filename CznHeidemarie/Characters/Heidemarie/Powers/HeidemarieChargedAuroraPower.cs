using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「充能极光」：接下来 <see cref="PowerModel.Amount"/> 张「极光剑」的伤害
/// +<see cref="Percent"/>%（设计文档 §5.3）。
///
/// <para><b>数值进入「极光剑增伤区」</b>：本能力只在还存在次数时提供
/// <see cref="Percent"/> 个百分点（见 <see cref="HeidemarieAuroraDamagePoolPower.TotalPercent"/>），
/// 与极夜<b>相加</b>，不另挂乘算 modifier。</para>
///
/// <para><b>消耗时机</b>：每张极光剑结算一次（主动打出走
/// <see cref="AfterAttack"/>；被丢弃时的伤害由
/// <see cref="HeidemarieAuroraSword"/> 自己调用 <see cref="ConsumeAsync"/>）扣 1 次。</para>
///
/// <para>当前（本轮）没有发放来源卡：设计文档 §6.1 的「蓄光」不在 §4.1 的落地名单里，
/// 本能力先按文档落地，供后续「蓄光」直接用
/// <see cref="GrantAsync"/> 发放。</para>
/// </summary>
public sealed class HeidemarieChargedAuroraPower : PowerModel
{
	/// <summary>存在剩余次数时，极光剑获得的增伤百分点。</summary>
	public const decimal Percent = 100m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>层数 = 剩余次数，可叠加。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>获得时确保极光剑增伤区与百分比增伤池都已挂载（池不可见，是唯一乘算出口）。</remarks>
	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
		=> HeidemarieAuroraDamagePoolPower.EnsureAsync(Owner);

	/// <inheritdoc/>
	/// <remarks>主动打出极光剑：每结算一张扣 1 次。</remarks>
	public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		if (command.Attacker == Owner && command.ModelSource is HeidemarieAuroraSword)
		{
			await ConsumeAsync(Owner);
		}
	}

	/// <summary>
	/// 发放若干次「充能」（供「蓄光」这类卡牌调用）。
	/// </summary>
	/// <param name="choiceContext">上下文。</param>
	/// <param name="owner">能力持有者。</param>
	/// <param name="count">次数。</param>
	public static Task GrantAsync(PlayerChoiceContext choiceContext, Creature owner, decimal count)
		=> PowerCmd.Apply<HeidemarieChargedAuroraPower>(
			choiceContext,
			owner,
			count,
			owner,
			null,
			silent: true);

	/// <summary>
	/// 消耗 1 次。次数归零则移除能力（图标不会停在「0」）。
	/// </summary>
	/// <param name="owner">能力持有者。</param>
	public static async Task ConsumeAsync(Creature owner)
	{
		HeidemarieChargedAuroraPower? power = owner.GetPower<HeidemarieChargedAuroraPower>();
		if (power == null)
		{
			return;
		}

		if (power.Amount <= 1)
		{
			await PowerCmd.Remove(power);
			return;
		}

		power.SetAmount(power.Amount - 1, silent: true);
	}
}
