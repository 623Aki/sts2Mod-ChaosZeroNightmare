using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「凝聚极光」：稀有技能，「无法使用」（不能主动打出）。
///
/// <para>原生关键字 <b>不能被打出</b>；自定义关键字 <b>唯一</b>、<b>链接</b>、<b>回收 3</b>。</para>
///
/// <para>它本身打不出去，靠「链接」被其它链接牌连带丢弃：每次移入墓地 →
/// 「极光之光」+1 → 「回收」把它送回手牌（回收 3 次）。三张极光之光叠满即触发
/// <see cref="HeidemarieAuroraLightPower"/> 的收束效果。</para>
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
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>固有「回收 3」。</remarks>
	protected override int CanonicalRecycle => 3;

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
