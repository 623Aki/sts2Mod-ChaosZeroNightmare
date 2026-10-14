using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// Heidemarie 的「百分比增伤池」。
///
/// <para><b>为什么需要它</b>：游戏的乘算区是<strong>连乘</strong>
/// （<c>Hook.ModifyDamageInternal</c>：<c>num *= num3</c>），若把「剑光 +50%」
/// 与「绝响 每剑 +100%」各做成一个乘算 modifier，结果会变成
/// <c>(1+0.5) × (1+N)</c>。而设计文档要求的是
/// <c>(基础+固定) × (1 + 0.5 + 其他百分比)</c> —— 百分比<strong>相加</strong>。</para>
///
/// <para>做法：把所有百分比增伤汇总到本能力的 <see cref="MegaCrit.Sts2.Core.Models.PowerModel.Amount"/>（单位：百分点），
/// 由<strong>它单独一个</strong>乘算 modifier 提供因子 <c>1 + Amount/100</c>，
/// 从而得到 <c>1 + Σ百分比</c>。</para>
///
/// <para><b>生命周期：显式入池 / 显式清池</b>（<see cref="InjectAsync"/> /
/// <see cref="ClearAsync"/>）—— 调用方在打出自己那张牌时注入，攻击结算完
/// <strong>由调用方负责清空</strong>，本能力<strong>不再</strong>监听
/// <c>AfterAttack</c> 自动清理。原因：<c>AttackCommand.Execute</c> 存在若干个
/// 「还没走到 <c>Hook.AfterAttack</c> 就提前 return」的分支
/// （攻击者已死亡、战斗已结束等），靠钩子清池会把这些情况漏成「池残留到下一次攻击」。
/// 调用方用 <c>try/finally</c>（或基类的
/// <see cref="CznHeidemarie.Characters.Heidemarie.Cards.HeidemarieCard.WithPercentDamageBoost"/>）
/// 包一层，则无论攻击是否正常结算都会清干净。</para>
///
/// <para>本能力对玩家不可见（<see cref="IsVisibleInternal"/> 返回 false），
/// 不占战斗界面图标。</para>
/// </summary>
public sealed class HeidemarieDamagePercentPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>纯机制能力，不在战斗界面显示图标。</remarks>
	protected override bool IsVisibleInternal => false;

	/// <inheritdoc/>
	/// <remarks>只对「本体所属玩家」的强化攻击（攻击牌 / 生物攻击）生效。</remarks>
	public override decimal ModifyDamageMultiplicative(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource,
		CardPlay? cardPlay)
	{
		if (dealer != Owner || !props.IsPoweredAttack())
		{
			return 1m;
		}

		return 1m + (decimal)Amount / 100m;
	}

	/// <summary>
	/// 入池：把 <paramref name="percentPoints"/> 个百分点累加进增伤池。
	///
	/// <para><b>调用方必须负责清池</b>：本方法只做「入池」。请与
	/// <see cref="ClearAsync"/> 成对使用（推荐直接用基类的
	/// <see cref="CznHeidemarie.Characters.Heidemarie.Cards.HeidemarieCard.WithPercentDamageBoost"/>，
	/// 它已经用 <c>try/finally</c> 保证攻击结束后一定清）。</para>
	/// </summary>
	/// <param name="choiceContext">玩家选择上下文。</param>
	/// <param name="owner">池的所属生物（一般是本角色）。</param>
	/// <param name="percentPoints">增伤百分点（<c>50</c> = +50%）。&lt;= 0 时不做任何事。</param>
	/// <param name="cardSource">来源卡牌，可为 null。</param>
	public static async Task InjectAsync(
		PlayerChoiceContext choiceContext,
		Creature owner,
		decimal percentPoints,
		CardModel? cardSource)
	{
		if (percentPoints <= 0m)
		{
			return;
		}

		await PowerCmd.Apply<HeidemarieDamagePercentPower>(
			choiceContext,
			owner,
			percentPoints,
			owner,
			cardSource,
			silent: true);
	}

	/// <summary>
	/// 清池：把整池清空（所有挂起的百分比增伤一次性消费掉）。
	/// 没有挂载时为空操作。
	/// </summary>
	public static Task ClearAsync(Creature owner)
		=> PowerCmd.Remove<HeidemarieDamagePercentPower>(owner);
}
