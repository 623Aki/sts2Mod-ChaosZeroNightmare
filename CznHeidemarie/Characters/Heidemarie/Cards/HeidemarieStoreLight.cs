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
/// 「积羽」：2 费技能（稀有牌）。
///
/// <para>关键字<b>安息</b>：在弃牌堆生成 <c>Cards</c> 张极光剑。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，获得能力<b>白昼</b>，使下一张极光剑伤害 +100%。</para>
/// </summary>
public sealed class HeidemarieStoreLight : HeidemarieCard, IRestCard, ISenseCard
{
	/// <summary>「白昼」提供的增伤百分点。</summary>
	private const decimal SenseBoost = 100m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(3),
	};

	/// <summary>2 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieStoreLight()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Generate(choiceContext);

	private Task Generate(PlayerChoiceContext choiceContext)
		=> HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Discard);

	/// <inheritdoc/>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await PowerCmd.Apply<HeidemarieDaylightPower>(
			choiceContext,
			Owner.Creature,
			SenseBoost,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(2m);
}
