using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「极光剑增伤区」的<strong>唯一乘算出口</strong>：白昼 + 极夜 在这里<strong>相加</strong>后统一提供因子。
///
/// <para><b>为什么需要它</b>：引擎的乘算区是连乘
/// （<c>Hook.ModifyDamageInternal</c>：<c>num *= num3</c>），若白昼与极夜各挂一个乘算 modifier，
/// 两者同时存在时结果会变成 <c>(1+白昼%) × (1+极夜%)</c>；
/// 而设计要的是 <c>1 + (白昼 + 极夜)/100</c>。</para>
///
/// <para><b>做法</b>：本能力<strong>不保存</strong>增伤数值（<see cref="MegaCrit.Sts2.Core.Models.PowerModel.Amount"/>
/// 恒为 1，只当「该区已启用」的标记），每次结算时实时向白昼 / 极夜查询各自百分点并相加。
/// 于是白昼与极夜之间不再需要「谁让位给谁」的互相判定 ——
/// 它们只保留各自的层数与生命周期（白昼打出极光剑后清、极夜回合结束清）。</para>
///
/// <para><b>生命周期</b>：由白昼 / 极夜在获得时「确保存在」（<see cref="EnsureAsync"/>，幂等），
/// 不自行消失；两者都不在时因子恒为 1（不产生任何效果），战斗结束由引擎统一清理。</para>
///
/// <para>对玩家不可见（<see cref="IsVisibleInternal"/> 返回 false），不占战斗界面图标。</para>
/// </summary>
public sealed class HeidemarieAuroraDamagePoolPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>纯机制标记，不叠加。</remarks>
	public override PowerStackType StackType => PowerStackType.Single;

	/// <inheritdoc/>
	/// <remarks>纯机制能力，不在战斗界面显示图标。</remarks>
	protected override bool IsVisibleInternal => false;

	/// <summary>
	/// 确保「极光剑增伤区」已启用（幂等）：已存在时不做任何事。
	///
	/// <para>由 <see cref="HeidemarieDaylightPower"/> / <see cref="HeidemarieNightPower"/>
	/// 在首次获得时调用；两者谁先到都行。</para>
	/// </summary>
	public static async Task EnsureAsync(Creature owner)
	{
		if (owner.GetPower<HeidemarieAuroraDamagePoolPower>() != null)
		{
			return;
		}

		// 该钩子不携带 PlayerChoiceContext；启用标记不需要玩家选择，
		// 用一个「不阻塞其他玩家」的空上下文即可（与基类关键字派发同一做法）。
		await PowerCmd.Apply<HeidemarieAuroraDamagePoolPower>(
			new BlockingPlayerChoiceContext(),
			owner,
			1m,
			owner,
			null,
			silent: true);
	}

	/// <summary>极光剑增伤区当前的总百分点（白昼 + Σ极夜），供计算与显示共用。</summary>
	public static decimal TotalPercent(Creature owner)
		=> (decimal)owner.GetPowerAmount<HeidemarieDaylightPower>()
			+ owner.GetPowerInstances<HeidemarieNightPower>().Sum(power => (decimal)power.Amount);

	/// <inheritdoc/>
	/// <remarks>
	/// 只对「本体所属玩家」打出的「极光剑」强化攻击生效 —— 白昼 / 极夜的设计都限定极光剑。
	/// 整个战斗只有本能力一个 modifier 参与这一区，因此天然是相加而非连乘。
	/// </remarks>
	public override decimal ModifyDamageMultiplicative(
		Creature? target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource,
		CardPlay? cardPlay)
	{
		if (dealer != Owner || cardSource is not HeidemarieAuroraSword || !props.IsPoweredAttack())
		{
			return 1m;
		}

		decimal total = TotalPercent(Owner);
		return total == 0m ? 1m : 1m + total / 100m;
	}
}
