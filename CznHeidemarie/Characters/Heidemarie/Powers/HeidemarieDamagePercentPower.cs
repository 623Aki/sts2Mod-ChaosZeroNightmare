using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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
/// <para>生命周期：无常驻意义 —— 任意一次「本体所属玩家」的攻击结算完
/// （<see cref="AfterAttack"/>）即整池清空，对应「下一张攻击牌 +50%」的语义。
/// 绝响 / 解放极光则在自身结算前临时注入。</para>
///
/// <para>本能力对玩家不可见（<see cref="IsVisibleInternal"/> 返回 false），
/// 不占战斗界面图标。</para>
/// </summary>
public sealed class HeidemarieDamagePercentPower : PowerModel
{
	/// <summary>「剑光」感应提供的增伤百分点。</summary>
	public const decimal SenseBoost = 50m;

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

	/// <inheritdoc/>
	/// <remarks>一次攻击结算完即清空整池（所有挂起的百分比增伤都被这一次攻击消费掉）。</remarks>
	public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		if (command.Attacker != Owner || !command.DamageProps.IsPoweredAttack())
		{
			return;
		}

		await PowerCmd.Remove(this);
	}
}
