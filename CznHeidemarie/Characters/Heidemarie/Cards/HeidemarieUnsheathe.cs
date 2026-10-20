using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「出鞘」：1 费技能（公共牌）。
///
/// <para>抽 <c>Cards</c> 张牌；每抽到 1 张<b>攻击牌</b>，
/// 下一张极光剑的伤害提高 <c>Percent</c>%。</para>
///
/// <para><b>从 <c>CardPileCmd.Draw</c> 的返回值里数攻击牌</b>，而不是回头查手牌 ——
/// 手牌里原本就有攻击牌，重查会把「本来就在手里的」也算成「这次抽到的」，
/// 一张牌能白吃好几层增伤。</para>
///
/// <para>增伤走「白昼」池（<see cref="HeidemarieDaylightPower"/>）：卡面写「下一张极光剑」，
/// 与白昼同一生命周期（打出极光剑后清除），且与极夜相加而非连乘。</para>
/// </summary>
public sealed class HeidemarieUnsheathe : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new CardsVar(2),
		// 每抽到 1 张攻击牌提供的增伤百分点
		new DynamicVar("Percent", 100m),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieUnsheathe()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// Draw 返回的是 IEnumerable：立刻 ToList 落地，避免重复枚举
		List<CardModel> drawn = (await CardPileCmd.Draw(
			choiceContext,
			DynamicVars.Cards.IntValue,
			Owner)).ToList();

		int attackCount = drawn.Count(c => c.Type == CardType.Attack);
		if (attackCount <= 0)
		{
			return;
		}

		await PowerCmd.Apply<HeidemarieDaylightPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Percent"].BaseValue * attackCount,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
