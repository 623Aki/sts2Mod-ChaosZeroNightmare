using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「解放极光」：0 费攻击（衍生牌 Token），目标为任意敌人。
///
/// <para>原生关键字 <b>消耗</b> / <b>虚无</b>；自定义关键字 <b>唯一</b>、<b>焚烧</b>。</para>
///
/// <para>造成 10 点伤害；消耗墓地中的<strong>所有</strong>「极光剑」，每消耗 1 张，
/// 基础伤害 +100%（百分点注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池）。</para>
///
/// <para>关键字<b>消耗</b>：打出即进消耗堆，永不回手、不回墓地。</para>
/// <para>关键字<b>虚无</b>：若回合结束时本牌仍在手中，同样被消耗。</para>
///
/// <para>关键字<b>焚烧</b>：本牌被消耗时（打出的消耗、虚无的回合末消耗，或被绝响消耗），
/// 在手牌中加入一张已升级的「凝聚极光」（自带「固有」），随后本牌<strong>移出战斗</strong>
/// （<see cref="CardPileCmd.RemoveFromCombat"/>，与能力「极光之光」清除凝聚极光同一做法）。</para>
///
/// <para>唯一获取渠道 = 能力「极光之光」：禁止随机生成（见 <c>CanBeGeneratedInCombat</c>/
/// <c>CanBeGeneratedByModifiers</c>），只由能力的代码直接创建。</para>
///
/// <para>演出走骨架的 ug 终极技动作（并挂载 ug 全屏演出）。</para>
/// </summary>
public sealed class HeidemarieLiberatedAurora : HeidemarieCard, IUniqueCard, IBurnCard
{
	/// <summary>每张被消耗的极光剑提供的增伤百分点。</summary>
	private const decimal PercentPerSword = 100m;

	/// <inheritdoc/>
	/// <remarks>
	/// 本卡走「全屏卡」，实际显示的是<b>动态卡面</b>：由
	/// <c>Registration/HeidemarieAnimatedCardArt.cs</c> + <c>scenes/cards/heidemarie_card_art.tscn</c>
	/// 把素材区 <c>CNZ_Source/czn30093/card/unique_30093_06</c> 的卡面 Spine 骨架渲染成实时贴图，
	/// 覆盖到古代布局的 <c>%AncientPortrait</c> 上。
	///
	/// <para>这里返回的静态图是<b>兜底</b>：渲染器没就绪或加载失败时，卡面至少还是一张正常的画。
	/// 它已预裁成卡面比例 300:422（420x591）—— 古代立绘窗口是硬拉伸填满
	/// （<c>expand_mode=IgnoreSize</c> + 默认 <c>stretch_mode=SCALE</c>），
	/// 不预裁就会被拉宽约 7%。</para>
	/// </remarks>
	protected override string FallbackPortraitPath => "res://CznHeidemarie/images/card_art/heidemarie_liberated_aurora.png";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(10m, ValueProp.Move),
	};

	/// <inheritdoc/>
	/// <remarks>
	/// 原生关键字 <b>虚无</b> + <b>消耗</b>：
	/// 「消耗」保证本牌一旦打出必定离开牌组（不再回墓地），
	/// 「虚无」则兜住「抽到手里没打、回合末被清掉」的情况 —— 两条路径都汇入消耗堆，
	/// 因此 <see cref="IBurnCard.OnBurn"/>（焚烧）在任何情况下都恰好触发一次。
	/// 悬停提示由 <c>CardModel.HoverTips</c> 从 <c>Keywords</c> 自动生成，无需手写。
	/// </remarks>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(2)
	{
		CardKeyword.Ethereal,
		CardKeyword.Exhaust,
	};

	/// <inheritdoc/>
	public override int MaxUpgradeLevel => 0;

	/// <inheritdoc/>
	/// <remarks>
	/// 不能通过除「极光之光」外任何渠道获得：禁止随机生成
	/// （战斗内随机生成由 <see cref="IUniqueCard"/> 在基类统一禁掉；这里再禁掉 modifier 池）。
	/// 能力自己的 <c>CreateCard</c> 不受这两个开关限制。
	/// </remarks>
	public override bool CanBeGeneratedByModifiers => false;

	/// <summary>
	/// 0 费、攻击、<b>古代牌</b>（= 全幅立绘卡）、目标为任意敌人。
	///
	/// <para><b>为什么稀有度是 <see cref="CardRarity.Ancient"/></b>：这就是本卡「全屏卡」的实现方式 ——
	/// 游戏的 <c>NCard</c> 对古代牌有一整套现成的全幅立绘布局（显示 <c>%AncientPortrait</c> 铺满整卡、
	/// <c>%AncientBorder</c> 边框、<c>%AncientTextBg</c> 文字底板、<c>%AncientBanner</c> 标题横幅，
	/// 同时隐藏 <c>%Portrait</c> / <c>%PortraitBorder</c> / <c>%Frame</c>），
	/// 参考 mod 的「超究武神霸斩」用的也是这一招，不需要任何补丁或自绘几何。</para>
	///
	/// <para>副作用为零：本卡不在 <see cref="HeidemarieCardPool"/> 里，
	/// 因此奖励 / 商店 / 变形 / <c>DustyTome</c> 的「随机古代牌」都取不到它。</para>
	/// </summary>
	public HeidemarieLiberatedAurora()
		: base(0, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy)
	{
	}

	/// <inheritdoc/>
	public async Task OnBurn(PlayerChoiceContext choiceContext, bool causedByEthereal)
	{
		if (Owner.Creature.CombatState is not { } combatState)
		{
			return;
		}

		// 焚烧：手牌加入一张「凝聚极光」并赋予升级（获得「固有」），重启引擎
		CardModel condensed = combatState.CreateCard<HeidemarieCondensedAurora>(Owner);
		// 生成即升级：必须在入堆前完成，卡面文本是卡片节点建立时读取的
		CardCmd.Upgrade(condensed);
		await CardPileCmd.AddGeneratedCardToCombat(condensed, PileType.Hand, Owner);

		// 焚烧：本牌移出战斗（此时本牌已在消耗堆，属战斗牌堆，可安全移除）
		await CardPileCmd.RemoveFromCombat(this);
	}

	/// <inheritdoc/>
	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 消耗墓地中所有极光剑，每张 +100%
		List<CardModel> swords = PileType.Discard.GetPile(Owner).Cards
			.Where(c => c is HeidemarieAuroraSword)
			.ToList();

		foreach (CardModel sword in swords)
		{
			await CardCmd.Exhaust(choiceContext, sword);
		}

		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		// 每消耗 1 张极光剑 +100%；入池与清池由基类包裹
		// （见 HeidemarieCard.WithPercentDamageBoost，攻击结束必定清池）。
		await WithPercentDamageBoost(choiceContext, swords.Count * PercentPerSword, async () =>
		{
			await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
				.FromCard(this, cardPlay)
				.WithAttackerAnim(HeidemarieAttackPlan.Ug.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.Ug))
				.Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_rock_shatter")
				.Execute(choiceContext);
		});
	}
}
