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
/// 能力「极夜」：<b>层数 = 还能增强多少张极光剑</b>。
///
/// <list type="bullet">
///   <item>每层使<b>下一张极光剑</b>的伤害 +<see cref="PercentPerLayer"/>%；</item>
///   <item>不设层数上限，<b>每生效一次层数 -1</b>
///   （打出一把剑扣 1 层；剑被丢弃时的伤害同样算一次结算，也扣 1 层）；</item>
///   <item>层数归零时能力移除；<b>不随回合结束消失</b> —— 没用完的层数留给下回合。</item>
/// </list>
///
/// <para><b>为什么合并「白昼」</b>：原本「白昼（+X%，只作用于下一张剑）」与
/// 「极夜（本回合 +X%，回合末清空）」是同一件事的两种计时方式，
/// 卡面要分别描述、玩家要分别理解。合并后统一成「按张数计费」的层数模型 ——
/// 卡面只写「极夜+N」，实际就是给 N 层，每张剑消耗 1 层、每层 +100%。</para>
///
/// <para><b>因子仍由单一出口提供</b>：本能力只保存层数，伤害因子由
/// <see cref="HeidemarieAuroraDamagePoolPower.TotalPercent"/> 汇总、交给
/// <see cref="HeidemarieDamagePercentPower"/> 这一个乘算 modifier ——
/// 与「百分比增伤池 / 极光连锁基线」全部<b>相加</b>，不会退化成连乘。</para>
///
/// <para><b>消耗时机</b>：主动打出走 <see cref="AfterAttack"/>（每张剑一条攻击指令，
/// 正好一次）；被丢弃时的伤害不是 <c>AttackCommand</c>，由
/// <see cref="HeidemarieAuroraSword"/> 自己调用 <see cref="ConsumeAsync"/>。</para>
/// </summary>
public sealed class HeidemarieNightPower : PowerModel
{
	/// <summary>每层提供的增伤百分点（1 层 = 一张剑 +100%）。</summary>
	public const decimal PercentPerLayer = 100m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>层数 = 剩余张数，可叠加、不设上限。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>获得时确保「极光剑增伤区」与「百分比增伤池」都已挂载（池不可见，是唯一乘算出口）。</remarks>
	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
		=> HeidemarieAuroraDamagePoolPower.EnsureAsync(Owner);

	/// <inheritdoc/>
	/// <remarks>主动打出极光剑：结算完消耗 1 层。</remarks>
	public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		if (command.Attacker == Owner && command.ModelSource is HeidemarieAuroraSword)
		{
			await ConsumeAsync(Owner);
		}
	}

	/// <summary>
	/// 消耗 1 层（一次「极光剑结算」）。层数归零则移除能力，图标不会停在「0」。
	/// </summary>
	/// <param name="owner">能力持有者。</param>
	public static async Task ConsumeAsync(Creature owner)
	{
		HeidemarieNightPower? power = owner.GetPower<HeidemarieNightPower>();
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
