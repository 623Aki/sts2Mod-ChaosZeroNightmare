using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「旧誓」：本角色回合开始时，墓地中每有 1 张「极光剑」，获得 2 点格挡。
///
/// <para>可叠加：实际格挡 = <b>剑数 × 2 × 层数</b>（升级后打出的「旧誓」施加 2 层，
/// 即每张剑 4 点）。</para>
///
/// <para>依赖「守墓」把极光剑永久留在墓地 —— 若没有守墓，剑会被洗牌带走，
/// 这条 build 就不成立（设计上有意如此）。</para>
///
/// <para>统计按<b>类型</b>判定而不是引用相等：战斗内卡牌实例可能被克隆。</para>
/// </summary>
public sealed class HeidemarieOldOathPower : PowerModel
{
	/// <summary>单层能力下，墓地中每 1 张极光剑获得的格挡。</summary>
	private const decimal BlockPerSwordPerStack = 2m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new BlockVar(BlockPerSwordPerStack, ValueProp.Unpowered),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <inheritdoc/>
	/// <remarks>
	/// 每回合遍历一次弃牌堆。量级 ≤ 数十张，可接受；
	/// 不要挪到 <c>ModifyBlockAdditive</c> 这类高频钩子里。
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner.Player)
		{
			return;
		}

		int swordCount = PileType.Discard.GetPile(player).Cards
			.Count(card => card is HeidemarieAuroraSword);

		if (swordCount <= 0)
		{
			return;
		}

		decimal amount = BlockPerSwordPerStack * swordCount * Amount;
		Flash();
		await CreatureCmd.GainBlock(Owner, amount, ValueProp.Unpowered, null);
	}
}
