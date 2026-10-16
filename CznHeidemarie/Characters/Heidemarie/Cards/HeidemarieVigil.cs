using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「守望」：0 费技能（公共牌）。
///
/// <para>选择手牌中的 1 张牌丢弃，获得 <c>Block</c> 点格挡；
/// 若丢弃的是<b>极光剑</b>，改为获得 <c>SwordBlock</c> 点格挡。</para>
///
/// <para>本角色「主动弃牌」的入门入口：丢一把剑 = 打一下（剑自己的随机伤害）
/// + 挡一下（更高的格挡档位）。</para>
/// </summary>
public sealed class HeidemarieVigil : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	public override bool GainsBlock => true;

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new BlockVar(5m, ValueProp.Move),
		// 丢弃极光剑时的格挡档位（升级后 +3）
		new DynamicVar("SwordBlock", 10m),
	};

	/// <summary>0 费、技能、公共牌、目标为自身。</summary>
	public HeidemarieVigil()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		decimal amount = DynamicVars.Block.BaseValue;

		// 手牌为空时不弹选择界面（会弹出一个空面板），直接按普通档给格挡。
		// 注意本牌打出时已经离开手牌，所以这里数到的就是「其余手牌」。
		if (PileType.Hand.GetPile(Owner).Cards.Count > 0)
		{
			CardModel? discarded = (await CardSelectCmd.FromHandForDiscard(
				choiceContext,
				Owner,
				new CardSelectorPrefs(SelectionScreenPrompt, 1),
				null,
				this)).FirstOrDefault();

			if (discarded != null)
			{
				// 必须先丢弃再读档位：被弃的极光剑自己的「被丢弃时」伤害也在这个时机结算。
				await CardCmd.Discard(choiceContext, discarded);

				if (discarded is HeidemarieAuroraSword)
				{
					amount = DynamicVars["SwordBlock"].BaseValue;
				}
			}
		}

		await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, cardPlay);
	}

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Block),
	};

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(3m);
		DynamicVars["SwordBlock"].UpgradeValueBy(3m);
	}
}
