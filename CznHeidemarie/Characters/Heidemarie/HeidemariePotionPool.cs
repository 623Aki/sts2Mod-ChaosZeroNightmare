using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色药水池。
///
/// <para>最小闭环阶段暂不投放角色专属药水：药水奖励由
/// <c>PotionFactory</c> 在本池之后追加共享药水池，因此空池不会影响奖励生成。</para>
/// </summary>
public sealed class HeidemariePotionPool : PotionPoolModel
{
	/// <summary>
	/// 能量图标配色键。与卡池 / 遗物池保持一致，使用自研的 <c>heidemarie</c>，
	/// 对应 <c>res://images/atlases/ui_atlas.sprites/card/energy_heidemarie.tres</c>。
	/// </summary>
	public override string EnergyColorName => "heidemarie";

	/// <inheritdoc/>
	public override Color LabOutlineColor => new Color("C8B6E2");

	/// <inheritdoc/>
	protected override IEnumerable<PotionModel> GenerateAllPotions() => new PotionModel[0];
}
