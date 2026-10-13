using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 关键字「感应」：抽取该卡牌后，触发感应效果。
///
/// <para>由 <see cref="HeidemarieCard.AfterCardDrawn"/> 统一分派 ——
/// 抽到本牌时调用 <see cref="OnSense"/>。</para>
/// </summary>
public interface ISenseCard
{
	/// <summary>本牌被抽到时触发。</summary>
	/// <param name="choiceContext">出牌/抽牌上下文。</param>
	Task OnSense(PlayerChoiceContext choiceContext);
}

/// <summary>
/// 关键字「链接」：直接使用此卡时，丢弃手牌中其他所有带「链接」的牌。
///
/// <para>纯标记接口 —— 判定「谁会被连带丢弃」；真正的丢弃动作由出牌的那张链接牌调用
/// <see cref="HeidemarieCardActions.DiscardOtherLinked"/> 完成。</para>
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
