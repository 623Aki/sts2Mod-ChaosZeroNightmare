using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznCore.Cards;
using CznHeidemarie.Characters.Heidemarie.Powers;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace CznHeidemarie.Characters.Heidemarie.Cards;

/// <summary>
/// Heidemarie 全部卡牌的公共基类。
///
/// <para>职责：</para>
/// <list type="bullet">
///   <item>统一把 <see cref="CardModel.Pool"/> 指到 <see cref="HeidemarieCardPool"/>；</item>
///   <item>在专属卡面绘制完成前，提供一张可用的占位立绘（见 <see cref="PortraitPath"/>）；</item>
///   <item>把 8 个自定义关键字的行为<strong>集中分派</strong>到对应接口方法
///   （感应 / 安息 / 回收 / 守墓 / 焚烧），各卡只实现接口，不重复写 <c>if (card == this)</c>；</item>
///   <item>把自定义关键字转成卡面悬停提示（见 <see cref="ExtraHoverTips"/>）。</item>
/// </list>
///
/// <para>关键字「连接」「终结」「唯一」是纯标记（行为写在出牌逻辑 / 加牌入口里），
/// 只借接口完成词条显示。</para>
/// </summary>
public abstract class HeidemarieCard : CardModel
{
	/// <summary>卡面路径缓存，避免每帧重复调用 <see cref="ResourceLoader.Exists(string)"/>。</summary>
	private static readonly Dictionary<string, string> PortraitPathCache = new Dictionary<string, string>(System.StringComparer.Ordinal);

	/// <summary>「回收」剩余层数。<c>null</c> 表示尚未改动，读 <see cref="CanonicalRecycle"/>。</summary>
	private int? _recycleRemaining;

	/// <summary>回收回手时的重入保护（避免 <c>AfterCardChangedPiles</c> 递归）。</summary>
	private bool _recycling;

	/// <summary>专属卡面缺失时使用的占位立绘（战士同名基础卡）。</summary>
	protected abstract string FallbackPortraitPath { get; }

	/// <summary>
	/// 卡牌自带的「回收 N」层数（如「凝聚极光」为 3）。
	/// 默认 0 —— 表示本身不带回收，只能被「凝锋」这类效果临时赋予。
	/// </summary>
	protected virtual int CanonicalRecycle => 0;

	/// <summary>
	/// 「回收」剩余层数：移入墓地时若大于 0，则返回手牌并减 1。
	/// 显示规则遵循设计文档：1 层只显示「回收」，&gt;1 才显示「回收N」。
	/// </summary>
	public int RecycleRemaining
	{
		get => _recycleRemaining ?? CanonicalRecycle;
		set => _recycleRemaining = value;
	}

	/// <summary>
	/// 「安息」：本回合内本牌是否被禁止移出墓地。
	/// 由 <see cref="IRestCard"/> 在丢弃时置位，回合开始（<see cref="AfterPlayerTurnStart"/>）清除。
	/// </summary>
	public bool RestLockedThisTurn { get; private set; }

	/// <summary>运行期「连接」标记（本回合）—— 卡牌效果临时赋予，回合开始时清除。</summary>
	public bool RuntimeLinkedThisTurn { get; private set; }

	/// <summary>
	/// 运行期「连接」标记（本场战斗）—— 描述未限定「本回合」的赋予走这里，持续到战斗结束。
	/// 战斗牌堆在每场战斗使用独立卡实例，因此无需手动清。
	/// </summary>
	public bool RuntimeLinkedThisCombat { get; private set; }

	/// <summary>运行期「安息」标记（本场战斗）—— 例如「北极圈」升级后赋予生成的极光剑。</summary>
	public bool RuntimeRestThisCombat { get; private set; }

	/// <summary>
	/// 本牌本次「打出前」手牌中带「连接」的牌数（<strong>不含自身</strong>）。
	///
	/// <para>由 <see cref="BeforeCardPlayed"/> 在「丢弃其他连接牌」<strong>之前</strong>记录，
	/// 供「打出前计数」类卡牌（一缕光芒 / 叠光奏浪 / 万剑诀）读取 ——
	/// 这些牌若在 <c>OnPlay</c> 里自己数，数到的是已经被丢干净的手牌，永远为 0。</para>
	///
	/// <para>-1 = 本次打出没有记录（例如卡牌未带「连接」）；此时用
	/// <see cref="ResolveLinkedCountBeforePlay"/> 兜底实时统计。</para>
	/// </summary>
	public int LinkedCountBeforePlay { get; private set; } = -1;

