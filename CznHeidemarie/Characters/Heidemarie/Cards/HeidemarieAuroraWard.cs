using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「极光护壁」：1 费技能（公共牌）。
///
/// <para>关键字<b>连接</b>。获得 <c>Block</c> 点格挡；打出前手牌中每有 1 张带「连接」的牌
/// （含自身），格挡再 +<c>Bonus</c>。</para>
///
/// <para>是「一缕光芒」的格挡镜像版。计数口径与它一致：卡面写了「打出前」，因此
/// <b>含自身</b>（见 <see cref="HeidemarieCard.ResolveLinkedCountBeforePlay"/>）。</para>
/// </summary>
public sealed class HeidemarieAuroraWard : HeidemarieCard, ILinkCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	/// <remarks>格挡牌必须声明 <see cref="CardModel.GainsBlock"/>，否则无法被相关效果识别。</remarks>
	public override bool GainsBlock => true;

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new BlockVar(6m, ValueProp.Move),
		// 每张连接牌追加的格挡量（升级后 +1）
		new DynamicVar("Bonus", 2m),
	};

	/// <summary>1 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieAuroraWard()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 「连接」的连带丢弃已由基类 BeforeCardPlayed 在出牌前完成；
		// 计数同样在那里、丢弃之前取好快照 —— 若在这里实时数，数到的是已被丢空的手牌。
		int linked = ResolveLinkedCountBeforePlay(includeSelf: true);

		decimal total = DynamicVars.Block.BaseValue + DynamicVars["Bonus"].BaseValue * linked;
		await CreatureCmd.GainBlock(Owner.Creature, total, ValueProp.Move, cardPlay);
	}

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <inheritdoc/>
	/// <remarks>数值回标（《新增设计》§3.2 #1）：基础 6 → 9（+3），每连接 2 → 3（+1）。</remarks>
	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(3m);
		DynamicVars["Bonus"].UpgradeValueBy(1m);
	}
}
