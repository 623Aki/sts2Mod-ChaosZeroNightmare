using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「旧誓」：2 费能力牌（稀有牌）。
///
/// <para>打出后获得能力<b>旧誓</b>：回合开始时，弃牌堆和消耗牌堆中每有 1 张极光剑，
/// 获得 2 点格挡（见 <see cref="HeidemarieOldOathPower"/>）。</para>
///
/// <para>把「剑冢」从一次性重放变成<b>持续防御收益</b>：「守墓」把极光剑永久堆在弃牌堆、
/// 「解放极光」等消耗路径把剑送进消耗堆，两条路线都喂这条能力 —— 这是官方没有的机制。</para>
///
/// <para><b>升级 = 每张剑 4 点格挡</b>：写法与官方 <c>FeelNoPain</c> 一致 ——
/// 卡牌自己声明 <see cref="CardModel.CanonicalVars"/>（卡面文本靠它插值）
/// 与 <see cref="CardModel.OnUpgrade"/>（升级只动这些数值），
/// 能力本体不判升级状态，层数仍由 <see cref="PowerModel.Amount"/> 承担。</para>
///
/// <para>因此「基础数值 / 升级数值」在本卡与 <see cref="HeidemarieOldOathPower"/>
/// 各存一份，升级后二者必须仍对齐（卡面 4 ⇔ 施加 2 层 × 每层 2）。</para>
/// </summary>
public sealed class HeidemarieOldOath : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>
	/// 卡面文本的数值：弃牌堆和消耗牌堆中每 1 张极光剑在回合开始时给的格挡。
	///
	/// <para>必须与 <see cref="HeidemarieOldOathPower"/> 的单层数值逐字一致。</para>
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 与能力同值、同 ValueProp：卡面与能力面板显示一致
		new BlockVar(2m, ValueProp.Unpowered),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <summary>2 费、能力、稀有牌、目标为自身。</summary>
	public HeidemarieOldOath()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 层数即「份数」：未升级 1 层（每张剑 2 点），升级 2 层（每张剑 4 点）
		await PowerCmd.Apply<HeidemarieOldOathPower>(
			choiceContext,
			Owner.Creature,
			IsUpgraded ? 2m : 1m,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	/// <remarks>升级：每张剑的格挡 +2（2 → 4），与「多施加 1 层」等效。</remarks>
	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(2m);
	}
}