	/// <summary>
	/// 取「打出前连接牌数」。
	///
	/// <para><b>计数口径（设计约定，改动请先确认）</b>：
	/// 卡面描述写了「打出前」的卡，统计时<strong>包含自身</strong>；
	/// 描述里没写「打出前」的，<strong>不含自身</strong>。
	/// 因此本方法要求调用方<strong>显式</strong>传 <paramref name="includeSelf"/>，
	/// 不要偷懒默认相加 —— 口径是卡面文案的一部分。</para>
	/// </summary>
	/// <param name="includeSelf">是否把本牌自身计入（描述含「打出前」时为 <c>true</c>）。</param>
	protected int ResolveLinkedCountBeforePlay(bool includeSelf)
	{
		int linkedInHand = LinkedCountBeforePlay >= 0
			? LinkedCountBeforePlay
			: HeidemarieCardActions.CountLinkedInHand(this, includeSelf: false);

		if (includeSelf && IsLinked)
		{
			linkedInHand++;
		}

		return linkedInHand;
	}

	/// <summary>
	/// 是否带「连接」：静态接口或运行期标记任一成立。
	///
	/// <para>虚属性 —— 子类可扩展判定条件。例如「拾星」升级后是<b>卡牌本体</b>获得「连接」
	/// （而非实现 <see cref="ILinkCard"/>），它就覆盖本属性把 <c>IsUpgraded</c> 也算进来
	/// （见 <see cref="HeidemarieGatherStar.IsLinked"/>）——
	/// 与「北极圈」升级后覆盖 <see cref="IsRestCard"/> 是同一套做法。</para>
	/// </summary>
	public virtual bool IsLinked => this is ILinkCard || RuntimeLinkedThisTurn || RuntimeLinkedThisCombat;

	/// <summary>
	/// 是否带「安息」：静态接口或运行期标记任一成立。
	///
	/// <para>虚属性 —— 子类可扩展判定条件。例如「北极圈」升级后是<b>卡牌本体</b>获得「安息」
	/// （而非实现 <see cref="IRestCard"/>），它就覆盖本属性把 <c>IsUpgraded</c> 也算进来
	/// （见 <see cref="HeidemarieNorthPolar.IsRestCard"/>）。</para>
	/// </summary>
	public virtual bool IsRestCard => this is IRestCard || RuntimeRestThisCombat;

	/// <summary>临时赋予「连接」（本回合）。</summary>
	public void GrantRuntimeLinkThisTurn() => RuntimeLinkedThisTurn = true;

	/// <summary>临时赋予「连接」（本场战斗）。</summary>
	public void GrantRuntimeLinkThisCombat() => RuntimeLinkedThisCombat = true;

	/// <summary>临时赋予「安息」（本场战斗）。</summary>
	public void GrantRuntimeRestThisCombat() => RuntimeRestThisCombat = true;

	/// <summary>
	/// 「安息」效果入口：静态 <see cref="IRestCard"/> 与运行期赋予的「安息」共用同一派发点。
	/// </summary>
	protected virtual Task OnRestEffect(PlayerChoiceContext choiceContext)
		=> this is IRestCard rest ? rest.OnRest(choiceContext) : Task.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// 「唯一」牌一律不允许被战斗内随机生成（攻击药水 / Discovery 之类），
	/// 以免出现第二张副本（「唯一」的判定范围是卡组，见 Registration/HeidemarieUniquePatch.cs）。
	/// </remarks>
	public override bool CanBeGeneratedInCombat => this is not IUniqueCard;

	/// <inheritdoc/>
	public override CardPoolModel Pool => ModelDb.CardPool<HeidemarieCardPool>();

	/// <inheritdoc/>
	/// <remarks>
	/// 取卡顺序：
	/// <list type="number">
	///   <item>本角色卡池的规范路径 <c>res://images/atlases/card_atlas.sprites/heidemarie/{卡牌ID}.tres</c>；</item>
	///   <item>该文件不存在时退回 <see cref="FallbackPortraitPath"/>。</item>
	/// </list>
	/// 因此后续只要把 heidemarie_*.tres 放进上述目录，游戏会自动切换卡面，无需改代码。
	/// </remarks>
	public override string PortraitPath
	{
		get
		{
			string ownPath = ImageHelper.GetImagePath(
				$"atlases/card_atlas.sprites/{Pool.Title.ToLowerInvariant()}/{Id.Entry.ToLowerInvariant()}.tres");

			if (!PortraitPathCache.TryGetValue(ownPath, out string? resolved))
			{
				resolved = ResourceLoader.Exists(ownPath) ? ownPath : FallbackPortraitPath;
				PortraitPathCache[ownPath] = resolved;
			}

			return resolved;
		}
	}

