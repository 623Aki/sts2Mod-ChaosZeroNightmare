using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 「北极圈」升级的**标记**能力：存在即表示其生成的极光剑带「安息」。
///
/// <para>纯标记、对玩家不可见。用于把「升级与否」这一信息带给
/// <see cref="HeidemarieNorthPolarPower"/>（能力本身无法读取卡牌的升级状态）。</para>
/// </summary>
public sealed class HeidemarieNorthPolarRestPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>纯机制标记，不在战斗界面显示图标。</remarks>
	protected override bool IsVisibleInternal => false;
}
