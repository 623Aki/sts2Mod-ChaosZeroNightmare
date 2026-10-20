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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Relics;

/// <summary>
/// 初始遗物「市长铭牌」：受群众爱戴。
///
/// <list type="bullet">
///   <item><b>战斗开始时</b>：获得能力「希望」20 层；</item>
///   <item><b>每回合开始时</b>：按「希望」层数<b>分档递增</b>地结算收益
///   （见 <see cref="AfterPlayerTurnStart"/>）：
///   <list type="bullet">
///     <item>希望 &lt; 100：获得 <c>层数 / 10</c> 点格挡；</item>
///     <item>希望 ≥ 100：<b>额外</b>获得 5 点[临时力量]（本回合结束收回）；</item>
///     <item>希望 ≥ 200：<b>额外</b>回复 10 点生命并获得 2 点能量。</item>
///   </list></item>
/// </list>
///
/// <para><b>档位是「累加」而不是「互斥」</b>：卡面写的是「额外获得」。
/// 若做成互斥，玩家爬到 200 层反而会<b>丢掉</b> 20 点格挡与 5 点力量 ——
/// 那是一段纯粹的降级，不可能是有意设计。因此高档次都建立在低档次之上。</para>
///
/// <para><b>为什么战斗开始用 <see cref="AfterRoomEntered"/> 而不是 <c>BeforeCombatStart</c></b>：
/// 引擎文档明确要求「应在第一回合开始<b>之前</b>生效的战斗开始效果（例如施加能力）」
/// 走这个钩子并做 <c>room is CombatRoom</c> 判定 —— 官方 <c>BronzeScales</c> / <c>DataDisk</c> 同款写法。
/// 用 <c>BeforeCombatStart</c> 会在部分时序下达不到「第一回合开始时就有 5 层希望」的效果。</para>
///
/// <para><b>格挡走 <see cref="AfterPlayerTurnStart"/></b>：这是一个「需要玩家选择上下文」的钩子，
/// 本遗物不做任何选择，因此直接用 <c>player == Owner</c> 判定即可。
/// STS2 第 1 回合结束不清空格挡，回合开始给格挡不会被打脸。</para>
///
/// <para><b>「临时力量」为什么要单独一个能力类</b>：见
/// <see cref="HeidemarieTemporaryStrengthPowerBase"/> —— 引擎的
/// <c>TemporaryStrengthPower</c> 是抽象基类，且 <c>OriginModel</c> 要求指向发放者，
/// 因此本处与「严阵以待」各开一个子类（<see cref="HeidemarieHopeStrengthPower"/> /
/// <c>HeidemarieTemporaryStrengthPower</c>）共用同一套实现与同一张图标。</para>
///
/// <para><b>图标</b>：三张素材分别是 256 大图 / 85 小图 / 85 描边，
/// 与引擎默认的三条路径约定（<c>relics/&lt;id&gt;.png</c>、
/// <c>relic_atlas.sprites/&lt;id&gt;.tres</c>、<c>relic_outline_atlas.sprites/&lt;id&gt;.tres</c>）不同名，
/// 因此在这里显式覆盖三条路径。三个文件都在 <c>res://images/relics/</c> 下。</para>
/// </summary>
public sealed class HeidemarieMayorBadge : RelicModel
{
	/// <summary>单场战斗开始时施加的「希望」层数。</summary>
	private const decimal HopeAtCombatStart = 20m;

	/// <summary>档位一：每多少层「希望」折算 1 点格挡。</summary>
	private const int HopePerBlock = 10;

	/// <summary>档位二阈值：希望达到该层数后开始给临时力量（<b>不含</b>格挡变化）。</summary>
	private const int StrengthTierHope = 100;

	/// <summary>档位二每回合给予的临时力量点数。</summary>
	private const decimal StrengthPerTurn = 5m;

	/// <summary>档位三阈值：希望达到该层数后开始给回复与能量。</summary>
	private const int BlessingTierHope = 200;

	/// <summary>档位三每回合回复的生命。</summary>
	private const decimal BlessingHeal = 10m;

	/// <summary>档位三每回合获得的能量。</summary>
	private const int BlessingEnergy = 2;

	/// <inheritdoc/>
	/// <remarks>初始遗物：不出现在任何奖励 / 商店池的稀有度掷点里（掷点只会产出 Common/Uncommon/Rare）。</remarks>
	public override RelicRarity Rarity => RelicRarity.Starter;

	/// <summary>卡面 / 悬停提示里的数值：全部按「单档、单回合」写，实际值由这些常量放大。</summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(4)
	{
		new BlockVar(1m, ValueProp.Unpowered),
		// 与原生「预备打击」一致：卡面/遗物文本用原生 StrengthPower 的图标与数值，
		// 实际施加的 HeidemarieHopeStrengthPower 只负责回合末回收。
		new PowerVar<StrengthPower>("Strength", StrengthPerTurn),
		new HealVar(BlessingHeal),
		new EnergyVar(BlessingEnergy),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieHopePower>(),
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <summary>小图标（85）：绕过 <c>relic_atlas</c>，直接指向素材 PNG。</summary>
	public override string PackedIconPath => ImageHelper.GetImagePath("relics/heidemarie_mayor_badge_small.png");

	/// <summary>描边图标（85 描边）。</summary>
	protected override string PackedIconOutlinePath => ImageHelper.GetImagePath("relics/heidemarie_mayor_badge_outline.png");

	/// <summary>大图标（256），用于遗物拾取 / 图鉴大图。</summary>
	protected override string BigIconPath => ImageHelper.GetImagePath("relics/heidemarie_mayor_badge.png");

	/// <inheritdoc/>
	/// <remarks>战斗开始时（战斗房间进入时）施加 5 层「希望」。</remarks>
	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is not CombatRoom)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<HeidemarieHopePower>(
			new ThrowingPlayerChoiceContext(),
			Owner.Creature,
			HopeAtCombatStart,
			Owner.Creature,
			null);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 我方回合开始时按「希望」层数分档结算，<b>三档累加</b>：
	/// 格挡（始终）→ 临时力量（≥100）→ 回复 + 能量（≥200）。
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		int hope = Owner.Creature.GetPower<HeidemarieHopePower>()?.Amount ?? 0;
		bool granted = false;

		// ── 档位一：希望每满 10 层 → 1 点格挡（任何层数都生效，向下取整）──────────
		int block = hope / HopePerBlock;
		if (block > 0)
		{
			await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, null);
			granted = true;
		}

		// ── 档位二：希望 ≥ 100 → 额外 5 点临时力量（本方回合结束时由该能力自行收回）──
		if (hope >= StrengthTierHope)
		{
			await PowerCmd.Apply<HeidemarieHopeStrengthPower>(
				choiceContext,
				Owner.Creature,
				StrengthPerTurn,
				Owner.Creature,
				null,
				silent: true);
			granted = true;
		}

		// ── 档位三：希望 ≥ 200 → 额外回复 10 点生命 + 2 点能量 ───────────────────
		if (hope >= BlessingTierHope)
		{
			await CreatureCmd.Heal(Owner.Creature, BlessingHeal);
			await PlayerCmd.GainEnergy(BlessingEnergy, Owner);
			granted = true;
		}

		if (granted)
		{
			Flash();
		}
	}
}
