using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的角色卡池。
///
/// <para>卡池内容直接在本类列举：<see cref="CardPoolModel.AllCards"/> 会先取
/// <see cref="GenerateAllCards"/>，再拼接各 mod 通过
/// <c>ModHelper.AddModelToPool</c> 追加的内容，因此这里必须给出完整列表。</para>
///
/// <para><b>哪些卡不进池</b>：奖励 / 商店只从本池里按稀有度挑选
/// （<c>CardFactory</c> 走 <c>CardPoolModel.GetUnlockedCards</c>）。
/// 因此<strong>不应被常规获取</strong>的卡一律不列在这里：</para>
/// <list type="bullet">
///   <item>「极光剑」—— 衍生牌（Token），只能由剑雨 / 凝锋 / 极光之光 生成；</item>
///   <item>「凝聚极光」—— 只能由解放极光的「焚烧」生成；</item>
///   <item>「解放极光」—— 只能由能力「极光之光」生成。</item>
/// </list>
/// 它们仍会被 <c>ModelDb</c> 扫描注册（<c>ModelDb.Card&lt;T&gt;()</c> 可用），
/// 只是不参与任何池内抽取。
/// </summary>
public sealed class HeidemarieCardPool : CardPoolModel
{
	/// <inheritdoc/>
	public override string Title => "heidemarie";

	/// <summary>
	/// 能量图标配色键。
	///
	/// <para>取游戏已有的 <c>ironclad</c>：本值会被
	/// <see cref="MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPath(string)"/> 拼成
	/// <c>res://images/atlases/ui_atlas.sprites/card/energy_&lt;值&gt;.tres</c>，
	/// 而游戏 ui_atlas 中没有 <c>energy_heidemarie</c>，且 ui_atlas 不参与
	/// AtlasResourceLoader 的 fallback，会持续刷 "Missing sprite" 并显示占位图标。
	/// 改用已有键即可消除。</para>
	/// </summary>
	public override string EnergyColorName => "ironclad";

	/// <inheritdoc/>
	public override string CardFrameMaterialPath => "card_frame_red";

	/// <inheritdoc/>
	public override Color DeckEntryCardColor => new Color("C8B6E2");

	/// <inheritdoc/>
	public override Color EnergyOutlineColor => new Color("5B4A7A");

	/// <inheritdoc/>
	public override bool IsColorless => false;

	/// <inheritdoc/>
	/// <remarks>
	/// 只列举「可常规获取」的卡：基础攻防 + 6 张角色卡
	/// （剑光 / 剑雨 / 剑冢 / 凝锋 / 安魂剑 / 绝响）。
	/// 衍生的极光剑、凝聚极光、解放极光不进池，见类注释。
	/// </remarks>
	protected override CardModel[] GenerateAllCards() => new CardModel[8]
	{
		// 基础攻防
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieDefend>(),
		// 角色卡（Basic / Common / Rare）
		ModelDb.Card<HeidemarieSwordlight>(),
		ModelDb.Card<HeidemarieSwordrain>(),
		ModelDb.Card<HeidemarieSwordTomb>(),
		ModelDb.Card<HeidemarieCondenseEdge>(),
		ModelDb.Card<HeidemarieRequiemBlade>(),
		ModelDb.Card<HeidemarieFinalEcho>(),
	};
}
