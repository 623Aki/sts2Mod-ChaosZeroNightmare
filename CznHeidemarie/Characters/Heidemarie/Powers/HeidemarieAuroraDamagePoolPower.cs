using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「极光剑增伤区」的<strong>来源登记 + 汇总器</strong>：目前唯一来源是<b>极夜</b>。
///
/// <para><b>它不再是乘算 modifier</b>（设计 Q4 = 合并）：本能力只负责
/// <list type="number">
///   <item>记录「极光剑增伤区已启用」（<see cref="PowerStackType.Single"/> 的标记层）；</item>
///   <item>把各来源的百分点<b>相加</b>后交给唯一乘算出口
///   <see cref="HeidemarieDamagePercentPower"/>（它只在
///   <c>cardSource</c> 是极光剑时计入这部分）。</item>
/// </list>
/// 因此「极夜」与「百分比增伤池（含极光连锁基线）」全部走同一条
/// 加法链，不会退化成引擎乘算区的连乘。</para>
///
/// <para><b>生命周期</b>：由极夜在获得时「确保存在」
/// （<see cref="EnsureAsync"/>，幂等，同时确保百分比增伤池已挂载），
/// 不自行消失；极夜归零时 <see cref="TotalPercent"/> 恒为 0，不产生任何效果，
/// 战斗结束由引擎统一清理。</para>
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
	/// 确保「极光剑增伤区」与「百分比增伤池」都已挂载（幂等）。
	///
	/// <para>由 <see cref="HeidemarieNightPower"/> 在首次获得时调用。</para>
	/// </summary>
	public static async Task EnsureAsync(Creature owner)
	{
		if (owner.GetPower<HeidemarieAuroraDamagePoolPower>() == null)
		{
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

		// 真正的乘算出口是百分比增伤池：没有它，极光剑加成无处可加。
		await HeidemarieDamagePercentPower.EnsureMountedAsync(owner);
	}

	/// <summary>
	/// 极光剑增伤区当前的总百分点（唯一来源：极夜）。
	///
	/// <para><b>极夜按「有层数就 +100%」计</b>：每层代表「下一张剑 +100%」，
	/// 不论还剩几层，单张剑吃到的都是固定 <see cref="HeidemarieNightPower.PercentPerLayer"/>
	/// 个百分点 —— 层数只决定「还能强化几张」，不叠加在同一张剑上。</para>
	///
	/// <para>「充能极光」已按设计移除：它原本提供的固定 +100% 改由<b>极夜层数</b>表达
	/// （给 1 层极夜 = 下一张剑 +100%），因此本汇总器只剩一个来源。</para>
	/// </summary>
	public static decimal TotalPercent(Creature owner)
	{
		return owner.GetPowerAmount<HeidemarieNightPower>() > 0
			? HeidemarieNightPower.PercentPerLayer
			: 0m;
	}
}
