using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznHeidemarie.Characters.Heidemarie.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using CznHeidemarie.Characters.Heidemarie.Presentation;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// 「终焉」：3 费攻击（先古牌 / 古代牌），对<strong>全体敌人</strong>。
///
/// <para>由「绝响」经「太古之牙」<strong>淬炼</strong>而来 —— 与参考 mod
/// （CznNineCharacter「反射 → 反射：零」）同一路径：转变关系注册在
/// <see cref="MegaCrit.Sts2.Core.Models.Relics.ArchaicTooth"/> 的
/// <c>TranscendenceUpgrades</c> 表里（见
/// <c>Registration/HeidemarieAncientTranscendencePatch.cs</c>），
/// 玩家获得太古之牙时由引擎自动换牌，并继承原牌的升级状态与附魔。</para>
///
/// <para>原生关键字 <b>消耗</b> / <b>保留</b>；自定义关键字 <b>终结</b>（用后立即结束本回合）、
/// <b>唯一</b>（卡组内仅可持有 1 张）。升级后<strong>移除「消耗」</strong>。</para>
///
/// <para>对全体敌人造成 20 点伤害；若手牌中存在「解放极光」，则将其消耗，此时墓地中每有 1 张
/// 「极光剑」，本次伤害 +100%（百分点注入 <see cref="HeidemarieDamagePercentPower"/> 增伤池，
/// 与剑光等其他百分比增伤<strong>相加</strong>）。</para>
///
/// <para><b>消耗减费</b>（口径与「过期的星星」一致）：本场战斗中，
/// <strong>任何人</strong>每消耗过一张牌，本牌费用 -1
/// （<c>EnergyCost.AddUntilPlayed(-1, reduceOnly: true)</c>）；
/// <b>打出后费用恢复原值</b>（引擎在 <c>AfterCardPlayedCleanup</c> 清掉 WhenPlayed 层），
/// 之后重新按消耗次数累计。因本牌带<b>保留</b>，减费可跨回合累积，
/// 但不会像旧实现（<c>AddThisCombat</c>）那样「减下去就永久不恢复」。</para>
///
/// <para>演出走骨架的重·斩击档（<c>U1Attack</c>）。卡面为动态立绘（素材
/// <c>unique_30093_01.skel</c>，参考「解放极光」），由
/// <c>Registration/HeidemarieAnimatedCardArt.cs</c> 接管 <c>%AncientPortrait</c>。</para>
/// </summary>
public sealed class HeidemarieTerminus : HeidemarieCard, ITerminateCard, IUniqueCard
{
	/// <summary>每张极光剑提供的增伤百分点。</summary>
	private const decimal PercentPerSword = 100m;

	/// <inheritdoc/>
	/// <remarks>
	/// 本卡走「全屏卡」（古代牌），实际显示的是动态立绘；这里返回的是<strong>兜底</strong>：
	/// 渲染器没就绪或加载失败时，卡面至少还是一张正常的画（引擎「卡面缺失」占位图）。
	/// </remarks>
	protected override string FallbackPortraitPath => "res://images/atlases/card_atlas.sprites/beta.tres";

	/// <inheritdoc/>
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>(1)
	{
		new DamageVar(20m, ValueProp.Move),
	};

	/// <inheritdoc/>
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword>(2)
	{
		CardKeyword.Exhaust,
		CardKeyword.Retain,
	};

	/// <summary>3 费、攻击、古代牌、对全体敌人。</summary>
	public HeidemarieTerminus()
		: base(3, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
	{
	}

	/// <inheritdoc/>
	/// <summary>演出档位：重击（u1_attack_ready → u1_attack_play）。动作与特效档位必须一致。</summary>
	protected override HeidemarieEffectTier PresentationTier => HeidemarieEffectTier.AttackHeavy;

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 若手牌中有解放极光 → 消耗，并按墓地里的极光剑数量叠增伤
		CardModel? liberated = PileType.Hand.GetPile(Owner).Cards
			.FirstOrDefault(c => c is HeidemarieLiberatedAurora);

		int swords = 0;
		if (liberated != null)
		{
			await CardCmd.Exhaust(choiceContext, liberated);
			swords = PileType.Discard.GetPile(Owner).Cards.Count(c => c is HeidemarieAuroraSword);
		}

		// 墓地每有 1 张极光剑 +100%；入池与清池由基类包裹
		// （见 HeidemarieCard.WithPercentDamageBoost，攻击结束必定清池）。
		await WithPercentDamageBoost(choiceContext, swords * PercentPerSword, async () =>
		{
			await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
				.FromCard(this, cardPlay)
				.WithAttackerAnim(HeidemarieAttackPlan.U1.Trigger, HeidemarieAttackPlan.HitDelay(HeidemarieAttackPlan.U1))
				.TargetingAllOpponents(CombatState!)
				.WithHitFx(VfxCmd.bloodyImpactPath)
				.Execute(choiceContext);
		});

		// 终结：打出后立即结束本回合（走游戏原生回合结束逻辑）
		PlayerCmd.EndTurn(Owner, canBackOut: false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 午夜（<c>Midnight</c>）同款的「补历史」：本牌中途进入战斗时
	/// （淬炼生成 / 洗入手牌等），按此前已发生的消耗次数一次性减费，
	/// 保证费用与战斗历史同步。复制品（IsClone）不参与。
	///
	/// <para>与「过期的星星」同一口径：走 <c>AddUntilPlayed</c>，
	/// 这笔补历史同样会在**打出后**被清掉（恢复原值），不会变成永久减费。</para>
	/// </remarks>
	public override Task AfterCardEnteredCombat(CardModel card)
	{
		if (card == this && !IsClone)
		{
			int amount = CombatManager.Instance.History.Entries.OfType<CardExhaustedEntry>().Count();
			if (amount > 0)
			{
				EnergyCost.AddUntilPlayed(-amount, reduceOnly: true);
			}
		}

		return base.AfterCardEnteredCombat(card);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 减费钩子：本场战斗中任何人每消耗一张牌，本牌费用 -1。
	/// <b>不判断 <c>card != this</c></b> —— 与午夜一致，
	/// 消耗任何牌（含本牌、含其他角色的牌）都计数。
	///
	/// <para><b>为什么用 <c>AddUntilPlayed</c> 而不是 <c>AddThisCombat</c></b>
	/// （与「过期的星星」一致）：</para>
	/// <list type="bullet">
	///   <item>相对 -1、<c>reduceOnly</c> 只在能降低当前费用时生效（0 封底）；</item>
	///   <item>打出本牌后由引擎 <c>AfterCardPlayedCleanup</c> 清空 WhenPlayed 层，
	///   费用恢复原值，之后再重新累计 —— 旧实现是永久减费，打出后还在继续便宜。</item>
	/// </list>
	///
	/// <para>基类的「焚烧」派发逻辑照常走（本牌不是焚烧牌，实际为空走）。</para>
	/// </remarks>
	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		// 已经减到 0 就不用再叠层：避免战斗中堆出几十层无效修正。
		if (EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
		{
			EnergyCost.AddUntilPlayed(-1, reduceOnly: true);
		}

		await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
	}

	/// <inheritdoc/>
	protected override void OnUpgrade()
	{
		// 升级：移除「消耗」
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
