using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Relics;

/// <summary>
/// 遗物「和平奖章」：以静制动。
///
/// <para><b>回合开始时</b>，若<strong>所有存活敌人的意图都不是攻击</strong>：</para>
/// <list type="bullet">
///   <item>友方所有人（联机时为全部玩家，含自己）各额外抽 <c>Cards</c> 张牌；</item>
///   <item>遗物持有者获得 <c>Energy</c> 点能量；</item>
///   <item>所有敌人获得 <c>Block</c> 点格挡 —— 这是这份「和平」的<strong>代价</strong>：
///   没人挨打，就意味着没人该掉血。</item>
/// </list>
///
/// <para><b>「意图不是攻击」用引擎官方的 <see cref="MonsterModel.IntendsToAttack"/>
/// 判定</b>（覆盖 Attack 与 DeathBlow 两种攻击意图），不自己解析 MoveState ——
/// 与敌人头顶意图图标同一判据，不会出现「图标画的是防御、奖章却按攻击处理」的错位。</para>
///
/// <para><b>多敌人只要有一个想动手就不触发</b>（设计文档明示）：用 <c>All</c> 而非
/// <c>Any</c>；已阵亡的敌人不计入 —— 死者没有意图，不应一票否决活人的和平。</para>
///
/// <para><b>为什么能量只给持有者</b>：卡面三句各有主语，「友方所有人」只修饰抽牌一句；
/// 能量句无主语，按本 mod「市长铭牌」的同一口径（<c>PlayerCmd.GainEnergy</c> 只给
/// <c>Owner</c>）处理。联机下若给全员 +2，每个和平回合都是全场免费电池，强度失控。</para>
/// </summary>
public sealed class HeidemariePeaceMedal : RelicModel
{
	/// <summary>条件满足时每名友方抽取的牌数。</summary>
	private const int CardsToDraw = 2;

	/// <summary>条件满足时持有者获得的能量。</summary>
	private const int EnergyToGain = 2;

	/// <summary>条件满足时每名敌人获得的格挡（代价）。</summary>
	private const decimal EnemyBlock = 5m;

	/// <inheritdoc/>
	/// <remarks>
	/// 效果量级大（免费过牌 + 免费能量），但触发条件苛刻（所有敌人都不攻击），
	/// 且带「敌人 +5 格挡」的实质代价，定 <b>Rare</b>。
	/// 设计文档此栏原本留空，稀有度为落地时补定 —— 如需调整只改这一处。
	/// </remarks>
	public override RelicRarity Rarity => RelicRarity.Rare;

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(3)
	{
		new CardsVar(CardsToDraw),
		new EnergyVar(EnergyToGain),
		new BlockVar(EnemyBlock, ValueProp.Unpowered),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
		HoverTipFactory.Static(StaticHoverTip.Energy),
	};

	/// <summary>小图标（85）：绕过 <c>relic_atlas</c>，直接指向素材 PNG（与「市长铭牌」同款做法）。</summary>
	public override string PackedIconPath => ImageHelper.GetImagePath("relics/heidemarie_peace_medal_small.png");

	/// <summary>描边图标（85 描边）。</summary>
	protected override string PackedIconOutlinePath => ImageHelper.GetImagePath("relics/heidemarie_peace_medal_outline.png");

	/// <summary>大图标（256），用于遗物拾取 / 图鉴大图。</summary>
	protected override string BigIconPath => ImageHelper.GetImagePath("relics/heidemarie_peace_medal.png");

	/// <inheritdoc/>
	/// <remarks>
	/// 我方回合开始结算。与「市长铭牌」同一钩子、同一 <c>player == Owner</c> 口径：
	/// 这是一个「需要玩家选择上下文」的钩子，本遗物不做任何选择。
	/// </remarks>
	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner)
		{
			return;
		}

		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		// 和平判定：所有存活敌人都不打算攻击（有一个想动手就不算和平）
		if (!IsPeacefulTurn(combatState))
		{
			return;
		}

		Flash();

		// ① 友方所有人（全部玩家，含自己）各抽 2 张 —— 单机时就是自己
		foreach (Player ally in combatState.Players)
		{
			if (ally.Creature is { IsDead: false })
			{
				await CardPileCmd.Draw(choiceContext, CardsToDraw, ally);
			}
		}

		// ② 能量 +2：只给持有者（口径见类注释）
		await PlayerCmd.GainEnergy(EnergyToGain, Owner);

		// ③ 敌人 +5 格挡：这份「和平」的代价
		foreach (Creature enemy in combatState.Enemies)
		{
			if (enemy.IsAlive)
			{
				await CreatureCmd.GainBlock(enemy, EnemyBlock, ValueProp.Unpowered, null);
			}
		}
	}

	/// <summary>
	/// 本回合是否「无人意图攻击」：所有存活敌人的
	/// <see cref="MonsterModel.IntendsToAttack"/> 均为 <c>false</c>。
	/// 无存活敌人时 vacuously 成立（战斗实则已结束，无所谓）。
	/// </summary>
	private static bool IsPeacefulTurn(ICombatState combatState)
	{
		return combatState.Enemies
			.Where(enemy => enemy.IsAlive)
			.All(enemy => enemy.Monster is not { IntendsToAttack: true });
	}
}
