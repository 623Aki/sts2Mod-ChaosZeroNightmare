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
/// 「极光连锁」：2 费能力牌（稀有牌），升级后 1 费（设计文档 §5.1）。
///
/// <para>打出后获得能力<b>极光连锁</b>：每当你<strong>直接使用</strong>或<strong>丢弃</strong>
/// 1 张带<b>连接</b>的牌，获得 1 层（最多 10 层）；每层使你的攻击伤害 +5%，
/// 与「百分比增伤池」<b>加算</b>（见 <see cref="HeidemarieAuroraChainPower"/>）。</para>
///
/// <para>与遗物「赤光之誓」共用同一套触发判定
/// （<see cref="HeidemarieCardActions.IsLinkTrigger"/> / <see cref="HeidemarieCardActions.IsLinkDiscard"/>），
/// 避免两处口径分叉（§7.3 #3）。</para>
/// </summary>
public sealed class HeidemarieAuroraChain : HeidemarieCard
{
	/// <inheritdoc/>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/ironclad/defend_ironclad.tres";

	/// <summary>
	/// 卡面文本里的两个数字：每层百分点与层数上限。
	/// 能力本体读的是 <see cref="HeidemarieAuroraChainPower.PercentPerStack"/> 等常量，两边必须一致。
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(2)
	{
		new DynamicVar("PerStack", HeidemarieAuroraChainPower.PercentPerStack),
		new DynamicVar("MaxStacks", HeidemarieAuroraChainPower.MaxStacks),
	};

	/// <inheritdoc/>
	/// <remarks>卡面里的「极光连锁」是能力名，挂上该能力的悬停提示便于查层数含义。</remarks>
	protected override IEnumerable<IHoverTip> ExtraCardHoverTips => new[]
	{
		HoverTipFactory.FromPower<HeidemarieAuroraChainPower>(),
	};

	/// <summary>2 费、能力、稀有牌、目标为自身。</summary>
	public HeidemarieAuroraChain()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<HeidemarieAuroraChainPower>(
			choiceContext,
			Owner.Creature,
			1m,
			Owner.Creature,
			this);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		// 升级：费用 -1（2 费 → 1 费）
		EnergyCost.UpgradeBy(-1);
	}
}
