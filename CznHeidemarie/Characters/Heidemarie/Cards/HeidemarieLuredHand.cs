using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「被光诱惑之手」：1 费技能（稀有牌），<b>消耗</b>；升级后获得<b>保留</b>。
///
/// <para>获得 1 点力量；对<b>全体敌人</b>施加 2 层易伤。</para>
///
/// <para>本角色<strong>第一张减益牌</strong>，补上此前「虚弱 / 易伤 / 中毒全空白」的缺口。
/// 力量与易伤都用官方能力（<see cref="StrengthPower"/> / <see cref="VulnerablePower"/>），
/// 不新增本角色能力 —— 这样悬停提示、图标、叠层规则全部与官方一致。</para>
/// </summary>
public sealed class HeidemarieLuredHand : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DynamicVar("Strength", 1m),
		new DynamicVar("Vulnerable", 2m),
	};

	/// <summary>1 费、技能、稀有牌、目标为自身。</summary>
	public HeidemarieLuredHand()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	/// <remarks>升级档「赋予保留」：覆盖属性随 <c>IsUpgraded</c> 现算，不污染母本。</remarks>
	public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
		? new[] { CardKeyword.Exhaust, CardKeyword.Retain }
		: new[] { CardKeyword.Exhaust };

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<VulnerablePower>(),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<StrengthPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars["Strength"].BaseValue,
			Owner.Creature,
			this);

		// 全体敌人：走多目标重载，一次广播完整生效
		if (CombatState is { } combatState && combatState.HittableEnemies.Count > 0)
		{
			await PowerCmd.Apply<VulnerablePower>(
				choiceContext,
				combatState.HittableEnemies,
				DynamicVars["Vulnerable"].BaseValue,
				Owner.Creature,
				this);
		}
	}

	// 升级只改关键字（见 CanonicalKeywords），不改数值，因此不重写 OnUpgrade。
}
