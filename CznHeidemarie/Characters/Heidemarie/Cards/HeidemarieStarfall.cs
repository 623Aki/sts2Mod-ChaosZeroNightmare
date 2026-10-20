using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「星落」：1 费技能（罕见牌），关键字<b>安息</b>。
///
/// <para>随机丢弃抽牌堆中的 <c>Discard</c> 张<b>安息</b>牌。</para>
///
/// <para>玩法上是「安息引擎的点火器」：把抽牌堆里的「安魂剑 / 展开极光 / 积羽 / 拾星」
/// 之类安息牌直接弃掉，让它们<strong>不入手就发动</strong>，等于用 1 费换一次免费的安息结算。
/// 与「星轨」（把带连接的牌捞进手）方向相反：一个是捞，一个是直接点。</para>
///
/// <para>⚠️ 被丢的牌自己带「安息」，所以丢弃时它们会连锁触发各自的效果 ——
/// 这是设计意图（连锁才是收益）；本牌自己已在打出时离开手牌，因此不会自我递归。</para>
/// </summary>
public sealed class HeidemarieStarfall : HeidemarieCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 丢弃的张数（升级 +1）
		new DynamicVar("Discard", 1m),
	};

	/// <summary>1 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieStarfall()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> FerryRestCards(choiceContext);

	/// <inheritdoc/>
	/// <remarks>关键字「安息」：被丢弃时发动同一效果。</remarks>
	public Task OnRest(PlayerChoiceContext choiceContext) => FerryRestCards(choiceContext);

	/// <summary>打出与安息共用的效果：随机丢弃抽牌堆里的安息牌。</summary>
	private async Task FerryRestCards(PlayerChoiceContext choiceContext)
	{
		// 快照：筛出安息牌后逐步摘除，避免重复选中同一张
		List<CardModel> pool = PileType.Draw.GetPile(Owner).Cards
			.Where(c => c is HeidemarieCard holder && holder.IsRestCard)
			.ToList();

		List<CardModel> picked = new();
		for (int i = 0; i < DynamicVars["Discard"].IntValue && pool.Count > 0; i++)
		{
			CardModel? card = Owner.RunState.Rng.CombatCardSelection.NextItem(pool);
			if (card == null)
			{
				break;
			}

			picked.Add(card);
			pool.Remove(card);
		}

		if (picked.Count > 0)
		{
			// 批量弃牌：禁止循环单张（Sly 时机 + 安息连锁的顺序都靠这一次调用保证）
			await CardCmd.Discard(choiceContext, picked);
		}
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars["Discard"].UpgradeValueBy(1m);
}
