using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「凝聚极光」：稀有技能，「无法使用」（不能主动打出）。
///
/// <para>原生关键字 <b>不能被打出</b>；自定义关键字 <b>唯一</b>、<b>连接</b>、<b>回收 3</b>。</para>
///
/// <para>它本身打不出去，靠「连接」被其它连接牌连带丢弃：每次移入墓地 →
/// 「极光之光」+1 → 「回收」把它送回手牌（回收 3 次）。三张极光之光叠满即触发
/// <see cref="HeidemarieAuroraLightPower"/> 的收束效果。</para>
///
/// <para>可升级 1 次，升级后获得原生关键字「固有」（战斗开始时直接入手）。
/// 实战中它由「解放极光」的焚烧生成，且生成时即已升级，因此通常自带「固有」。</para>
/// </summary>
public sealed class HeidemarieCondensedAurora : HeidemarieCard, IUniqueCard, ILinkCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Unplayable,
	};

	/// <inheritdoc/>
	/// <remarks>
	/// 卡面「极光之光」高亮（见本地化 <c>HEIDEMARIE_CONDENSED_AURORA.description</c>），
	/// 悬停时除关键字词条外再附加两个提示（做法参考原版「剥夺」给「灵魂」挂提示）：
	/// <list type="number">
	///   <item>能力「极光之光」—— 说明层数效果；</item>
	///   <item>卡牌「解放极光」预览 —— 说明攒满 3 层后实际会到手的牌。</item>
	/// </list>
	/// </remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new List<IHoverTip>(2)
	{
		HoverTipFactory.FromPower<HeidemarieAuroraLightPower>(),
		HoverTipFactory.FromCard<HeidemarieLiberatedAurora>(),
	};

	/// <inheritdoc/>
	/// <remarks>固有「回收 3」。</remarks>
	protected override int CanonicalRecycle => 3;

	/// <inheritdoc/>
	/// <remarks>
	/// 可升级 1 次：升级后获得原生关键字「固有」，
	/// 使本牌在战斗开始时直接进入手牌。
	/// </remarks>
	protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

	/// <summary>无能量消耗（不能被打出的牌）。</summary>
	public HeidemarieCondensedAurora()
		: base(-1, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	/// <inheritdoc/>
	/// <remarks>移入墓地时「极光之光 +1」，达标则触发收束（解放极光入手、清掉凝聚极光与全部极光之光）。</remarks>
	public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
	{
		if (card == this && card.Pile?.Type == PileType.Discard)
		{
			HeidemarieAuroraLightPower? power = await PowerCmd.Apply<HeidemarieAuroraLightPower>(
				new ThrowingPlayerChoiceContext(),
				Owner.Creature,
				1m,
				Owner.Creature,
				this,
				silent: true);

			if (power != null && power.Amount >= HeidemarieAuroraLightPower.Threshold)
			{
				await power.TriggerAsync(new ThrowingPlayerChoiceContext());
			}
		}

		// 交回基类处理「回收」（若触发收束已把它移出战斗，这里会因 Pile 非墓地而跳过）
		await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
	}
}
