using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「长眠之唤」：0 费技能（罕见牌）。
///
/// <para>抽取 <c>Cards</c> 张牌；然后从<strong>弃牌堆</strong>选择 1 张带
/// <b>安息</b>的牌并<b>打出</b>它。</para>
///
/// <para><b>为什么只能选「上回合或更早」丢掉的安息牌</b>：带安息的牌被丢弃时会被
/// <see cref="HeidemarieRestLock"/> 锁在墓地，直到下个自己回合起手抽牌前才解锁。
/// 因此本回合刚丢的安息牌不在候选里 —— 这不是遗漏，而是安息自身的规则；
/// 候选过滤必须做在传进选择界面之前（选面板没有「不可选」谓词）。</para>
///
/// <para><b>重放不算「直接使用」</b>：从墓地打出走 <c>CardCmd.AutoPlay</c>，
/// 期间用 <see cref="HeidemarieCardActions.SuppressLinkDiscard"/> 抑制「连接」的连带丢弃，
/// 与「剑冢」同口径；极光连锁 / 赤光剑也因此不会被这次重放触发。</para>
/// </summary>
public sealed class HeidemarieRestCall : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 抽牌数（升级 +1）
		new CardsVar(1),
	};

	/// <summary>0 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieRestCall()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

		List<CardModel> candidates = PileType.Discard.GetPile(Owner).Cards
			.Where(card => card is HeidemarieCard { IsRestCard: true } holder
				&& !holder.RestLockedThisTurn)
			.ToList();

		if (candidates.Count == 0)
		{
			return;
		}

		CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
			choiceContext,
			candidates,
			Owner,
			new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();

		if (chosen == null)
		{
			return;
		}

		// 重放期间抑制「连接」的连带丢弃（进出各存一次原值，保证嵌套时能正确还原）
		bool previousSuppress = HeidemarieCardActions.SuppressLinkDiscard;
		HeidemarieCardActions.SuppressLinkDiscard = true;
		try
		{
			// 目标传 null：指向类牌由 AutoPlay 自动补一个随机敌人（同「剑冢」）
			await CardCmd.AutoPlay(choiceContext, chosen, null);
		}
		finally
		{
			HeidemarieCardActions.SuppressLinkDiscard = previousSuppress;
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
