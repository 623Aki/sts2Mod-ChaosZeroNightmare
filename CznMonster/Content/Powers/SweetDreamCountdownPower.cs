using System.Threading.Tasks;
using CznMonster.Content.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznMonster.Content.Powers;

/// <summary>
/// 美梦的「梦魇」计数（CZN 通用计时符号 cs02_0250，表外自定义显示 Power，同
/// <see cref="PolemicaBroodPower"/> 先例）：Boss 每受到 1 次伤害计数 -1，每回合抽牌后
/// 偷玩家 <see cref="SweetDreamStealRuntime.StealPerTurn"/> 张手牌（阶段 1 = 1 /
/// 阶段 2 = 2，v1.4 裁定），归零时归还全部并触发「苏醒」类重置行动（行动选择在
/// <see cref="SweetDream"/> 状态机分支里，这里只负责显示与两个运行时钩子）。
/// </summary>
/// <remarks>
/// 条目名裁定为「梦魇」（非「梦魇倒计时」）；计数语义按受击事件递减，
/// 非回合递减（设计文档 §1.1 / §8 风险 1 已实证）。
/// </remarks>
public sealed class SweetDreamCountdownPower : PowerModel
{
	/// <summary>阶段 1 的梦魇计数（CZN pt1_00 SET 7）。</summary>
	public const int Stage1Countdown = 7;

	/// <summary>阶段 2 的梦魇计数（CZN pt2_00 SET 10）。</summary>
	public const int Stage2Countdown = 10;

	/// <inheritdoc />
	/// <remarks>用 Buff：不会被 Artifact 拦截，且这条美梦自身的机制不是减益，
	/// 玩家驱散不应影响 CZN 语义。</remarks>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc />
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc />
	/// <remarks>纯显示计数，不播能力特效（自定义 Power 没有对应的 vfx 资源）。</remarks>
	public override bool ShouldPlayVfx => false;

	/// <inheritdoc />
	/// <remarks>
	/// 多人缩放（玩家裁定：不写自定义公式）：开启引擎默认口径
	/// 「基础值 × 玩家数 × 章节系数」（<see cref="PowerCmd.Apply"/> 内部按
	/// <see cref="PowerModel.GetScaledAmountForMultiplayer"/> 的默认实现缩放）；本类内
	/// 所有 SetAmount 类重置一律用 <see cref="SweetDream.Scaled"/> 复算同一公式对齐。
	/// </remarks>
	public override bool ShouldScaleInMultiplayer => true;

	/// <summary>
	/// 动态描述：标准模板 + 运行时变量 <c>{StolenCount}</c>（持有区当前牌数）与
	/// <c>{StealCount}</c>（每回合偷牌张数，阶段 1 = 1 / 阶段 2 = 2）。
	/// 悬停时 <c>GetDumbHoverTip</c> 会以最新值即时重建（对应留守待定项「动态描述刷新点」）。
	/// </summary>
	public override LocString Description
	{
		get
		{
			LocString description = new LocString("powers", base.Id.Entry + ".description");
			description.Add("StealCount", SweetDreamStealRuntime.StealPerTurn);
			description.Add("StolenCount", SweetDreamStealRuntime.Count);
			description.Add("StolenList", SweetDreamStealRuntime.BuildStolen());
			return description;
		}
	}

	/// <summary>
	/// CZN cu_takedmg_ge01：每次受到 ≥1 点伤害（未抵消）的事件，计数 -1。
	/// 多段攻击每段各算一次（设计文档 §8 风险 2：碎梦爪 3 段一次 -3）。
	/// </summary>
	public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
		DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != base.Owner || result.UnblockedDamage <= 0 || !base.Owner.IsAlive)
		{
			return;
		}
		if (base.Owner.Monster is not SweetDream dream)
		{
			return;
		}
		Flash();
		await dream.OnCountdownHit();
	}

	/// <summary>
	/// CZN cs02_0250_01（TIMING_DRAW_AFTER，LIST_PICK_RAN）：该玩家回合抽牌完成后，
	/// 从其手牌随机偷 <see cref="SweetDreamStealRuntime.StealPerTurn"/> 张（阶段 1 = 1 /
	/// 阶段 2 = 2，v1.4 裁定）。多人局语义：每名玩家在各自回合开始时各被偷一次
	/// （无手写同步，配合计数阈值 ×N 自平衡，设计文档 §10 映射口径）。
	/// </summary>
	public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		if (player.Creature.IsDead || !base.Owner.IsAlive || base.Owner.Monster is not SweetDream)
		{
			return;
		}
		for (int i = 0; i < SweetDreamStealRuntime.StealPerTurn; i++)
		{
			// 手牌中途见底时 StealOne 自行跳过，无需预判
			await SweetDreamStealRuntime.StealOne(player);
		}
	}

	/// <summary>
	/// 玩家裁定⑥（§8 风险 6）：战斗意外终结（击败/团灭/逃遁）时，被偷的战斗克隆体
	/// 随战斗态销毁、卡组本体从未离开牌堆——牌不会被吞。这里只需清空持有区缓存，
	/// 防止陈旧引用在下一场战斗把 {StolenCount} 虚报。
	/// </summary>
	public override Task AfterCombatEnd(CombatRoom room)
	{
		SweetDreamStealRuntime.Clear();
		return base.AfterCombatEnd(room);
	}
}
