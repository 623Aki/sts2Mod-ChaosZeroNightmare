using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色遗物池。
///
/// <para>最小闭环阶段直接采用战士的默认遗物「燃烧之血」：既作为初始遗物，
/// 也放进池子里占位，保证池子非空。后续换成 Heidemarie 专属遗物时，
/// 只需替换 <see cref="GenerateAllRelics"/> 的返回值。</para>
/// </summary>
public sealed class HeidemarieRelicPool : RelicPoolModel
{
	/// <summary>
	/// 能量图标配色键。与卡池 / 药水池保持一致，使用自研的 <c>heidemarie</c>，
	/// 对应 <c>res://images/atlases/ui_atlas.sprites/card/energy_heidemarie.tres</c>。
	/// </summary>
	public override string EnergyColorName => "heidemarie";

	/// <inheritdoc/>
	public override Color LabOutlineColor => new Color("C8B6E2");

	/// <inheritdoc/>
	protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[1]
	{
		ModelDb.Relic<BurningBlood>(),
	};
}
