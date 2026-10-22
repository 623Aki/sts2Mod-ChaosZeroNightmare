using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace CznMonster.Content.Powers;

/// <summary>
/// 阿涅丝的「共鸣」（CZN cs02_2105/cs02_2088 计数器适配；v0.7 重构为「只增的威胁刻度」）：
///
/// <para><b>只增不减</b>——普通攻击既不吃也不消耗它，因此二阶段的每一刀都能被玩家精确预估，
/// 不存在「意图锁定时一个数、执行时另一个数」的漂移。</para>
///
/// <para><b>增长源只有两个</b>：变身 / 处决后重铺 <see cref="RebaseAmount"/> 层；
/// 压制拍（蓄力）+2 层（见 <c>Agnes.SuppressMove</c>）。上限 10 由
/// <c>Agnes.AddResonance</c> 统一封顶。</para>
///
/// <para><b>唯一释放口 = 处决</b>：伤害 = 基础 × (1 + 共鸣 + 2×场上碎片数)，随后清零重铺 3。
/// 余响碎片<b>不计入</b>本能力的层数——它们的价值以「每只 +2 系数」直接写进处决公式，
/// 因此「玩家打死碎片」与「Boss 自己收割」两路都不需要额外联动，天然不会双计
/// （v0.7b 删除原 <c>AfterDeath</c> 联动与处决窗口：击杀碎片从此是纯收益，
/// 不会再出现「花 18 点输出只换 1 层共鸣」的负收益交换）。</para>
///
/// <para>本能力只做显示与查询，层值写回走 <c>SetAmount</c>
/// （<see cref="PolemicaBroodPower"/> 同款：显示与模型内部字段同步）。</para>
/// </summary>
public sealed class AgnesResonancePower : PowerModel
{
	/// <summary>变身 / 处决两个唯一入口重铺的层数（每轮循环从 3 层重新起算）。</summary>
	public const int RebaseAmount = 3;

	/// <inheritdoc />
	/// <remarks>用 Buff：不会被 Artifact 挡掉（同 <see cref="PolemicaBroodPower"/> 的理由）。</remarks>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc />
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc />
	/// <remarks>纯显示计数，不播能力特效（自定义 Power 没有对应的 vfx 资源）。</remarks>
	public override bool ShouldPlayVfx => false;

	/// <inheritdoc />
	/// <remarks>层数只随 Boss 自身状态变化，联机时不随玩家数缩放。</remarks>
	public override bool ShouldScaleInMultiplayer => false;
}
