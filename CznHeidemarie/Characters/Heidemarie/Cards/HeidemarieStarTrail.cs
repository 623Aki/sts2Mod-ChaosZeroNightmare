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
/// 「星轨」：1 费技能（罕见牌）。
///
/// <para>查看抽牌堆顶 <c>View</c> 张卡牌，把其中带<b>连接</b>的加入手牌，其余丢弃。</para>
///
/// <para>与「窥星择光」（看 3 选 1）互补的<b>定向捞牌</b>：不需要玩家选择，
/// 有连接就捞、没有就丢；被丢掉的牌会触发「安息」等丢弃类效果。</para>
/// </summary>
public sealed class HeidemarieStarTrail : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 查看的张数（升级 +1）
		new DynamicVar("View", 4m),
	};

	/// <summary>1 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieStarTrail()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 先取快照：下面每处理一张都会改动手牌 / 抽牌堆，直接遍历活集合会抛异常。
		List<CardModel> top = PileType.Draw.GetPile(Owner).Cards
			.Take(DynamicVars["View"].IntValue)
			.ToList();

		if (top.Count == 0)
		{
			return;
		}

		foreach (CardModel card in top)
		{
			// ⚠️ 必须写类型判定：抽牌堆里可能有打击 / 防御 / 诅咒等非本角色卡，
			// 直接强转会崩。
			if (card is HeidemarieCard holder && holder.IsLinked)
			{
				await CardPileCmd.Add(card, PileType.Hand);
			}
			else
			{
				await CardCmd.Discard(choiceContext, card);
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 经 <c>DynamicVars</c> 索引器访问，避免升级预览克隆时改到与母本共享的变量对象
	/// （同 <see cref="HeidemarieForesee.OnUpgrade"/>）。
	/// </remarks>
	protected override void OnUpgrade() => DynamicVars["View"].UpgradeValueBy(1m);
}
