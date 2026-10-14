using CznHeidemarie.Characters.Heidemarie.Cards;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie;

/// <summary>
/// Heidemarie 的<strong>衍生牌池</strong>（极光剑 / 凝聚极光 / 解放极光）。
///
/// <para><b>为什么单独开一个池</b>：这三张牌不能进 <see cref="HeidemarieCardPool"/>
/// ——奖励 / 商店只按 <c>CardPoolModel.GetUnlockedCards</c> 抽牌，进了角色池就会
/// 被当成可获取卡。但完全不登记又会让它们从 <c>ModelDb.AllCards</c> 里消失，
/// 表现为：控制台 <c>card HEIDEMARIE_CONDENSED_AURORA</c> 报 not found、
/// 百科大全里看不到这三张牌。原版的衍生牌（如「飞刀」）就是放在
/// <c>TokenCardPool</c> 里的，这里照做。</para>
///
/// <para>本池由 <c>Registration.HeidemarieCompendiumPatch</c> 追加进
/// <c>ModelDb.AllSharedCardPools</c>，因此：
/// <list type="bullet">
///   <item>进 <c>ModelDb.AllCardPools</c> / <c>ModelDb.AllCards</c>（控制台、图鉴可见）；</item>
///   <item>图鉴里被视作「已解锁」，不会显示成未知卡；</item>
///   <item>不参与任何角色池抽取（奖励 / 商店 / 变形都不受影响）。</item>
/// </list></para>
///
/// <para>注意：这些牌的 <c>CardModel.Pool</c> 仍由 <see cref="HeidemarieCard"/> 指回
/// <see cref="HeidemarieCardPool"/>（卡面 / 能量图标取那个池），所以图鉴的角色筛选
/// 依然能命中它们。</para>
/// </summary>
public sealed class HeidemarieTokenPool : CardPoolModel
{
	/// <inheritdoc/>
	public override string Title => "heidemarie_token";

	/// <inheritdoc/>
	/// <remarks>与 <see cref="HeidemarieCardPool"/> 一致；改这里必须同步改能量图标补丁里的配色键。</remarks>
	public override string EnergyColorName => "heidemarie";

	/// <inheritdoc/>
	/// <remarks>与 <see cref="HeidemarieCardPool"/> 保持一致（本池的牌 <c>Pool</c> 也指回角色池）。</remarks>
	public override string CardFrameMaterialPath => "card_frame_heidemarie";

	/// <inheritdoc/>
	public override Color DeckEntryCardColor => new Color("C8B6E2");

	/// <inheritdoc/>
	public override Color EnergyOutlineColor => new Color("5B4A7A");

	/// <inheritdoc/>
	/// <remarks>沿用角色的配色，不当作无色池（否则能量图标 / 卡框会串到别的配色）。</remarks>
	public override bool IsColorless => false;

	/// <inheritdoc/>
	/// <remarks>
	/// 只列衍生牌：它们各自都设了 <c>CanBeGeneratedInCombat = false</c>，
	/// 且本池不会被任何抽取逻辑引用，因此不存在「随机变出极光剑」的途径。
	/// <para>「解放极光」的稀有度是<b>古代</b>（全幅立绘卡，见卡类注释），
	/// 但仍登记在本池里 —— 本池只负责让它出现在 <c>ModelDb.AllCards</c>（控制台 / 图鉴），
	/// 不参与任何按稀有度的抽取。</para>
	/// </remarks>
	protected override CardModel[] GenerateAllCards() => new CardModel[3]
	{
		ModelDb.Card<HeidemarieAuroraSword>(),
		ModelDb.Card<HeidemarieCondensedAurora>(),
		ModelDb.Card<HeidemarieLiberatedAurora>(),
	};
}
