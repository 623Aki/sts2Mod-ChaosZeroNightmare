using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 关键字「感应」：抽取该卡牌后，触发感应效果。
///
/// <para>由 <see cref="HeidemarieCard.AfterCardChangedPiles"/> 统一分派 ——
/// 本牌<strong>从抽牌堆进入手牌</strong>时调用 <see cref="OnSense"/>。</para>
///
/// <para>之所以不用 <c>AfterCardDrawn</c>：那个钩子只在
/// <c>CardPileCmd.Draw</c> 路径上派发，而「把指定牌从抽牌堆抓进手牌」的效果
/// （卡牌效果、遗物效果）走的是 <c>CardPileCmd.Add(card, PileType.Hand)</c>，
/// 不会有 <c>AfterCardDrawn</c>，会导致感应静默失效。</para>
/// </summary>
public interface ISenseCard
{
	/// <summary>本牌被抽到时触发。</summary>
	/// <param name="choiceContext">出牌/抽牌上下文。</param>
	Task OnSense(PlayerChoiceContext choiceContext);
}

/// <summary>
/// 关键字「连接」：<strong>直接使用</strong>此卡时，丢弃手牌中其他所有带「连接」的牌。
///
/// <para>纯标记接口 —— 只表示「本牌自带连接」。判定「谁带连接」用
/// <see cref="HeidemarieCard.IsLinked"/>（静态接口<strong>或</strong>运行期临时赋予），
/// 真正的丢弃动作统一由 <see cref="HeidemarieCard.BeforeCardPlayed"/> 调用
/// <see cref="HeidemarieCardActions.DiscardOtherLinked"/> 完成，
/// 因此运行期临时获得连接的普通牌也能正确触发。</para>
///
/// <para>「直接使用」是限定条件：从墓地重放（剑冢）、被别的效果连续自动打出
/// （万剑齐鸣）都不算直接使用，期间由
/// <see cref="HeidemarieCardActions.SuppressLinkDiscard"/> 抑制丢弃。</para>
/// </summary>
public interface ILinkCard
{
}

/// <summary>
/// 关键字「安息」：非回合结束丢弃此卡时，此卡效果发动；本回合内此卡不能被移出墓地。
///
/// <para>由 <see cref="HeidemarieCard.AfterCardDiscarded"/> 分派。
/// 回合末清手牌不触发该钩子，因此「非回合结束」判据天然成立。
/// 「本回合不能移出墓地」由 <see cref="HeidemarieCard.RestLockedThisTurn"/> 记录。</para>
/// </summary>
public interface IRestCard
{
	/// <summary>本牌在非回合结束时被丢弃，发动其效果。</summary>
	/// <param name="choiceContext">上下文。</param>
	Task OnRest(PlayerChoiceContext choiceContext);
}

/// <summary>
/// 关键字「守墓」：洗牌时，此卡不会移动至抽牌堆。
///
/// <para>纯标记接口 —— 由 <see cref="HeidemarieCard.ModifyShuffleOrder"/> 把它从洗牌列表剔除。</para>
/// </summary>
public interface IGraveKeepCard
{
}

/// <summary>
/// 关键字「终结」：使用此卡牌后立即结束本回合。
///
/// <para>纯标记接口（仅用于卡面词条显示）；实际行为是各卡在 <c>OnPlay</c> 末尾调用
/// <c>PlayerCmd.EndTurn(Owner, false)</c>。仅「打出」时生效。</para>
/// </summary>
public interface ITerminateCard
{
}

/// <summary>
/// 关键字「唯一」：整个卡组内仅可持有 1 张拥有「唯一」的相同卡牌，且无法复制。
///
/// <para>纯标记接口；去重逻辑由加牌入口统一处理（见实现清单 §2.7）。</para>
/// </summary>
public interface IUniqueCard
{
}

/// <summary>
/// 关键字「焚烧」：本牌被消耗时触发。
///
/// <para>由 <see cref="HeidemarieCard.AfterCardExhausted"/> 统一分派。</para>
/// </summary>
public interface IBurnCard
{
	/// <summary>本牌被消耗时触发。</summary>
	/// <param name="choiceContext">上下文。</param>
	/// <param name="causedByEthereal">是否由「虚无」在回合末引发的消耗。</param>
	Task OnBurn(PlayerChoiceContext choiceContext, bool causedByEthereal);
}
