using System.Collections.Generic;
using CznHeidemarie.Characters.Heidemarie.Relics;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色遗物池。
///
/// <para>当前只放<b>初始遗物</b>「市长铭牌」（<see cref="HeidemarieMayorBadge"/>）。
/// 它是 <c>RelicRarity.Starter</c>，稀有度掷点只会产出 Common / Uncommon / Rare，
/// 因此登记在这里不会让它作为奖励 / 商店货出现 —— 放进池子的真正理由有两个：</para>
/// <list type="number">
///   <item><c>RelicModel.Pool</c> 要求「该遗物至少属于一个池」，否则读取时会抛异常；</item>
///   <item>让 <c>ModelDb.AllRelics</c> 与图鉴能正常枚举到它。</item>
/// </list>
///
/// <para>后续新增可获取遗物时，往 <see cref="GenerateAllRelics"/> 里追加即可。</para>
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
		ModelDb.Relic<HeidemarieMayorBadge>(),
	};
}