	/// <summary>
	/// 技能 / 能力牌的默认身体动作序列（<c>buff_ready → buff_play</c>）。
	///
	/// <para>默认给技能与能力牌；攻击牌返回 <c>null</c>（它们的动作走
	/// <c>AttackCommand.WithAttackerAnim</c>），已有专属演出的牌自己覆盖
	/// <see cref="OnEnqueuePlayVfx"/>（如「防御」用 <c>defense_ready → defense_play</c>）。</para>
	/// </summary>
	protected virtual string[]? SkillBodySequence =>
		Type is CardType.Skill or CardType.Power ? SkillBodyTier() : null;

	/// <summary>
	/// 技能 / 能力牌的身体动作档位：**稀有度与费用取更重者**。
	///
	/// <list type="bullet">
	///   <item>Rare（或 ≥ 3 费）→ 蓄势档 <c>u4_buff_ready → u4_buff_play</c>（全骨架最长的增益动作）；</item>
	///   <item>Uncommon（或 2 费）→ 结阵档 <c>u2_buff_ready → u2_buff_play</c>；</item>
	///   <item>Basic / Common → 施法档 <c>buff_ready → buff_play</c>。</item>
	/// </list>
	///
	/// <para>与 <c>effects/heidemarie_moves.json</c> 里同档位的特效组键逐条对应
	/// （<c>eff_u4_buff_*</c> / <c>eff_u2_buff_*</c> / <c>eff_casting_ready</c>），
	/// 改这里必须同步改那张表，否则动作与特效会错位。段落在骨架上缺失时
	/// <see cref="OnEnqueuePlayVfx"/> 会降级回 <c>Cast</c> / <c>PowerUp</c> 触发器。</para>
	/// </summary>
	protected string[] SkillBodyTier()
	{
		if (Rarity == CardRarity.Rare || CanonicalCostValue >= 3)
		{
			return new[] { "u4_buff_ready", "u4_buff_play" };
		}

		if (Rarity == CardRarity.Uncommon || CanonicalCostValue >= 2)
		{
			return new[] { "u2_buff_ready", "u2_buff_play" };
		}

		return new[] { "buff_ready", "buff_play" };
	}

	/// <summary>本牌带的自定义关键字（默认由接口推导，见 <see cref="HeidemarieCardMechanics"/>）。</summary>
	protected virtual IEnumerable<CardMechanicDefinition> CardMechanics => HeidemarieCardMechanics.For(this);

	/// <inheritdoc/>
	/// <remarks>
	/// 技能 / 能力牌的统一身体演出：出牌瞬间先起 <see cref="SkillBodySequence"/>，
	/// 让时间轴里的单体特效有动作可以贴 —— 否则角色停在待机、特效孤零零地亮一下。
	/// 任一动画缺失时降级回状态机的 <c>Cast</c> / <c>PowerUp</c> 触发器。
	///
	/// <para>与「防御」同一时机（早于 <c>OnPlay</c> 的数值结算）且不阻塞出牌；
	/// 动作序列末尾会自动排待机，不会停在最后一帧。</para>
	/// </remarks>
	public override async Task OnEnqueuePlayVfx(Creature? target)
	{
		string[]? sequence = SkillBodySequence;
		if (sequence is null || sequence.Length == 0)
		{
			await base.OnEnqueuePlayVfx(target);
			return;
		}

		if (!HeidemarieAnimation.PlaySequence(Owner.Creature, sequence))
		{
			await HeidemarieAnimation.PlayTriggerAsync(
				Owner.Creature,
				Type == CardType.Power ? "PowerUp" : "Cast",
				HeidemarieAnimationSeconds.BuffReady);
		}
	}

	/// <inheritdoc/>
	/// <remarks>自定义关键字转成悬停提示；子类如需附加提示请覆盖 <see cref="ExtraCardHoverTips"/>。</remarks>
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
		CardMechanicPresentation.CreateHoverTips(CardMechanics).Concat(ExtraCardHoverTips);

	/// <summary>子类附加的悬停提示（默认空）。</summary>
	protected virtual IEnumerable<IHoverTip> ExtraCardHoverTips => Array.Empty<IHoverTip>();

