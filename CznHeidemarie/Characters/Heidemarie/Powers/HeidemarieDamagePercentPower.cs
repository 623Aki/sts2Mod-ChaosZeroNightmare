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
/// Heidemarie 的「百分比增伤池」—— <b>全角色唯一的乘算出口</b>。
///
/// <para><b>为什么只有一个乘算出口</b>：游戏的乘算区是<strong>连乘</strong>
/// （<c>Hook.ModifyDamageInternal</c>：<c>num *= num3</c>）。若「极夜 +100%」、
/// 「绝响 每剑 +100%」各做成一个 modifier，结果会变成
/// <c>(1+1) × (1+N)</c>。而设计要求所有百分比<strong>相加</strong>。</para>
///
/// <para><b>本轮合并（设计 Q4 = 是）</b>：原先「极光剑增伤区」另有独立 modifier，
/// 与本站形成连乘。现在两区合并为同一个出口，伤害因子统一为：</para>
/// <code>
/// 1 + ( 一次性注入 Amount
///     + 持久基线 PersistentPercent（极光连锁 层数×5）
///     + 极光剑专属加成（仅当 cardSource 是极光剑：极夜 + 充能极光） ) / 100
/// </code>
///
/// <para><b>三部分各自的职责</b>：</para>
/// <list type="bullet">
///   <item><b>一次性注入</b>（<see cref="Amount"/>）：由卡牌在攻击前
///   <see cref="InjectAsync"/> 注入，攻击结束后由调用方
///   <see cref="ClearAsync"/> 清回 0（推荐用基类
///   <see cref="HeidemarieCard.WithPercentDamageBoost"/>，它用 <c>try/finally</c> 保证清干净）；</item>
///   <item><b>持久基线</b>（<see cref="PersistentPercent"/>）：由
///   <see cref="HeidemarieAuroraChainPower"/> 的层数实时算出，<b>不落库</b> ——
///   避免「能力被移除但池里还留着基线」的残留；</item>
///   <item><b>极光剑加成</b>：只在 <paramref name="cardSource"/> 是
///   <see cref="HeidemarieAuroraSword"/> 时计入，由
///   <see cref="HeidemarieAuroraDamagePoolPower.TotalPercent"/> 汇总。</item>
/// </list>
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

	/// <summary>
	/// 持久基线（百分点）：来自「极光连锁」的 <c>层数 × 5</c>。
	///
	/// <para>实时向能力查询而不是存成本能力的字段：能力被移除 /
	/// 层数到顶 / 战斗结束清能力时，基线自动归零，不会留下幽灵增伤。</para>
	/// </summary>
	public decimal PersistentPercent
		=> Owner is null ? 0m
			: Owner.GetPowerAmount<HeidemarieAuroraChainPower>() * HeidemarieAuroraChainPower.PercentPerStack;

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

		decimal total = Amount + PersistentPercent;

		// 极光剑专属加成（极夜 + 充能极光）也在这一个 modifier 里相加。
		if (cardSource is HeidemarieAuroraSword)
		{
			total += HeidemarieAuroraDamagePoolPower.TotalPercent(Owner);
		}

		return total <= 0m ? 1m : 1m + total / 100m;
	}

	/// <summary>
	/// 确保本池已挂载（幂等）。挂载后一次性部分立即归零，
	/// 因此「挂上」这件事本身不产生任何增伤。
	/// </summary>
	/// <param name="owner">池的所属生物（一般是本角色）。</param>
	public static async Task EnsureMountedAsync(Creature owner)
	{
		if (owner.GetPower<HeidemarieDamagePercentPower>() != null)
		{
			return;
		}

		// 该钩子不携带 PlayerChoiceContext；挂载不需要玩家选择，
		// 用一个「不阻塞其他玩家」的空上下文即可（与基类关键字派发同一做法）。
		PlayerChoiceContext context = new BlockingPlayerChoiceContext();
		await PowerCmd.Apply<HeidemarieDamagePercentPower>(
			context,
			owner,
			1m,
			owner,
			null,
			silent: true);

		// 只为「挂载通道」：把刚加进来的 1 点归零。
		owner.GetPower<HeidemarieDamagePercentPower>()?.SetAmount(0, silent: true);
	}

	/// <summary>
	/// 入池：把 <paramref name="percentPoints"/> 个百分点累加进增伤池的一次性部分。
	///
	/// <para><b>调用方必须负责清池</b>：本方法只做「入池」。请与
	/// <see cref="ClearAsync"/> 成对使用（推荐直接用基类的
	/// <see cref="HeidemarieCard.WithPercentDamageBoost"/>，
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
	/// 清池：<b>只清一次性部分</b>（持久基线与极光剑加成不属于「这次攻击」）。
	///
	/// <para>当既没有持久基线、也没有极光剑加成来源时，整个能力卸下，
	/// 避免战斗里常驻一个空通道。</para>
	/// </summary>
	public static async Task ClearAsync(Creature owner)
	{
		HeidemarieDamagePercentPower? pool = owner.GetPower<HeidemarieDamagePercentPower>();
		if (pool == null)
		{
			return;
		}

		if (pool.Amount != 0)
		{
			pool.SetAmount(0, silent: true);
		}

		if (pool.PersistentPercent <= 0m && HeidemarieAuroraDamagePoolPower.TotalPercent(owner) <= 0m)
		{
			await PowerCmd.Remove(pool);
		}
	}
}
