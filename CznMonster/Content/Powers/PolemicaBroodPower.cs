using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content.Powers;

/// <summary>
/// 波勒米卡的「孵化仪式」（设计文档 §4.8）：
/// Boss 每行动一次 +1，满 <see cref="Threshold"/> 时下一次行动改为换防。
/// 本能力只做显示与查询，计数逻辑在 <c>Polemica</c> 内部字段中，两者同步。
/// </summary>
public sealed class PolemicaBroodPower : PowerModel
{
	/// <summary>触发换防所需的仪式层数。</summary>
	public const int Threshold = 3;

	/// <inheritdoc />
	/// <remarks>用 Buff：不会被 Artifact 挡掉，且这是首领自身的正面仪式，不是减益。</remarks>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc />
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc />
	/// <remarks>纯显示计数，不播能力特效（自定义 Power 没有对应的 vfx 资源）。</remarks>
	public override bool ShouldPlayVfx => false;

	/// <inheritdoc />
	/// <remarks>阈值固定 3，联机时不随玩家数缩放。</remarks>
	public override bool ShouldScaleInMultiplayer => false;
}
