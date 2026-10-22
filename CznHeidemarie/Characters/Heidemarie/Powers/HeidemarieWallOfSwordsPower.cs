using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「壁仞千里」：每当你丢弃 1 张牌时，获得 2 点格挡；
/// 若丢弃的是<b>极光剑</b>，改为 4 点。
///
/// <para>可叠加（<see cref="PowerStackType.Counter"/>）：实际格挡 = <b>单层值 × 层数</b>，
/// 因此打出 N 次「壁仞千里」后每弃 1 张牌就是 <c>2N</c>（弃剑 <c>4N</c>）点格挡。</para>
///
/// <para>照官方 <c>FeelNoPainPower</c> 的写法：格挡走
/// <c>CreatureCmd.GainBlock</c> + <see cref="ValueProp.Unpowered"/>（来自能力，不属于卡牌动作），
/// <c>cardPlay</c> 传 <c>null</c>。</para>
///
/// <para><b>回合末清手牌不触发本能力</b>：官方清手牌走 <c>CardPileCmd.Add</c>，
/// 不会派发 <see cref="AbstractModel.AfterCardDiscarded"/>，判据天然成立。</para>
///
/// <para><b>计数口径（2026-10-21 与「情绪爆发」统一）</b>：只判「自己玩家的牌进弃牌堆」，
/// <b>不判当前行动方</b> —— 手牌 / 抽牌堆被丢进弃牌堆都算，
/// 自己的卡、「连接」的连带丢弃、敌方效果逼你弃牌全部计入；回合末清手牌仍然不算。</para>
///
/// <para><b>为什么这里不用像「情绪爆发」那样记账</b>：格挡在敌方回合当场获得才有用
/// （正好挡本次敌方攻击），而能量会在玩家回合开始被 <c>ResetEnergy</c> 冲掉，
/// 两者时机诉求相反。</para>
///
/// <para><b>本能力不是计数型</b>：没有阈值、每弃 1 张立即结算，
/// 因此图标数字继续显示层数（<see cref="PowerModel.Amount"/>），不做弃牌计数显示。</para>
/// </summary>
public sealed class HeidemarieWallOfSwordsPower : PowerModel
{
	/// <summary>单层能力下，每弃 1 张普通牌获得的格挡。</summary>
	private const decimal BlockPerNormalCard = 2m;

	/// <summary>单层能力下，每弃 1 张极光剑获得的格挡（数值回标 4 → 3，《新增设计》§3.2 #2）。</summary>
	private const decimal BlockPerSword = 3m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <summary>
	/// 单层数值。卡面 / 悬停提示里的文本按 1 层写死（实际值由 <see cref="PowerModel.Amount"/> 放大），
	/// 与本工程既有能力的约定一致。
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new BlockVar(BlockPerNormalCard, ValueProp.Unpowered),
		new DynamicVar("SwordBlock", BlockPerSword),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <inheritdoc/>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		// 只认「自己玩家的牌进弃牌堆」（弃牌是全场事件，会在 Hook 里广播给所有战斗模型）。
		// 不判当前行动方：敌方效果逼你弃牌同样给格挡（与「情绪爆发」统一口径）。
		if (card.Owner != Owner.Player)
		{
			return;
		}

		// 极光剑按类型判定，不按引用相等 —— 战斗内实例可能被克隆。
		decimal perStack = card is HeidemarieAuroraSword
			? DynamicVars["SwordBlock"].BaseValue
			: DynamicVars.Block.BaseValue;

		decimal amount = perStack * Amount;
		if (amount <= 0m)
		{
			return;
		}

		Flash();
		await CreatureCmd.GainBlock(Owner, amount, ValueProp.Unpowered, null);
	}
}
