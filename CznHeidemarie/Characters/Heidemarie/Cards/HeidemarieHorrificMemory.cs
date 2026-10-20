using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「骇人的记忆」：1 费<b>状态牌</b>，<b>消耗</b>。抽 1 张牌；全体敌人获得 1 点力量。
///
/// <para><b>类型口径（设计 Q4 = A）</b>：<c>CardType.Status</c> + <c>CardRarity.Status</c>。
/// 主设计表把「状态牌」填在稀有度列，落地按官方的两轴枚举拆开。</para>
///
/// <para><b>可打出（设计 Q5 = A）</b>：花 1 费把它从手里清掉，代价是白给敌人 1 点力量；
/// 若不清，它会一直占着手牌位置。</para>
///
/// <para>只登记在 <see cref="HeidemarieTokenPool"/>（衍生牌池），不进角色卡池，
/// 因此不会被奖励 / 商店 / 战斗随机生成抽到 —— 唯一来源是「噩梦的另一面」。</para>
/// </summary>
public sealed class HeidemarieHorrificMemory : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new CardsVar(1),
		// 给全体敌人加的力量
		new DynamicVar("Strength", 1m),
	};

	/// <summary>1 费、状态牌、状态稀有度、目标为自身。</summary>
	public HeidemarieHorrificMemory()
		: base(1, CardType.Status, CardRarity.Status, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <inheritdoc/>
	/// <remarks>状态牌不可升级。</remarks>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

		// 给「敌人」加力量 —— 对玩家是纯负面，这正是这张状态牌的代价
		if (CombatState is not { } combatState || combatState.HittableEnemies.Count == 0)
		{
			return;
		}

		await PowerCmd.Apply<StrengthPower>(
			choiceContext,
			combatState.HittableEnemies,
			DynamicVars["Strength"].BaseValue,
			Owner.Creature,
			this);
	}
}
