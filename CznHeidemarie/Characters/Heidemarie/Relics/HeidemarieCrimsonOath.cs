using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Relics;

/// <summary>
/// 可获取遗物「赤光之誓」（Rare）：承载「赤光剑」（设计文档 §5.2）。
///
/// <list type="bullet">
///   <item><b>回合开始时</b>：获得 5 层「赤光剑」（层数上限 8）；</item>
///   <item>每当你<strong>直接使用</strong>或<strong>丢弃</strong> 1 张带「连接」的牌：
///   消耗 1 层，对<strong>当前生命值最高</strong>的敌人造成 3 点伤害；</item>
///   <item>层数为 0 时不触发。</item>
/// </list>
///
/// <para><b>为什么触发逻辑挂在能力侧</b>（§5.3）：层数本身就是「剩余次数」，
/// 让 <see cref="HeidemarieCrimsonSwordPower"/> 自己监听出牌 / 弃牌钩子，
/// 遗物只负责每回合补层 —— 补层的上限（8）与触发口径只有一处实现，
/// 不会出现「遗物判一次、能力再判一次」的分叉。</para>
///
/// <para><b>图标</b>：三张素材（256 大图 / 85 小图 / 85 描边）与引擎默认路径不同名，
/// 因此显式覆盖三条路径，做法同「市长铭牌 / 和平奖章」。</para>
/// </summary>
public sealed class HeidemarieCrimsonOath : RelicModel
{
	/// <summary>每回合开始补充的层数。</summary>
	private const int ChargesPerTurn = 5;

	/// <inheritdoc/>
	/// <remarks>可获取遗物：参与奖励 / 商店的 Rare 稀有度掷点。</remarks>
	public override RelicRarity Rarity => RelicRarity.Rare;

	/// <summary>
	/// 卡面里唯一的数字：每次触发的伤害（与能力常量同源，避免两处写死不一致）。
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DynamicVar("Amount", HeidemarieCrimsonSwordPower.DamagePerTrigger),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieCrimsonSwordPower>(),
	};

	/// <summary>小图标（85）：绕过 <c>relic_atlas</c>，直接指向素材 PNG。</summary>
	public override string PackedIconPath => ImageHelper.GetImagePath("relics/heidemarie_crimson_oath_small.png");

	/// <summary>描边图标（85 描边）。</summary>
	protected override string PackedIconOutlinePath => ImageHelper.GetImagePath("relics/heidemarie_crimson_oath_outline.png");

	/// <summary>大图标（256），用于遗物拾取 / 图鉴大图。</summary>
	protected override string BigIconPath => ImageHelper.GetImagePath("relics/heidemarie_crimson_oath.png");

	/// <inheritdoc/>
	/// <remarks>
	/// 我方回合开始：补 5 层「赤光剑」，按上限 8 截断（已满则不做任何事）。
	/// 与「市长铭牌 / 和平奖章」同一钩子、同一 <c>player == Owner</c> 口径。
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		int current = Owner.Creature.GetPowerAmount<HeidemarieCrimsonSwordPower>();
		int target = System.Math.Min(HeidemarieCrimsonSwordPower.MaxStacks, current + ChargesPerTurn);
		if (target <= current)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<HeidemarieCrimsonSwordPower>(
			choiceContext,
			Owner.Creature,
			target - current,
			Owner.Creature,
			null,
			silent: true);
	}
}
