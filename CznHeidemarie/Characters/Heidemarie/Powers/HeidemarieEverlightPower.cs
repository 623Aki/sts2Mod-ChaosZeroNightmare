using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「长明」：我方回合开始时，获得 <see cref="PowerModel.Amount"/> 层<b>极夜</b>。
///
/// <para><b>层数 = 每回合发放的极夜层数</b>（Counter，可叠加）：
/// 打出 1 张「长明」给 2 层，打出 2 张合计 4 层 —— 于是每回合开始发的极夜也随之翻倍。
/// 升级档由卡牌侧把施加层数改为 4（见 <c>HeidemarieEverlight</c>），能力本体不判升级状态。</para>
///
/// <para><b>与「北极圈」的分工</b>：「北极圈」直接产剑（数量），「长明」只产极夜（质量）。
/// 两者都在 <c>AfterPlayerTurnStart</c> 结算，但因为发的是不同能力 / 不同牌，互不干扰。</para>
/// </summary>
public sealed class HeidemarieEverlightPower : PowerModel
{
	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>层数 = 每回合开始的极夜层数，可叠加、不设上限。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	/// <remarks>
	/// 回合开始发放极夜。
	///
	/// <para>只判「是不是自己」，不判当前行动方以外的条件 —— 该钩子本身只在玩家回合开始时派发。
	/// 发放走 <c>silent: true</c>：这是能力自身的周期效果，不需要每次闪一下施加动画
	/// （与「北极圈」生成剑、极光剑的被动结算同一口径）。</para>
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner.Player)
		{
			return;
		}

		// Amount 至少按 1 层算：能力存在就应当有产出，避免异常存档里出现「0 层却挂在身上」
		int layers = Math.Max(1, (int)Amount);
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner,
			layers,
			Owner,
			null,
			silent: true);
	}
}
