using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「白昼」：使**下一张极光剑**的伤害提高 <see cref="PowerModel.Amount"/>%。
///
/// <para><b>因子由「极光剑增伤区」统一提供</b>（见 <see cref="HeidemarieAuroraDamagePoolPower"/>）：
/// 本能力只保存层数与生命周期，<strong>不再自己 override 乘算 modifier</strong>。
/// 白昼与极夜的百分比在该能力处<strong>相加</strong>，因此两者同时存在时
/// 不会退化成引擎乘算区的连乘 <c>(1+白昼)(1+极夜)</c>。</para>
///
/// <para>只作用于 <see cref="HeidemarieAuroraSword"/>，对随机伤害同样生效；
/// 打出极光剑后清除。</para>
/// </summary>
public sealed class HeidemarieDaylightPower : PowerModel
{
	/// <summary>「剑光」感应提供的增伤百分点。</summary>
	public const decimal SenseBoost = 50m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>首次获得时确保「极光剑增伤区」已启用（幂等，重复获得不会重复挂载）。</remarks>
	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
		=> HeidemarieAuroraDamagePoolPower.EnsureAsync(Owner);

	/// <inheritdoc/>
	/// <remarks>打出极光剑后清除（下一次极光剑不再享受）。</remarks>
	public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		if (command.Attacker == Owner && command.ModelSource is HeidemarieAuroraSword)
		{
			await PowerCmd.Remove(this);
		}
	}
}
