using System.Collections.Generic;
using CznHeidemarie.Characters.Heidemarie.Relics;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色遗物池。
///
/// <para>当前放两件：初始遗物「市长铭牌」（<see cref="HeidemarieMayorBadge"/>，
/// <c>RelicRarity.Starter</c>）与可获取遗物「和平奖章」
/// （<see cref="HeidemariePeaceMedal"/>，<c>RelicRarity.Rare</c>，
/// 可出现在商城 / 奖励）。</para>
///
/// <para>「市长铭牌」虽是 Starter 稀有度、不参与奖励 / 商店掷点，仍须登记在池子里，
/// 理由有两个：</para>
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
	protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[2]
	{
		ModelDb.Relic<HeidemarieMayorBadge>(),
		ModelDb.Relic<HeidemariePeaceMedal>(),
	};
}
