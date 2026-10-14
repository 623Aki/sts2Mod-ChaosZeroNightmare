using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「归墟剑骸」：2 费技能（公共牌）。
///
/// <para>关键字<b>连接</b>、<b>安息</b>。在弃牌堆生成 <c>Cards</c> 张极光剑。</para>
/// </summary>
public sealed class HeidemarieSwordCrypt : HeidemarieCard, ILinkCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(4),
	};

	/// <summary>2 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieSwordCrypt()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	/// <remarks>「连接」的连带丢弃由 <see cref="HeidemarieCard.BeforeCardPlayed"/> 统一在出牌前完成。</remarks>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await Generate(choiceContext);
	}

	private Task Generate(PlayerChoiceContext choiceContext)
		=> HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Discard);

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(3m);
}
