using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「拾星」：1 费技能（公共牌）。
///
/// <para>关键字<b>安息</b>：抽 <c>Cards</c> 张牌。</para>
///
/// <para><b>本轮的核心反哺卡</b>：被「连接」的连带丢弃丢掉时照常发动安息 → 再抽 3 张，
/// 于是「打出连接牌」从亏手牌变成赚手牌。</para>
///
/// <para>升级档「赋予连接」让 <b>本牌自身</b>永久带「连接」（见 <see cref="IsLinked"/>）——
/// 于是它也会被别的连接牌连带丢弃，再次触发安息。这与「北极圈」升级后给自己挂「安息」
/// 是同一套做法（覆盖虚属性，而不是在 <c>OnUpgrade</c> 里写运行期标记）。</para>
/// </summary>
public sealed class HeidemarieGatherStar : HeidemarieCard, IRestCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(3),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieGatherStar()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <summary>
	/// 升级「赋予<b>连接</b>」：本牌自身带连接。
	///
	/// <para>用<b>覆盖虚属性</b>而不是 <c>OnUpgrade</c> 里置运行期标记：
	/// 升级预览会克隆母本再 <c>UpgradeInternal()</c>，在 <c>OnUpgrade</c> 里改状态会污染母本；
	/// 覆盖属性则随 <see cref="CardModel.IsUpgraded"/> 每次现算，无状态可污染。</para>
	/// </summary>
	public override bool IsLinked => base.IsLinked || IsUpgraded;

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => DrawCards(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> DrawCards(choiceContext);

	/// <summary>打出与安息发动共用的效果：抽 <c>Cards</c> 张牌。</summary>
	private Task DrawCards(PlayerChoiceContext choiceContext)
		=> CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

	// 升级只改「带不带连接」（见 IsLinked），不改数值，因此不重写 OnUpgrade。
}
