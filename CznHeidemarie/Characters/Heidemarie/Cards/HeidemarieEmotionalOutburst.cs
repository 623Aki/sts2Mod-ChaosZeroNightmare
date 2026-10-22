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
/// 「情绪爆发」：2 费能力（罕见牌），关键字<b>固有</b>。
///
/// <para>获得能力「情绪爆发」：每丢弃 4 张牌，获得 1 点能量（回合末弃牌不算）。</para>
///
/// <para><b>计数口径</b>：只认「自己玩家的牌进弃牌堆」，来源是手牌还是抽牌堆、
/// 由谁丢弃（自己的卡／「连接」的连带丢弃／敌方效果）都算；
/// 回合结束的清手牌不算（官方清手牌不派发丢弃钩子）。详见
/// <see cref="HeidemarieEmotionalOutburstPower"/>。</para>
///
/// <para><b>「固有」是原生关键字</b>（<see cref="CardKeyword.Innate"/>，见源码枚举），
/// 直接用 <c>CanonicalKeywords</c> 声明即可，不需要自定义词条 ——
/// 开局自动摸到手，所以这是本角色第一张「起手必到」的引擎牌。</para>
///
/// <para>计数器细节见 <see cref="HeidemarieEmotionalOutburstPower"/>：
/// 用私有字段而非层数计数，但<b>图标上的数字显示的就是已丢弃张数</b>（0~3，满 4 归零）；
/// 打多张会显示多个能力图标，各自记进度。</para>
/// </summary>
public sealed class HeidemarieEmotionalOutburst : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(1)
	{
		CardKeyword.Innate,
	};

	/// <summary>2 费、能力、罕见牌、目标为自身、固有。</summary>
	public HeidemarieEmotionalOutburst()
		: base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.Static(StaticHoverTip.Energy),
	};

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 1 层 = 「每 4 张发 1 能量」的档位（阈值写死在能力里）
		await PowerCmd.Apply<HeidemarieEmotionalOutburstPower>(
			choiceContext,
			Owner.Creature,
			1m,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
