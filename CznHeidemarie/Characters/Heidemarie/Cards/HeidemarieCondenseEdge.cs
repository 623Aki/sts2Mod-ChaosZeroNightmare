using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「凝锋」：0 费技能（公共牌），目标为自身。
///
/// <para>生成 2 张「极光剑」加入手牌，并<strong>赋予其「回收 1」</strong>
/// （移入墓地时回手一次）。升级后生成数 +1。</para>
///
/// <para>极光剑本身不带回收，这里通过 <see cref="HeidemarieCard.RecycleRemaining"/> 运行期赋值实现
/// 「给任意卡临时挂关键字行为」。</para>
/// </summary>
public sealed class HeidemarieCondenseEdge : HeidemarieCard
{
	/// <summary>赋予生成的极光剑的回收层数（未写明层数时默认 1 层）。</summary>
	private const int GrantedRecycle = 1;

	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new CardsVar(2),
	};

	/// <summary>0 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieCondenseEdge()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「回收」层数在建牌阶段就写进卡实例（GenerateAuroraSwords 的最后一个参数），
		// 不能等入堆之后再补 —— 卡面文字只在卡片节点建立时读一次词条状态，
		// 后补的话卡面上不会有「回收」，而悬停提示会有（见 GenerateAuroraSwords 的注释）。
		await HeidemarieCardActions.GenerateAuroraSwords(
			this,
			choiceContext,
			DynamicVars.Cards.IntValue,
			PileType.Hand,
			GrantedRecycle);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Cards.UpgradeValueBy(1m);
	}
}
