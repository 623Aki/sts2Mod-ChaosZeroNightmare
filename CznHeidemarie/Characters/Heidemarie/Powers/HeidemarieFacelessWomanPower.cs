using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「无脸女」：每当你<b>打出</b>一张带「连接」的牌时，抽 <see cref="PowerModel.Amount"/> 张牌。
///
/// <para><b>计数口径（设计已定 Q11 = B）</b>：只认<strong>玩家手动打出</strong>的连接牌。
/// 自动打出（「万剑齐鸣」连打、「剑冢」从墓地重放、其他效果 AutoPlay）<strong>不计数</strong> ——
/// 否则一次万剑齐鸣就能抽十几张，容易滚成无限循环；
/// 同时这也与「连接」关键字「只有<em>直接使用</em>才连带丢弃」的口径一致。</para>
///
/// <para>判定「带连接」用 <see cref="HeidemarieCard.IsLinked"/> 复合属性，
/// 因为它同时覆盖静态 <see cref="HeidemarieCard"/> 子类实现的
/// <c>ILinkCard</c> 与「缀星 / 微光 / 撷光」之类运行期临时赋予的连接。</para>
/// </summary>
public sealed class HeidemarieFacelessWomanPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>可叠加：层数就是每次触发的抽牌数。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>
	/// 出牌后结算。注意此处 <c>cardPlay.Card.Owner</c> 与 <c>cardPlay.Player</c> 可能不同
	/// （有换主效果的牌），本能力只认「自己玩家的连接牌」。
	/// </remarks>
	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Player != Owner.Player)
		{
			return;
		}

		// Q11 = B：自动打出不计。
		if (cardPlay.IsAutoPlay)
		{
			return;
		}

		if (cardPlay.Card is not HeidemarieCard holder || !holder.IsLinked)
		{
			return;
		}

		Flash();
		await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
	}
}
