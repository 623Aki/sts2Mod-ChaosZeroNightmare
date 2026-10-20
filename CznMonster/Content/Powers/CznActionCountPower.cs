using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznMonster.Content.Powers;

/// <summary>
/// 行动计数显示 Power（设计文档 §7.7，D12：v1 即做）：
/// 纯显示，无任何钩子副作用；用 Buff 是因为 Debuff 会被 Artifact 挡掉，显示层不能被打断。
/// </summary>
public sealed class CznActionCountPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	// 纯显示计数，不播能力特效（自定义 Power 没有对应的 vfx 资源）
	public override bool ShouldPlayVfx => false;

	// 联机倍率已在计数器里乘过（§7.9），这里不再缩放
	public override bool ShouldScaleInMultiplayer => false;
}
