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
/// 「壁仞千里」：1 费能力牌（罕见牌）。
///
/// <para>打出后获得能力<b>壁仞千里</b>：每丢弃 1 张牌获得 2 点格挡；
/// 若丢弃的是极光剑，改为 3 点（见 <see cref="HeidemarieWallOfSwordsPower"/>）。</para>
///
/// <para>把「连接」的连带丢弃从代价变成收益，是「凝锋（铸剑）」的对仗卡。</para>
///
/// <para><b>升级 = 单层数值上调 + 多施加 1 层（4 / 6）</b>：写法与官方 <c>FeelNoPain</c> 一致 ——
/// 卡牌自己声明 <see cref="CardModel.CanonicalVars"/>（卡面文本靠它插值）
/// 与 <see cref="CardModel.OnUpgrade"/>（升级只动这些数值），
/// 能力本体不判升级状态，层数仍由 <see cref="PowerModel.Amount"/> 承担。</para>
///
/// <para>因此「基础数值 / 升级数值」在本卡与 <see cref="HeidemarieWallOfSwordsPower"/>
/// 各存一份，升级后二者必须仍对齐（卡面 4 / 6 ⇔ 施加 2 层 × 每层 2 / 3）。</para>
/// </summary>
public sealed class HeidemarieWallOfSwords : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>
	/// 卡面文本的两个数值：普通牌档与极光剑档。
	///
	/// <para>必须与 <see cref="HeidemarieWallOfSwordsPower"/> 的单层数值逐字一致 ——
	/// 卡面写 2 / 4，能力就按 2 / 4 给，多层再乘层数。</para>
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		// 普通牌档：每弃 1 张牌获得的格挡（与能力同值、同 ValueProp，卡面与能力面板显示一致）
		new BlockVar(2m, ValueProp.Unpowered),
		// 极光剑档：丢弃的是极光剑时改用这一档（数值回标 4 → 3，《新增设计》§3.2 #2）
		new DynamicVar("SwordBlock", 3m),
	};

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <summary>1 费、能力、罕见牌、目标为自身。</summary>
	public HeidemarieWallOfSwords()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 层数即「份数」：未升级 1 层（2 / 4），升级 2 层（4 / 8）
		await PowerCmd.Apply<HeidemarieWallOfSwordsPower>(
			choiceContext,
			Owner.Creature,
			IsUpgraded ? 2m : 1m,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 升级：仍是「多施加 1 层」，但两档按裁决值改（《新增设计》§3.2 #2）——
	/// 普通档 2 → 4（+2），极光剑档 3 → 6（+3），2 层后即 4 / 6。
	/// </remarks>
	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(2m);
		DynamicVars["SwordBlock"].UpgradeValueBy(3m);
	}
}
