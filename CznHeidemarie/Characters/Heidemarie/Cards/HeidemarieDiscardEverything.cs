using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「这些都不要了」：0 费技能（稀有牌），<b>消耗</b>；升级后移除消耗。
///
/// <para>丢弃抽牌堆里的<b>全部</b>卡牌，每丢弃 1 张获得 <c>Block</c> 点格挡（**每张 2 点**）。</para>
///
/// <para>⚠️ 两个容易踩的坑：
/// <list type="number">
///   <item>先 <c>ToList()</c> 快照再弃 —— 边遍历活集合边改堆会抛异常；</item>
///   <item>必须用批量重载 <c>CardCmd.Discard(ctx, IEnumerable)</c>，循环单张会错乱 <c>Sly</c> 时机。</item>
/// </list></para>
///
/// <para>「守墓」的极光剑本来就留在墓地里、不会进抽牌堆，因此不受本牌影响。</para>
/// </summary>
public sealed class HeidemarieDiscardEverything : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 每丢弃 1 张抽牌堆的牌获得的格挡
		new BlockVar(2m, ValueProp.Move),
	};

	/// <summary>0 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieDiscardEverything()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>升级档「移除消耗」：用<b>覆盖属性</b>随 <c>IsUpgraded</c> 现算，
	/// 不在 <c>OnUpgrade</c> 里改状态 —— 升级预览会克隆母本，改状态会污染母本。</remarks>
	public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
		? Array.Empty<CardKeyword>()
		: new[] { CardKeyword.Exhaust };

	/// <inheritdoc/>
	public override bool GainsBlock => true;

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> drawPile = PileType.Draw.GetPile(Owner).Cards.ToList();
		if (drawPile.Count == 0)
		{
			return;
		}

		// 先弃光，再按张数结算格挡 —— 顺序反过来会让「被丢弃时」的效果吃到错误的场况
		await CardCmd.Discard(choiceContext, drawPile);

		decimal block = DynamicVars.Block.BaseValue * drawPile.Count;
		await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
	}
}
