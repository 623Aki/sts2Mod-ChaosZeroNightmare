using System.Collections.Generic;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「无脸女」：1 费能力（稀有牌）。获得能力「无脸女」：
/// 每当你<b>打出</b>一张带「连接」的牌，抽 <c>Cards</c> 张牌。
///
/// <para>这是本角色的<strong>第二条过牌引擎</strong>（第一条是「拾星」的安息抽牌）：
/// 把「连接」从「消耗手牌」变成「换手牌」，与「无脸女」本身不带连接、
/// 因此不会自我连锁。</para>
///
/// <para>触发口径见 <see cref="HeidemarieFacelessWomanPower"/>（自动打出不计）。</para>
/// </summary>
public sealed class HeidemarieFacelessWoman : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		// 每次触发抽几张（升级 +1）
		new CardsVar(1),
	};

	/// <summary>1 费、能力、稀有牌、目标为自身。</summary>
	public HeidemarieFacelessWoman()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<HeidemarieFacelessWomanPower>(
			choiceContext,
			Owner.Creature,
			DynamicVars.Cards.BaseValue,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
