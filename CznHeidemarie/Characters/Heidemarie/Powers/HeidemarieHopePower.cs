using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
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
///   <item>希望累计满 <see cref="ParadiseThreshold"/> 层时，在手牌生成 1 张「剑之乐园」——
///   <b>一场战斗只生成一次</b>。</item>
/// </list>
///
/// <para><b>阈值口径</b>：希望<b>不重置</b>，阈值固定为 100（不再递增），但只发一次牌 ——
/// 达到 100 层之后继续累积的层数仍然用于「市长铭牌」的档位结算
/// （每 10 层 1 点格挡 / ≥100 临时力量 / ≥200 回复与能量），只是不再产牌。</para>
///
/// <para><b>为什么不用 PowerCmd 把层数扣回去</b>：<see cref="PowerModel"/> 在层数降到 0 时
/// 会被引擎直接移除（<c>ShouldRemoveDueToAmount</c>），一旦归零这条引擎就断了 ——
/// 而且从 0 层重新爬回 100 层需要玩家再吃 20 次不被格挡的伤害。</para>
///
/// <para><b>计数口径</b>：只认 <see cref="ValuePropExtensions.IsPoweredAttack"/> 的伤害
/// （即攻击指令造成的伤害），中毒 / 掉血 / 反伤等非攻击伤害不计入 ——
/// 避免「点燃自己刷希望」这类非预期循环。宠物（Osty）打出的伤害算在主人头上。</para>
///
/// <para><b>素材</b>：能力图标按引擎约定放 <c>res://images/powers/heidemarie_hope_power.png</c>，
/// 无需改代码。</para>
/// </summary>
public sealed class HeidemarieHopePower : PowerModel
{
	/// <summary>每次触发（造成或受到 1 次不被格挡的伤害）增加的希望层数。</summary>
	private const int HopePerTrigger = 5;

	/// <summary>生成「剑之乐园」所需的希望层数（固定阈值，一场战斗只发一次）。</summary>
	private const int ParadiseThreshold = 100;

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

	/// <summary>内部数据：本场战斗是否已经发过「剑之乐园」。</summary>
	private sealed class Data
	{
		/// <summary>一场战斗只发一次，发过就置位。</summary>
		public bool ParadiseGranted;
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

	/// <summary>累加希望；累计满 <see cref="ParadiseThreshold"/> 层时发一次「剑之乐园」。</summary>
	private async Task GainHope(PlayerChoiceContext choiceContext)
	{
		// 层数直接叠加（不是「施加」新能力，不会触发 BeforePowerAmountChanged 之外的逻辑）
		await PowerCmd.ModifyAmount(choiceContext, this, HopePerTrigger, Owner, null);
		Flash();

		Data data = GetInternalData<Data>();
		if (data.ParadiseGranted || Amount < ParadiseThreshold)
		{
			return;
		}

		// 一场战斗只生成一次：先置位再发牌，避免发牌过程中的伤害结算重入多给一张。
		data.ParadiseGranted = true;
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
