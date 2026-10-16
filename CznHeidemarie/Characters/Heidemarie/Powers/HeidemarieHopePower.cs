using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Powers;

/// <summary>
/// 能力「希望」：希望是一把量尺 —— 你越用力挥剑、越不肯倒下，它涨得越快。
///
/// <list type="bullet">
///   <item>每<b>造成</b>一次不被格挡的伤害（每次命中各算一次），希望 +5；</item>
///   <item>每<b>受到</b>一次不被格挡的伤害（每次命中各算一次），希望 +5；</item>
///   <item>希望每累计满 100 层，在手牌生成 1 张「剑之乐园」，并提高下一次的阈值。</item>
/// </list>
///
/// <para><b>阈值口径（设计约定）</b>：希望<b>不重置</b>。
/// 内部数据记录「下一个阈值」，每跨过一次就 +100 ——
/// 因此第 1 张「剑之乐园」在 100 层时到手，第 2 张在 200 层、第 3 张在 300 层……
/// 这与「回合开始时每 10 层希望获得 1 点格挡」（见 <c>HeidemarieMayorBadge</c>）共用同一份层数，
/// 是一条越打越强的长线引擎。</para>
///
/// <para><b>为什么不用 PowerCmd 把层数扣回去</b>：<see cref="PowerModel"/> 在层数降到 0 时
/// 会被引擎直接移除（<c>ShouldRemoveDueToAmount</c>），一旦归零这条引擎就断了 ——
/// 而且从 0 层重新爬回 100 层需要玩家再吃 20 次不被格挡的伤害。
/// 「阈值递增 + 层数不清零」既保留了原文「超过 100 后生成」的语义，也不会自断引擎。</para>
///
/// <para><b>计数口径</b>：只认 <see cref="ValuePropExtensions.IsPoweredAttack"/> 的伤害
/// （即攻击指令造成的伤害），中毒 / 掉血 / 反伤等非攻击伤害不计入 ——
/// 避免「点燃自己刷希望」这类非预期循环。宠物（Osty）打出的伤害算在主人头上。</para>
///
/// <para><b>素材</b>：能力图标暂缺，会回落到 <c>images/powers/missing_power.png</c>。
/// 补图时按引擎约定放 <c>res://images/powers/heidemarie_hope_power.png</c> 即可，无需改代码。</para>
/// </summary>
public sealed class HeidemarieHopePower : PowerModel
{
	/// <summary>每次触发（造成或受到 1 次不被格挡的伤害）增加的希望层数。</summary>
	private const int HopePerTrigger = 50;

	/// <summary>生成 1 张「剑之乐园」所需的希望层数（阈值步长）。</summary>
	private const int HopeStep = 100;

	/// <inheritdoc/>
	public override PowerType Type => PowerType.Buff;

	/// <inheritdoc/>
	public override PowerStackType StackType => PowerStackType.Counter;

	/// <summary>卡面 / 悬停提示里的数值（按单次触发写死，实际值由代码常量给出）。</summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(1),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
	{
		HoverTipFactory.FromCard<HeidemarieSwordParadise>(),
	};

	/// <summary>内部数据：下一个生成「剑之乐园」的阈值。</summary>
	private sealed class Data
	{
		/// <summary>已发放过的阈值累计量（100 / 200 / 300 …）。</summary>
		public int NextThreshold = HopeStep;
	}

	/// <inheritdoc/>
	protected override object InitInternalData() => new Data();

	/// <inheritdoc/>
	/// <remarks>我方（含宠物）造成不被格挡的攻击伤害 → 希望 +5。</remarks>
	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (dealer != Owner && dealer?.PetOwner?.Creature != Owner)
		{
			return;
		}

		if (!props.IsPoweredAttack() || result.UnblockedDamage <= 0)
		{
			return;
		}

		await GainHope(choiceContext);
	}

	/// <inheritdoc/>
	/// <remarks>我方受到不被格挡的攻击伤害 → 希望 +5。</remarks>
	public override async Task AfterDamageReceived(
		PlayerChoiceContext choiceContext,
		Creature target,
		DamageResult result,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (target != Owner)
		{
			return;
		}

		if (!props.IsPoweredAttack() || result.UnblockedDamage <= 0)
		{
			return;
		}

		await GainHope(choiceContext);
	}

	/// <summary>累加希望；跨过阈值时发牌。</summary>
	private async Task GainHope(PlayerChoiceContext choiceContext)
	{
		// 层数直接叠加（不是「施加」新能力，不会触发 BeforePowerAmountChanged 之外的逻辑）
		await PowerCmd.ModifyAmount(choiceContext, this, HopePerTrigger, Owner, null);
		Flash();

		Data data = GetInternalData<Data>();
		if (Amount < data.NextThreshold)
		{
			return;
		}

		// 一次伤害最多跨过一层阈值（+5 的步长远小于 100，无需 while 循环）
		data.NextThreshold += HopeStep;
		await GrantSwordParadise(choiceContext);
	}

	/// <summary>在手牌生成 1 张「剑之乐园」。</summary>
	private async Task GrantSwordParadise(PlayerChoiceContext choiceContext)
	{
		if (Owner.CombatState is not { } combatState || Owner.Player is not { } player)
		{
			return;
		}

		HeidemarieSwordParadise card = combatState.CreateCard<HeidemarieSwordParadise>(player);
		await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
	}
}
