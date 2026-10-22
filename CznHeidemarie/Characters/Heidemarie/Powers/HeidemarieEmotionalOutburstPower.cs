using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「情绪爆发」：每当你丢弃 <see cref="CardsPerEnergy"/> 张牌，获得 1 点能量。
///
/// <para><b>计数口径（2026-10-21 修订）</b>：只要<b>自己玩家的牌</b>进弃牌堆就算 ——
/// 手牌或抽牌堆被丢进弃牌堆都算，<b>不管是谁 / 什么效果丢的</b>
/// （自己的卡、「连接」的连带丢弃、敌方效果逼你弃牌……全部计入）。
/// 唯一例外是<b>回合结束的清手牌</b>。</para>
///
/// <para><b>为什么「回合结束」天然不算</b>：官方回合末清手牌（<c>CombatManager.FlushPlayerHand</c>）
/// 走 <c>CardPileCmd.Add</c>，不会派发 <see cref="AbstractModel.AfterCardDiscarded"/>，
/// 因此这条判据天然成立（与「壁仞千里」同一依据）。</para>
///
/// <para><b>为什么不再判「当前行动方」</b>：官方遗物（Tingsha / Tough Bandages）都要求
/// <c>Side == CurrentSide</c>，但那会把「敌方回合里被迫弃牌」整段排除。本卡的设计口径是
/// 「算全部弃牌」，所以去掉这道判断；代价是敌方回合里凑满 4 张时，
/// <b>当场发的能量会在玩家回合开始被 <c>ResetEnergy</c> 冲掉</b> ——
/// 因此这类能量先记在 <c>_bankedEnergy</c> 里，等玩家回合开始再真正发放。</para>
///
/// <para><b>为什么用私有计数器而不是 <see cref="PowerModel.Amount"/></b>（设计已定 Q12 = A）：
/// 层数对玩家是可见的，若把「已丢 3 张」写进层数，卡面会显示「情绪爆发 3」，
/// 玩家会误以为叠了 3 层效果。层数在这里只表示「每 N 张」的档位（当前恒为 1 层）。</para>
///
/// <para><b>图标上的数字 = 已丢弃张数</b>（2026-10-21 新增）：走
/// <see cref="PowerModel.DisplayAmount"/>，只有显示层用私有计数器，<see cref="PowerModel.Amount"/>
/// 仍是档位；每次计数变化调 <see cref="PowerModel.InvokeDisplayAmountChanged"/> 让图标实时刷新。</para>
///
/// <para><b>打多张 = 多个图标</b>：<see cref="PowerInstanceType.Instanced"/>，与官方
/// <c>TheBombPower</c> 同一写法 —— 每次打出新建一个独立实例，各自记各自的进度。</para>
/// </summary>
public sealed class HeidemarieEmotionalOutburstPower : PowerModel
{
	/// <summary>累计丢弃多少张牌发 1 点能量。</summary>
	private const int CardsPerEnergy = 4;

	/// <summary>本回合已丢弃的张数（不对外显示）。</summary>
	private int _discardedSinceLastEnergy;

	/// <summary>
	/// 非玩家回合凑满阈值、暂时没法发的能量（点）。
	///
	/// <para>玩家回合开始时会 <c>ResetEnergy</c>（把能量直接设成上限），
	/// 此刻发的能量等于白给，所以先记账、到玩家回合开始再补发。</para>
	/// </summary>
	private int _bankedEnergy;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>
	/// 每次打出都新建独立实例（打 2 张就是 2 个图标，各自记进度），不叠层。
	/// </remarks>
	public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

	/// <inheritdoc/>
	/// <remarks>
	/// 图标数字显示<b>当前已丢弃的张数</b>（0 ~ 3）；满 4 发能量后归零。
	/// 只影响显示，不影响 <see cref="PowerModel.Amount"/>（档位，恒为 1）。
	/// </remarks>
	public override int DisplayAmount => _discardedSinceLastEnergy;

	/// <inheritdoc/>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		// 弃牌是全场事件，会在 Hook 里广播给所有战斗模型 —— 先判归属：
		// 只算「自己玩家的牌」，别人的牌被丢不归本能力管。
		// 注意：这里**不判**「当前行动方」—— 敌方效果丢弃我方手牌 / 抽牌堆同样计入。
		if (card.Owner != Owner.Player)
		{
			return;
		}

		_discardedSinceLastEnergy++;
		bool reachedThreshold = _discardedSinceLastEnergy >= CardsPerEnergy;
		if (reachedThreshold)
		{
			// 用减法而不是清零：一弃 8 张时应当发 2 点能量。
			_discardedSinceLastEnergy -= CardsPerEnergy;
		}

		// 图标数字实时刷新（满 4 归零也在这里同步）。
		InvokeDisplayAmountChanged();

		if (!reachedThreshold)
		{
			return;
		}

		// 敌方回合里此刻发的能量会被玩家回合开始的 ResetEnergy 冲掉，
		// 先记账，等 AfterPlayerTurnStart 再真正发放。
		ICombatState? combatState = Owner.CombatState;
		if (combatState == null || Owner.Side != combatState.CurrentSide)
		{
			_bankedEnergy++;
			return;
		}

		await GrantEnergy(1m);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 补发敌方回合期间记账的能量。放在 <c>AfterPlayerTurnStart</c>：
	/// 此时 <c>SetupPlayerTurn</c> 已完成 <c>ResetEnergy</c> 与摸牌，发的能量不会被冲掉。
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner.Player || _bankedEnergy <= 0)
		{
			return;
		}

		decimal amount = _bankedEnergy;
		_bankedEnergy = 0;
		await GrantEnergy(amount);
	}

	/// <summary>闪光 + 发能量（立即发放与补发共用）。</summary>
	private async Task GrantEnergy(decimal amount)
	{
		Player? player = Owner.Player;
		if (player == null)
		{
			return;
		}

		Flash();
		await PlayerCmd.GainEnergy(amount, player);
	}
}
