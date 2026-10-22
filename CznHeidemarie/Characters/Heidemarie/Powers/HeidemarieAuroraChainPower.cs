using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「极光连锁」：每当你<strong>直接使用</strong>或<strong>丢弃</strong> 1 张带「连接」的牌，
/// 获得 1 层（最多 <see cref="MaxStacks"/> 层）；每层使你的攻击伤害
/// +<see cref="PercentPerStack"/>%。
///
/// <para><b>增伤通道（设计 Q4 = 合并）</b>：层数不自己挂乘算 modifier，
/// 而是作为「百分比增伤池」（<see cref="HeidemarieDamagePercentPower"/>）的
/// <b>持久基线</b>由池实时读取 —— 与一次性注入的百分比、极光剑增伤区<b>相加</b>，
/// 因此不会退化成引擎乘算区的连乘。</para>
///
/// <para><b>触发判定</b>全部走 <see cref="HeidemarieCardActions.IsLinkTrigger"/> /
/// <see cref="HeidemarieCardActions.IsLinkDiscard"/>（与遗物「赤光之誓」共用）：
/// 自动打出（剑冢重放 / 万剑齐鸣 / 长眠之唤）<b>不算直接使用</b>。</para>
///
/// <para><b>上限处理</b>：先判层数再 <c>PowerCmd.Apply</c>，
/// 不靠 Apply 之后截断（§7.3 #10）。</para>
/// </summary>
public sealed class HeidemarieAuroraChainPower : PowerModel
{
	/// <summary>层数上限（对应 +50% 攻击伤害）。</summary>
	public const int MaxStacks = 10;

	/// <summary>每层提供的增伤百分点。</summary>
	public const decimal PercentPerStack = 5m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>
	/// 获得本能力时确保「百分比增伤池」已挂载 —— 基线由它读取，
	/// 没有池就没有乘算出口（池本身不可见）。
	/// </remarks>
	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
		=> HeidemarieDamagePercentPower.EnsureMountedAsync(Owner);

	/// <inheritdoc/>
	/// <remarks>「直接使用」1 张连接牌。</remarks>
	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner.Player is not { } player || !HeidemarieCardActions.IsLinkTrigger(cardPlay, player))
		{
			return;
		}

		await GainStackAsync(choiceContext);
	}

	/// <inheritdoc/>
	/// <remarks>「丢弃」1 张连接牌（不含回合末清手牌，那条路径不派发本钩子）。</remarks>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (Owner.Player is not { } player || !HeidemarieCardActions.IsLinkDiscard(card, player))
		{
			return;
		}

		await GainStackAsync(choiceContext);
	}

	/// <summary>获得 1 层（到顶后不再增加）。</summary>
	private async Task GainStackAsync(PlayerChoiceContext choiceContext)
	{
		if (Amount >= MaxStacks)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<HeidemarieAuroraChainPower>(
			choiceContext,
			Owner,
			1m,
			Owner,
			null,
			silent: true);
	}
}
