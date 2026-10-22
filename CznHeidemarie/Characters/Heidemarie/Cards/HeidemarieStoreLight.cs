using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「积羽」：2 费技能（稀有牌）。
///
/// <para>关键字<b>安息</b>：在抽牌堆生成 <c>Cards</c> 张极光剑。</para>
/// <para>关键字<b>感应</b>：抽取本牌后，获得 <see cref="SenseLayers"/> 层<b>极夜</b>
/// （每层使下一张极光剑伤害 +100%）。</para>
/// </summary>
public sealed class HeidemarieStoreLight : HeidemarieCard, IRestCard, ISenseCard
{
	/// <summary>
	/// 感应给的极夜层数 = 3（Rare / 2 费档）。
	/// 层数按「稀有度 + 费用」定档：Common·1 费 = 1 层，Uncommon·2 费 = 2 层，Rare·2 费 = 3 层。
	/// </summary>
	private const decimal SenseLayers = 3m;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(3),
	};

	/// <inheritdoc/>
	/// <remarks>卡面写「极夜+3」，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieNightPower>(),
	};

	/// <summary>2 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieStoreLight()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	public Task OnRest(PlayerChoiceContext choiceContext) => Generate(choiceContext);

	/// <inheritdoc/>
	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
		=> Generate(choiceContext);

	private Task Generate(PlayerChoiceContext choiceContext)
		=> HeidemarieCardActions.GenerateAuroraSwords(this, choiceContext, DynamicVars.Cards.IntValue, PileType.Draw);

	/// <inheritdoc/>
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
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(2m);
}
