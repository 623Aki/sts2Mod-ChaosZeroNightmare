using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「长明」：1 费能力牌（罕见牌）。
///
/// <para>打出后获得能力<b>长明</b>：我方回合开始时，获得 <c>Layers</c> 层<b>极夜</b>
/// （每层使下一张极光剑伤害 +100%，生效一次层数 -1）。</para>
///
/// <para><b>升级 = 每回合层数 +2（2 → 4）</b>：与「壁仞千里」同一写法 ——
/// 卡牌自己声明 <see cref="CanonicalVars"/>（卡面文本插值）与 <see cref="OnUpgrade"/>
/// （升级只动数值），能力本体不判升级状态，层数仍由 <see cref="PowerModel.Amount"/> 承担。</para>
///
/// <para>可叠加：打出 N 张「长明」，能力层数累加，回合开始发放的极夜 = 2N（升级档 4N）。</para>
/// </summary>
public sealed class HeidemarieEverlight : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>
	/// 卡面文本数值：每回合开始时获得的极夜层数。
	/// 必须与 <see cref="OnPlay"/> 实际施加的层数一致（基础 2，升级 4）。
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DynamicVar("Layers", 2m),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>1 费、能力、罕见牌、目标为自身。</summary>
	public HeidemarieEverlight()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<HeidemarieEverlightPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Layers"].IntValue,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	/// <remarks>升级：每回合获得的极夜层数 +2（2 → 4）。</remarks>
	protected override void OnUpgrade() => DynamicVars["Layers"].UpgradeValueBy(2m);
}
