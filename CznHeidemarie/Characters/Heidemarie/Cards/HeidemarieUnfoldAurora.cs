using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「展开极光」：2 费技能（公共牌）。
///
/// <para>关键字<b>安息</b>。在手牌生成 <c>Cards</c> 张极光剑，
/// 并挂载能力<b>极夜</b>，使本回合内极光剑伤害 +50%。</para>
/// </summary>
public sealed class HeidemarieUnfoldAurora : HeidemarieCard, IRestCard
{
	/// <summary>「极夜」提供的增伤百分点。</summary>
	private const decimal NightBoost = 50m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(2),
	};

	/// <summary>2 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieUnfoldAurora()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Generate(choiceContext);

	private async Task Generate(PlayerChoiceContext choiceContext)
	{
		await HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Hand);
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			NightBoost,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
