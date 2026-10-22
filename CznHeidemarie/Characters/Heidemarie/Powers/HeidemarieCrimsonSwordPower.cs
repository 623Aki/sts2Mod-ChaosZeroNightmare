using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「赤光剑」：遗物「赤光之誓」的计数载体（设计文档 §5.2 / §5.3）。
///
/// <para><see cref="PowerModel.Amount"/> = <b>剩余触发次数</b>（遗物每回合补 5 层，上限 8）。
/// 每当你<strong>直接使用</strong>或<strong>丢弃</strong> 1 张带「连接」的牌：
/// 消耗 1 层，对<strong>当前生命值最高</strong>的敌人造成 <see cref="DamagePerTrigger"/> 点伤害；
/// 层数为 0 时不再触发。</para>
///
/// <para><b>触发判定与「极光连锁」共用</b>同一对静态方法
/// （<see cref="HeidemarieCardActions.IsLinkTrigger"/> / <see cref="HeidemarieCardActions.IsLinkDiscard"/>）——
/// 同一张牌的一次事件只触发一次；被「连接」连带丢弃的其他牌各自触发
/// （它们确实各被丢弃了一次）。</para>
///
/// <para><b>伤害口径</b>：<see cref="ValueProp.Unpowered"/> —— 遗物能力造成的固定伤害，
/// 不吃力量 / 虚弱等增伤区（与「壁仞千里」给的格挡同一思路）。</para>
/// </summary>
public sealed class HeidemarieCrimsonSwordPower : PowerModel
{
	/// <summary>层数上限（遗物补充时按此截断）。</summary>
	public const int MaxStacks = 8;

	/// <summary>每次触发造成的伤害。</summary>
	public const decimal DamagePerTrigger = 3m;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	/// <remarks>层数 = 剩余触发次数。</remarks>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <summary>卡面 / 悬停提示的固定伤害值。</summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(DamagePerTrigger, ValueProp.Unpowered),
	};

	/// <inheritdoc/>
	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner.Player is not { } player || !HeidemarieCardActions.IsLinkTrigger(cardPlay, player))
		{
			return;
		}

		await ConsumeAndStrikeAsync(choiceContext);
	}

	/// <inheritdoc/>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (Owner.Player is not { } player || !HeidemarieCardActions.IsLinkDiscard(card, player))
		{
			return;
		}

		await ConsumeAndStrikeAsync(choiceContext);
	}

	/// <summary>消耗 1 层，并对当前生命值最高的敌人造成一次伤害。</summary>
	private async Task ConsumeAndStrikeAsync(PlayerChoiceContext choiceContext)
	{
		if (Amount <= 0)
		{
			// 层数为 0 时不触发（卡面明示）
			return;
		}

		if (Owner.CombatState is not { } combatState)
		{
			return;
		}

		// 「当前生命值最高」：只算活着的可命中敌人；同血量取列表顺序靠前者。
		Creature? target = combatState.HittableEnemies
			.Where(enemy => enemy.IsAlive)
			.OrderByDescending(enemy => enemy.CurrentHp)
			.FirstOrDefault();

		if (target == null)
		{
			return;
		}

		Flash();

		// 先扣层：扣到 0 时直接移除能力，避免图标停在「0」。
		if (Amount <= 1)
		{
			await PowerCmd.Remove(this);
		}
		else
		{
			SetAmount(Amount - 1, silent: true);
		}

		await CreatureCmd.Damage(choiceContext, target, DynamicVars.Damage, Owner);
	}
}
