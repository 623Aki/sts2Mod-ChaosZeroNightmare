using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「光棱」：1 费技能（罕见牌），<b>消耗</b>。
///
/// <para>丢弃抽牌堆中的<b>所有</b>「极光剑」—— 被丢弃的剑照常结算「被丢弃时」的伤害，
/// 于是本牌是「从抽牌堆预支剑伤」的消费端：不入墓地、直接消耗。</para>
///
/// <para>⚠️ 与「这些都不要了」同样的两个坑（见其注释）：
/// 先 <c>ToList()</c> 快照再弃 —— 边遍历活集合边改堆会抛异常；
/// 且必须用批量重载 <c>CardCmd.Discard(ctx, IEnumerable)</c>，循环单张会错乱丢弃类效果的时序。</para>
/// </summary>
public sealed class HeidemarieLightPrism : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>1 费、技能、罕见牌、目标为自身、消耗。</summary>
	public HeidemarieLightPrism()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>关键字<b>消耗</b>：打出即进消耗堆，永不回手、不回墓地。</remarks>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Exhaust,
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 抽牌堆快照：先列全再弃 —— 边遍历活集合边改堆会抛异常（同「这些都不要了」）
		List<CardModel> swords = PileType.Draw.GetPile(Owner).Cards
			.Where(c => c is HeidemarieAuroraSword)
			.ToList();

		if (swords.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, swords);
	}

	/// <inheritdoc/>
	/// <remarks>升级：减费（1 → 0）。</remarks>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
