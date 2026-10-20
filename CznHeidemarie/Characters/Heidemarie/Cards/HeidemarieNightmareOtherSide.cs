using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「噩梦的另一面」：1 费技能（稀有牌），关键字<b>连接</b> + <b>安息</b>。
///
/// <para>抽 <c>Cards</c> 张牌；在<b>抽牌堆</b>生成 1 张状态牌「骇人的记忆」。</para>
///
/// <para>本角色的<strong>第一张「自己往牌堆里塞负面牌」</strong>的牌：
/// 抽牌是即时收益，「骇人的记忆」是延迟代价（抽到它要多花 1 费清掉，
/// 否则会白给全体敌人加力量）。带「安息」意味着被连接牌连带丢弃时照样发动 ——
/// 于是「弃牌 → 抽牌 → 塞垃圾」形成一条可玩的赌徒曲线。</para>
///
/// <para>状态牌用 <c>CreateCard</c> + <c>AddGeneratedCardToCombat</c> <b>生成</b>，
/// 不是把已有牌「加入」，因此不会消耗牌堆里现成的牌。</para>
/// </summary>
public sealed class HeidemarieNightmareOtherSide : HeidemarieCard, ILinkCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(2),
	};

	/// <summary>1 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieNightmareOtherSide()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Nightmare(choiceContext);

	/// <inheritdoc/>
	/// <remarks>关键字「安息」：被丢弃时发动同一效果。</remarks>
	public Task OnRest(PlayerChoiceContext choiceContext) => Nightmare(choiceContext);

	/// <summary>打出与安息共用的效果：抽牌 + 往抽牌堆塞 1 张「骇人的记忆」。</summary>
	private async Task Nightmare(PlayerChoiceContext choiceContext)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

		if (CombatState is not { } combatState)
		{
			return;
		}

		HeidemarieHorrificMemory memory = combatState.CreateCard<HeidemarieHorrificMemory>(Owner);
		await CardPileCmd.AddGeneratedCardToCombat(memory, PileType.Draw, Owner);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