	/// <inheritdoc/>
	/// <remarks>
	/// 把 <see cref="CardMechanics"/> 填进卡面描述模板的参数（<c>{Link}</c> / <c>{LinkName}</c> …），
	/// 于是卡面词条文本与悬停提示同源，并且能跟着运行期状态变（「回收」的有无与层数）——
	/// 这正是「凝锋赋予的回收」能显示在极光剑卡面上的原因。
	/// 子类若要追加自己的模板参数，覆盖本方法并调用 <c>base</c>。
	/// </remarks>
	protected override void AddExtraArgsToDescription(LocString description)
	{
		HeidemarieCardMechanics.AddCardTextArguments(description, this);
		base.AddExtraArgsToDescription(description);
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 通用工具：百分比增伤池的一次性使用
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 「百分比增伤池」的一次性使用：注入 <paramref name="percentPoints"/> 个百分点 →
	/// 执行 <paramref name="attack"/> → <strong>无论攻击是否正常结算都清池</strong>。
	///
	/// <para><b>为什么要包一层 try/finally</b>：池的清理责任归调用方
	/// （见 <see cref="HeidemarieDamagePercentPower"/> 的说明）。若写成
	/// 「注入 → 攻击 → 清池」的裸序列，攻击一旦抛异常就会把池留到下一次攻击
	/// （变成白嫖增伤）；用本方法包起来则一定清干净。</para>
	///
	/// <para>增伤覆盖范围 = <paramref name="attack"/> 里的全部命中与全部目标
	/// （池在攻击开始前注入、在攻击整体结束后才清），与「一次攻击结算」语义一致。</para>
	/// </summary>
	/// <param name="choiceContext">玩家选择上下文。</param>
	/// <param name="percentPoints">增伤百分点（<c>50</c> = +50%）。&lt;= 0 时不入池，仍会清池（空操作）。</param>
	/// <param name="attack">需要吃这次增伤的攻击逻辑。</param>
	protected async Task WithPercentDamageBoost(
		PlayerChoiceContext choiceContext,
		decimal percentPoints,
		Func<Task> attack)
	{
		await HeidemarieDamagePercentPower.InjectAsync(choiceContext, Owner.Creature, percentPoints, this);
		try
		{
			await attack();
		}
		finally
		{
			await HeidemarieDamagePercentPower.ClearAsync(Owner.Creature);
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	// 关键字行为：统一钩子分派
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// 关键字「连接」的<strong>统一派发点</strong>：任何带「连接」的牌被打出时，
	/// 在此丢弃手牌中其他所有带「连接」的牌。
	///
	/// <para><b>为什么必须是「打出前」这个钩子，而不是各卡自己的 <c>OnPlay</c>：</b></para>
	/// <list type="number">
	///   <item>「连接」可能是<strong>运行期临时赋予</strong>的（万众英雄抽到的牌、缀星 / 微光 /
	///   冥渡 / 撷光 / 引锋召刃选中的牌），它们本身没有连接牌的出牌逻辑 —— 挂在基类钩子上才对所有牌生效；</item>
	///   <item>「剑冢」重放墓地里的极光剑、以及别的效果自动打出的连接牌，同样走这条钩子；</item>
	///   <item>「打出前计数」类卡牌要在丢弃<strong>之前</strong>拿到连接数，
	///   而「打出时生成手牌」类卡牌（万剑诀 / 归墟剑骸）又必须赶在生成之前完成丢弃 ——
	///   两者只有「OnPlay 之前」这一个共同位置，所以顺手在这里记录
	///   <see cref="LinkedCountBeforePlay"/>。</item>
	/// </list>
	///
	/// <para>「万剑齐鸣」连续打出多张连接牌时由
	/// <see cref="HeidemarieCardActions.SuppressLinkDiscard"/> 抑制连带丢弃。</para>
	/// </summary>
	public override async Task BeforeCardPlayed(CardPlay cardPlay)
	{
		if (cardPlay.Card == this && IsLinked)
		{
			// 先记录快照（口径 = 手牌中其他连接牌，不含自身），再丢弃 —— 顺序不能反。
			// 「是否含自身」由各卡按卡面文案在 ResolveLinkedCountBeforePlay 里显式决定。
			LinkedCountBeforePlay = HeidemarieCardActions.CountLinkedInHand(this, includeSelf: false);

			// 该钩子不携带 PlayerChoiceContext；丢弃本身不需要玩家选择，
			// 因此用一个「不阻塞其他玩家」的空上下文（与感应派发同一做法）。
			await HeidemarieCardActions.DiscardOtherLinked(this, new BlockingPlayerChoiceContext());
		}

		await base.BeforeCardPlayed(cardPlay);
	}

	/// <inheritdoc/>
	/// <remarks>关键字「焚烧」：本牌被消耗后执行。</remarks>
	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		if (card == this && this is IBurnCard burn)
		{
			await burn.OnBurn(choiceContext, causedByEthereal);
		}

		await base.AfterCardExhausted(choiceContext, card, causedByEthereal);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 关键字「安息」：非回合结束被丢弃时发动效果。
	/// 回合末清手牌走的是 <c>CardPileCmd.Add</c>，不会触发本钩子，因此天然满足「非回合结束」。
	/// </remarks>
	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (card == this && IsRestCard)
		{
			RestLockedThisTurn = true;
			await OnRestEffect(choiceContext);
		}

		await base.AfterCardDiscarded(choiceContext, card);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 关键字「感应」：本牌<b>从抽牌堆进入手牌</b>时触发；
	/// 关键字「回收」：移入墓地时返回手牌并减 1 层。
	/// </remarks>
	public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
	{
		// 「感应」为什么挂在「牌堆变化」而不是 AfterCardDrawn：
		// 只有 CardPileCmd.Draw（回合抽牌、抽牌类效果）才会派发 AfterCardDrawn，
		// 而「把指定牌从抽牌堆抓进手牌」的效果（本 mod 的窥星择光 / 撷光 / 引锋召刃，
		// 原版的 PowerCell / JeweledMask 等遗物）走的是 CardPileCmd.Add(card, PileType.Hand)，
		// 那条路径不会派发 AfterCardDrawn —— 于是这些牌进手时感应静默失效。
		// 改用「旧堆 == 抽牌堆 && 新堆 == 手牌」判定后，两条路径都能覆盖，
		// 且天然排除「被丢进墓地 / 被消耗 / 洗回抽牌堆」等非抽取动作。
		if (card == this
			&& this is ISenseCard sense
			&& oldPileType == PileType.Draw
			&& card.Pile?.Type == PileType.Hand)
		{
			// 牌堆变化钩子不携带 PlayerChoiceContext；感应效果本身不需要玩家选择，
			// 因此用一个「不阻塞其他玩家」的空上下文即可（游戏内同类场景的标准做法）。
			await sense.OnSense(new BlockingPlayerChoiceContext());
		}

		if (card == this && !_recycling && card.Pile?.Type == PileType.Discard && RecycleRemaining > 0)
		{
			RecycleRemaining--;
			_recycling = true;
			try
			{
				await CardPileCmd.Add(this, PileType.Hand);
			}
			finally
			{
				_recycling = false;
			}
		}

		await base.AfterCardChangedPiles(card, oldPileType, clonedBy);
	}

	/// <inheritdoc/>
	/// <remarks>关键字「守墓」：洗牌时把自己从洗牌列表（弃牌堆 + 抽牌堆）里剔除，留在墓地。</remarks>
	public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
	{
		if (!isInitialShuffle && this is IGraveKeepCard)
		{
			cards.Remove(this);
		}

		base.ModifyShuffleOrder(player, cards, isInitialShuffle);
	}

	/// <inheritdoc/>
	/// <remarks>回合开始清除「安息」的本回合锁定。</remarks>
	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner)
		{
			RestLockedThisTurn = false;
			RuntimeLinkedThisTurn = false;
		}

		return base.AfterPlayerTurnStart(choiceContext, player);
	}

	/// <summary>
	/// 构造一张 Heidemarie 卡牌。
	/// </summary>
	/// <param name="canonicalEnergyCost">基础耗能。</param>
	/// <param name="type">卡牌类型（攻击/技能/能力）。</param>
	/// <param name="rarity">稀有度。</param>
	/// <param name="targetType">目标类型。</param>
	/// <param name="shouldShowInCardLibrary">是否在图鉴中显示。</param>
	protected HeidemarieCard(int canonicalEnergyCost, CardType type, CardRarity rarity, TargetType targetType, bool shouldShowInCardLibrary = true)
		: base(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary)
	{
		CanonicalCostValue = canonicalEnergyCost;
	}

	/// <summary>
	/// 构造时声明的<b>基准费用</b>（升级 / 附魔带来的费用变化不算）。
	/// 供 <see cref="SkillBodyTier"/> 做「稀有度 + 费用取更重者」的档位判定。
	/// </summary>
	protected int CanonicalCostValue { get; }
}
