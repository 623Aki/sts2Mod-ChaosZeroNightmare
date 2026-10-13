using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「剑冢」：2 费技能（公共牌），<strong>非指向</strong>（<c>TargetType.None</c>，出牌不选目标）。
///
/// <para>从墓地中打出最多 5 张「极光剑」；升级后费用 -1。</para>
/// <para>极光剑本身是随机目标（<see cref="HeidemarieAuroraSword"/> 为
/// <c>TargetType.RandomEnemy</c>），因此本牌不再需要指定敌人 ——
/// 每张被重放的极光剑各自随机命中一名敌人。</para>
/// <para>极光剑带「守墓」，会一直留在墓地，因此本牌可以反复利用它们。</para>
/// </summary>
public sealed class HeidemarieSwordTomb : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 最多打出几张极光剑
		new DynamicVar("Swords", 5m),
	};

	/// <summary>2 费、技能、公共牌、非指向。</summary>
	public HeidemarieSwordTomb()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.None)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await HeidemarieCardActions.PlayAuroraSwordsFromDiscard(
			this,
			choiceContext,
			DynamicVars["Swords"].IntValue);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
	}
}
