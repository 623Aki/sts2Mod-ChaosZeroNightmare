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
/// 「极光预兆」：2 费技能（罕见牌）。
///
/// <para>关键字<b>安息</b>：在手牌生成 <c>Cards</c> 张极光剑
/// （打出与安息发动共用同一段效果）。</para>
///
/// <para>关键字<b>感应</b>：抽取本牌后，获得 <see cref="SenseLayers"/> 层<b>极夜</b>
/// （每层使下一张极光剑伤害 +100%）。</para>
///
/// <para>升级：生成张数 +1（2 → 3）。感应层数不随升级变化 —— 那是关键字效果，
/// 与「积羽 / 剑光」等牌同一口径（升级只动主效果）。</para>
/// </summary>
public sealed class HeidemarieAuroraOmen : HeidemarieCard, IRestCard, ISenseCard
{
	/// <summary>
	/// 感应给的极夜层数 = 2（Uncommon·2 费档）。
	/// 层数按「稀有度 + 费用」定档：Common·1 费 = 1 层，Uncommon·2 费 = 2 层，Rare·2 费 = 3 层。
	/// </summary>
	private const decimal SenseLayers = 2m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 生成到手牌的极光剑张数（升级 +1）
		new CardsVar(2),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>2 费、技能、罕见牌、目标为自身。</summary>
	public HeidemarieAuroraOmen()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>关键字「安息」：被非回合结束丢弃时发动，效果与打出一致（生成极光剑）。</remarks>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Generate(choiceContext);

	/// <summary>本牌主效果（打出 / 安息发动共用）：在手牌生成极光剑。</summary>
	private Task Generate(PlayerChoiceContext choiceContext)
		=> HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Hand);

	/// <inheritdoc/>
	/// <remarks>关键字「感应」：从抽牌堆进入手牌时获得极夜（由基类统一分派）。</remarks>
	public async Task OnSense(PlayerChoiceContext choiceContext)
	{
		await PowerCmd.Apply<HeidemarieNightPower>(
			choiceContext,
			Owner.Creature,
			SenseLayers,
			Owner.Creature,
			this,
			silent: true);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
