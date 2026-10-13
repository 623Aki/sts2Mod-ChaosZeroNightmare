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
	/// 能量图标配色键。与卡池保持一致，取游戏已有的 <c>ironclad</c>，
	/// 避免拼出不存在的 <c>ui_atlas.sprites/card/energy_heidemarie.tres</c>
	/// 而触发 "Missing sprite" 警告（ui_atlas 无 fallback）。
	/// </summary>
	public override string EnergyColorName => "ironclad";

	/// <inheritdoc/>
	public override Color LabOutlineColor => new Color("C8B6E2");

	/// <inheritdoc/>
	protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[1]
	{
		ModelDb.Relic<BurningBlood>(),
	};
}
