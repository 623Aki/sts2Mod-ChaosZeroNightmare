using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Combat;
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
/// </summary>
public sealed class HeidemarieWallOfSwordsPower : PowerModel
{
	/// <summary>单层能力下，每弃 1 张普通牌获得的格挡。</summary>
	private const decimal BlockPerNormalCard = 2m;

	/// <summary>单层能力下，每弃 1 张极光剑获得的格挡。</summary>
	private const decimal BlockPerSword = 4m;

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
		// 只认自己玩家弃的牌（弃牌是全场事件，会在 Hook 里广播给所有战斗模型）。
		if (card.Owner != Owner.Player)
		{
			return;
		}

		// 再加一道「当前行动方」判定：敌方回合里因故弃牌不该给我方发格挡。
		ICombatState? combatState = Owner.CombatState;
		if (combatState == null || Owner.Side != combatState.CurrentSide)
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
