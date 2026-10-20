using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「背靠背」：2 费技能（稀有牌）。
///
/// <para>全体友方获得 <c>Dexterity</c> 层<b>敏捷</b>；然后打出手牌中费用最高的 <c>Play</c> 张牌。</para>
///
/// <para><b>「敏捷」是官方能力</b>（<see cref="DexterityPower"/>，中文卡面名「敏捷」）——
/// 效果是「从卡牌中获得的格挡增加 X 点」（<c>DEXTERITY_POWER.description</c>）。
/// 因此这是本角色第一张<strong>给队友上敏捷</strong>的联机牌：
/// 队友所有带格挡的牌都会变厚，配合「背靠背」后半段的自动出牌形成
/// 「先加固 → 再铺场」的节奏。</para>
///
/// <para><b>自动打出不触发「连接」的连带丢弃</b>：与「万剑齐鸣」「剑冢」同一口径 ——
/// 自动打出不算「直接使用此卡」，期间用
/// <see cref="HeidemarieCardActions.SuppressLinkDiscard"/> 抑制，
/// 否则第一张连接牌就会把剩下的连接牌全丢掉（本该打出的 2 张变成 1 张）。</para>
/// </summary>
public sealed class HeidemarieBackToBack : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		// 全体友方各获得几点「敏捷」
		// （PowerVar：卡面预览会走 Hook.ModifyPowerAmountGiven，与 DexterityPotion 同款写法）
		new PowerVar<DexterityPower>("Dexterity", 2m),
		// 从手牌自动打出几张
		new DynamicVar("Play", 2m),
	};

	/// <summary>2 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieBackToBack()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		// 敏捷自身的悬停提示已含「增加卡牌格挡」语义，不再另挂 Block 静态提示
		HoverTipFactory.FromPower<DexterityPower>(),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (CombatState is not { } combatState)
		{
			return;
		}

		// ① 全体友方（所有玩家，含自己）获得「敏捷」
		IReadOnlyList<Creature> allies = combatState.Players
			.Where(player => player.Creature != null)
			.Select(player => player.Creature)
			.ToList();

		if (allies.Count > 0)
		{
			await PowerCmd.Apply<DexterityPower>(
				choiceContext,
				allies,
				DynamicVars["Dexterity"].BaseValue,
				Owner.Creature,
				this);
		}

		// ② 打出手牌里费用最高的若干张 —— 先排序快照，边打边算会漏牌
		List<CardModel> highest = PileType.Hand.GetPile(Owner).Cards
			.OrderByDescending(c => c.EnergyCost.GetWithModifiers(CostModifiers.All))
			.Take(DynamicVars["Play"].IntValue)
			.ToList();

		if (highest.Count == 0)
		{
			return;
		}

		// 进/出各存一次原值：被自动打出的牌里可能有「剑冢」，它也会置位抑制标记
		bool previousSuppress = HeidemarieCardActions.SuppressLinkDiscard;
		HeidemarieCardActions.SuppressLinkDiscard = true;
		try
		{
			foreach (CardModel card in highest)
			{
				await CardCmd.AutoPlay(choiceContext, card, null);
			}
		}
		finally
		{
			HeidemarieCardActions.SuppressLinkDiscard = previousSuppress;
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
