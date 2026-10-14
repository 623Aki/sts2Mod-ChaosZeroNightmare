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
	/// <para>本值会被
	/// <see cref="MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPath(string)"/> 拼成
	/// <c>res://images/atlases/ui_atlas.sprites/card/energy_&lt;值&gt;.tres</c>，
	/// 用于卡牌费用与遗物 / 药水提示中的能量图标。</para>
	///
	/// <para><b>注意：这条图集路径在本工程里会被游戏 <c>AtlasResourceLoader</c> 拦截</b>
	/// （<c>ui_atlas</c> 内无本角色 sprite，且无 fallback，会回落到
	/// <c>missing_power.png</c>）。因此真正生效的是
	/// <see cref="Registration.HeidemarieEnergyIconPathPatch"/> 这条 Postfix 补丁，
	/// 它把本配色键重定向到 <c>images/ui/energy/heidemarie_energy_card_74.png</c>。
	/// 改本值时必须同步改补丁里的配色键。</para>
	///
	/// <para>卡池 / 遗物池 / 药水池三处必须严格一致，否则同一次提示里会出现两种图标。</para>
	/// </summary>
	public override string EnergyColorName => "heidemarie";

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
	/// 只列举「可常规获取」的卡：基础攻防 + 全部角色卡。
	/// 衍生的极光剑、凝聚极光、解放极光不进池，见类注释。
	/// </remarks>
	protected override CardModel[] GenerateAllCards() => new CardModel[25]
	{
		// 基础攻防
		ModelDb.Card<HeidemarieStrike>(),
		ModelDb.Card<HeidemarieDefend>(),
		// 原有角色卡
		ModelDb.Card<HeidemarieSwordlight>(),
		ModelDb.Card<HeidemarieSwordrain>(),
		ModelDb.Card<HeidemarieSwordTomb>(),
		ModelDb.Card<HeidemarieCondenseEdge>(),
		ModelDb.Card<HeidemarieRequiemBlade>(),
		ModelDb.Card<HeidemarieFinalEcho>(),
		// 新角色卡（连接 / 极光引擎扩展）
		ModelDb.Card<HeidemarieAllHeroes>(),
		ModelDb.Card<HeidemarieBindStar>(),
		ModelDb.Card<HeidemarieFaintLight>(),
		ModelDb.Card<HeidemarieRequiem>(),
		ModelDb.Card<HeidemarieRecover>(),
		ModelDb.Card<HeidemarieCallBlade>(),
		ModelDb.Card<HeidemarieRayOfLight>(),
		ModelDb.Card<HeidemarieTripleGlow>(),
		ModelDb.Card<HeidemarieResonance>(),
		ModelDb.Card<HeidemarieSkyfall>(),
		ModelDb.Card<HeidemarieUnfoldAurora>(),
		ModelDb.Card<HeidemarieNorthPolar>(),
		ModelDb.Card<HeidemarieSwordCrypt>(),
		ModelDb.Card<HeidemarieMirrorBlades>(),
		ModelDb.Card<HeidemarieStoreLight>(),
		ModelDb.Card<HeidemarieFallenStar>(),
		ModelDb.Card<HeidemarieForesee>(),
	};
}
