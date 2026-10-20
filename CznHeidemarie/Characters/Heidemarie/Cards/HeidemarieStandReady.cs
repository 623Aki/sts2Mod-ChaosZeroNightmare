using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「严阵以待」：X 费技能（公共牌），关键字<b>保留</b>。
///
/// <para>获得 <b>3X</b> 点格挡；下一回合获得 <b>X</b> 点临时力量。</para>
///
/// <para><b>X 费的源码口径</b>：<c>HasEnergyCostX => true</c> + 构造里基准费写 0，
/// 引擎会在出牌时把当前能量写进 <c>CapturedXValue</c>，用 <c>ResolveEnergyXValue()</c> 读
/// （官方 <c>Whirlwind</c> / <c>Skewer</c> 同款写法）。</para>
///
/// <para>「下一回合的临时力量」分两步：本牌先挂 <see cref="HeidemarieStandReadyPower"/>
/// （只记数值），到下一回合开始时它再转成 <see cref="HeidemarieTemporaryStrengthPower"/> ——
/// 因为临时力量一施加就立刻生效、并在<em>当回合</em>结束扣回，直接施加会变成「本回合」。</para>
/// </summary>
public sealed class HeidemarieStandReady : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		// 每点 X 的格挡
		new BlockVar(3m, ValueProp.Move),
		// 每点 X 的临时力量。
		// 与原生「预备打击」一致：卡面用 StrengthPower 的图标/数值，
		// 实际施加的是 TemporaryStrengthPower 子类（回合末自动扣回）。
		new PowerVar<StrengthPower>("Strength", 1m),
	};

	/// <summary>X 费、技能、公共牌、目标为自身、保留。</summary>
	public HeidemarieStandReady()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>X 费由引擎按「已花费能量」捕获，基准费写 0。</remarks>
	protected override bool HasEnergyCostX => true;

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Retain,
	};

	/// <inheritdoc/>
	public override bool GainsBlock => true;

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
		HoverTipFactory.FromPower<StrengthPower>(),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int x = ResolveEnergyXValue();
		if (x <= 0)
		{
			return;
		}

		await CreatureCmd.GainBlock(
			Owner.Creature,
			DynamicVars.Block.BaseValue * x,
			ValueProp.Move,
			cardPlay);

		await PowerCmd.Apply<HeidemarieStandReadyPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Strength"].BaseValue * x,
			Owner.Creature,
			this);
	}

	// 升级只加关键字「保留」（见 CanonicalKeywords），不改数值，因此不重写 OnUpgrade。
}
