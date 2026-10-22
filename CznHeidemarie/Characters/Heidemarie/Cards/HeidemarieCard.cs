using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznCore.Cards;
using CznHeidemarie.Characters.Heidemarie.Powers;
using CznHeidemarie.Characters.Heidemarie.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
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
	/// 由 <see cref="IRestCard"/> 在丢弃时置位，下个自己的回合起手抽牌前
	/// （<see cref="BeforeHandDraw"/>）清除。
	///
	/// <para>拦截点在 <see cref="HeidemarieRestLock"/>：洗牌走
	/// <see cref="ModifyShuffleOrder"/>、取墓牌各自过滤候选、其余移堆动作由
	/// <c>Registration/HeidemarieRestLockPatch</c> 兜底。「坠星」按设计强制绕过。</para>
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

	/// <summary>
	/// 本角色卡面 PNG 的规范路径：
	/// <c>res://images/packed/card_portraits/heidemarie/&lt;卡牌ID&gt;.png</c>。
	///
	/// <para>与游戏自带的卡面约定完全一致（<see cref="CardModel.PortraitPngPath"/> 就是这条），
	/// 所以素材直接按「卡牌 ID 小写」命名丢进目录即可，不需要任何图集打包步骤。</para>
	///
	/// <para>同时喂给基类的 <see cref="CardModel.HasPortrait"/>，让「有没有专属立绘」的判断也准。</para>
	/// </summary>
	private string ManagedPortraitPngPath =>
		ImageHelper.GetImagePath($"packed/card_portraits/{Pool.Title.ToLowerInvariant()}/{Id.Entry.ToLowerInvariant()}.png");

	/// <inheritdoc />
	protected override string PortraitPngPath => ManagedPortraitPngPath;

	/// <inheritdoc/>
	/// <remarks>
	/// 取卡顺序（<b>先 PNG，再图集，最后占位</b>）：
	/// <list type="number">
	///   <item><c>res://images/packed/card_portraits/heidemarie/{卡牌ID}.png</c> —— 当前实际使用的路径；</item>
	///   <item><c>res://images/atlases/card_atlas.sprites/heidemarie/{卡牌ID}.tres</c> ——
	///   将来若把立绘打进图集就走这条（现在没有这个目录，属于预留）；</item>
	///   <item><see cref="FallbackPortraitPath"/> —— 两者都没有时的战士占位图。</item>
	/// </list>
	///
	/// <para><b>为什么必须自己按 PNG 路径返回</b>：引擎的 <c>AtlasResourceLoader</c> 虽然会把
	/// <c>card_atlas...tres</c> 回落到 <c>packed/card_portraits/&lt;pool&gt;/&lt;id&gt;.png</c>，
	/// 但前提是「先真的去加载那个 .tres」。而本类若在 <c>.tres</c> 不存在时直接返回
	/// <see cref="FallbackPortraitPath"/>（一个<b>存在</b>的战士 .tres），
	/// 就永远走不到那条回落 —— 立绘会一直显示战士占位图。</para>
	/// </remarks>
	public override string PortraitPath
	{
		get
		{
			string pngPath = ManagedPortraitPngPath;
			if (PortraitPathCache.TryGetValue(pngPath, out string? cached))
			{
				return cached;
			}

			string atlasPath = ImageHelper.GetImagePath(
				$"atlases/card_atlas.sprites/{Pool.Title.ToLowerInvariant()}/{Id.Entry.ToLowerInvariant()}.tres");

			string resolved = ResourceLoader.Exists(pngPath) ? pngPath
				: ResourceLoader.Exists(atlasPath) ? atlasPath
				: FallbackPortraitPath;

			PortraitPathCache[pngPath] = resolved;
			return resolved;
		}
	}

	/// <summary>
	/// 本牌的演出档位 = 身体动作链 + 特效组（档位表见
	/// <see cref="HeidemariePresentationCatalog"/>）。
	///
	/// <para>攻击牌显式写；技能 / 能力牌由基类按「稀有度与费用取更重者」算，不用写。
	/// 默认规则：Rare（或 ≥3 费）→ 蓄势；Uncommon（或 2 费）→ 结阵；其余 → 施法；
	/// 攻击类回退轻击档（攻击牌通常都会自己覆盖）。</para>
	/// </summary>
	protected virtual HeidemarieEffectTier PresentationTier => ResolveDefaultTier();

	/// <summary>
	/// 档位内的特效变体名（对应 <c>heidemarie_moves.json</c> 的「档位/变体」键，
	/// 如 <c>AttackLight/red</c>）。同一套动作换一套特效强度，不换动作。
	/// </summary>
	protected virtual string? PresentationVariant => null;

	/// <summary>档位表读取用的实际档位（<see cref="PresentationTier"/> 的同程序集转发）。</summary>
	internal HeidemarieEffectTier EffectiveTier => PresentationTier;

	/// <summary>档位表读取用的实际变体名（<see cref="PresentationVariant"/> 的同程序集转发）。</summary>
	internal string? EffectiveVariant => PresentationVariant;

	// 默认档位：沿用原 SkillBodyTier 的稀有度 / 费用规则
	private HeidemarieEffectTier ResolveDefaultTier()
	{
		if (Type is not (CardType.Skill or CardType.Power))
		{
			return HeidemarieEffectTier.AttackLight;
		}

		if (Rarity == CardRarity.Rare || CanonicalCostValue >= 3)
		{
			return HeidemarieEffectTier.Charge;
		}

		if (Rarity == CardRarity.Uncommon || CanonicalCostValue >= 2)
		{
			return HeidemarieEffectTier.Guard;
		}

		return HeidemarieEffectTier.Cast;
	}

	/// <summary>
	/// 技能 / 能力牌的身体动作序列（由档位表给出，见
	/// <see cref="HeidemariePresentationCatalog"/>）。攻击牌返回 <c>null</c>
	/// —— 它们的动作走 <c>AttackCommand.WithAttackerAnim</c>，已有专属演出的牌
	/// 自己覆盖 <see cref="OnEnqueuePlayVfx"/>（如「防御」）。
	/// </summary>
	protected virtual string[]? SkillBodySequence =>
		Type is CardType.Skill or CardType.Power
			? HeidemariePresentationCatalog.Resolve(this).BodySequence
			: null;

	/// <summary>本牌带的自定义关键字（默认由接口推导，见 <see cref="HeidemarieCardMechanics"/>）。</summary>
	protected virtual IEnumerable<CardMechanicDefinition> CardMechanics => HeidemarieCardMechanics.For(this);

	/// <summary>
	/// 按本牌档位起一次攻击指令：触发器与命中延迟由
	/// <see cref="HeidemariePresentationCatalog"/> 按 <see cref="PresentationTier"/> 给出，
	/// 卡牌不再各自写 <c>HeidemarieAttackPlan.X</c>，动作与特效档位天然一致。
	///
	/// <para>返回 <see cref="AttackCommand"/> 供调用方继续链
	/// <c>Targeting</c> / <c>WithHitCount</c> / <c>WithHitFx</c> / <c>Execute</c>。</para>
	/// </summary>
	/// <param name="damage">本次攻击的伤害值。</param>
	/// <param name="cardPlay">出牌上下文。</param>
	protected AttackCommand AttackWithPresentation(decimal damage, CardPlay cardPlay)
	{
		HeidemarieAttackPlan plan = HeidemariePresentationCatalog.AttackPlanOf(this)
			?? throw new InvalidOperationException(
				$"{GetType().Name} 的档位不是攻击档，不能用 AttackWithPresentation");
		return DamageCmd.Attack(damage)
			.FromCard(this, cardPlay)
			.WithAttackerAnim(plan.Trigger, HeidemarieAttackPlan.HitDelay(plan));
	}

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

		// 「回收」与「安息」互斥：已经带锁墓标记的牌不回手。
		// 注意首次丢入墓地时，本钩子（CardPileCmd.Add 内）先于 AfterCardDiscarded 的置锁，
		// 因此这里只能拦住「已处于锁定态」的牌；当前没有同时带两个关键字的牌，
		// 将来若加入，需要在丢弃入口处另行处理（否则回收会先于锁生效）。
		if (card == this && !_recycling && card.Pile?.Type == PileType.Discard
			&& RecycleRemaining > 0 && !HeidemarieRestLock.IsLocked(this))
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
	/// <remarks>
	/// 关键字「守墓」：洗牌时若自己还在墓地，就从洗牌列表（弃牌堆 + 抽牌堆）里剔除，留在墓地。
	/// 关键字「安息」：本回合被锁在墓地的牌同样不进抽牌堆 ——
	/// 判定与例外见 <see cref="HeidemarieRestLock"/>。
	/// </remarks>
	public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
	{
		// 初始洗牌发生在战斗开始（卡还在牌组里），两类关键字都不适用
		if (!isInitialShuffle)
		{
			// 「守墓」默认剔除，两个前提：
			//
			// ① <b>此刻还在墓地（弃牌堆）</b>。「守墓」的含义是「洗牌时不要移动到抽牌堆」，
			//    一张已经在抽牌堆里的牌谈不上「不移动」—— 剔除它反而制造状态不一致：
			//    这里只改「洗牌列表」，不负责把牌从牌堆里搬走，而洗牌结尾的批量 Add
			//    对不在列表里的牌既不删除也不重排，整堆重排完后它就被挤到堆顶，
			//    结果变成「一洗必先抽到」。（被「坠星」塞进抽牌堆的剑正是这种情形，
			//    「重启」「瓶装潜力」这类显式洗牌会踩到。）口径与
			//    <see cref="HeidemarieRestLock.IsLocked"/> 一致。
			// ② 调用方没有用 <see cref="HeidemarieRestLock.BypassGraveKeep"/> 开豁免
			//    （「坠星」的洗牌）。
			if (this is IGraveKeepCard
				&& Pile?.Type == PileType.Discard
				&& !HeidemarieRestLock.IsGraveKeepBypassedFor(player))
			{
				cards.Remove(this);
			}

			// 只剔除「此刻确实在墓地」的锁墓牌：被「坠星」强制推到抽牌堆之后，
			// 锁标记仍留到回合结束，但不该再从洗牌列表里拿掉（它本来就该被洗）。
			if (HeidemarieRestLock.IsLocked(this))
			{
				cards.Remove(this);
			}
		}

		base.ModifyShuffleOrder(player, cards, isInitialShuffle);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 回合开始时清除运行期「临时连接」（本回合内赋予的连接）。
	/// </remarks>
	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner)
		{
			RuntimeLinkedThisTurn = false;
		}

		return base.AfterPlayerTurnStart(choiceContext, player);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// 清除「安息」的本回合锁 —— <b>必须挂在起手抽牌之前</b>。
	///
	/// <para>引擎的回合开始顺序是「起手抽牌 → <c>AfterPlayerTurnStart</c>」
	/// （见 <c>CombatManager.SetupPlayerTurn</c>：先 <c>CardPileCmd.Draw</c>，再派发该钩子），
	/// 而起手抽牌在抽牌堆空时会触发自动洗牌。若把清锁留在 <c>AfterPlayerTurnStart</c>，
	/// 上一回合锁在墓地的牌会错过这次洗牌，导致本回合少抽牌 ——
	/// 语义上「本回合」也应当在抽牌之前就结束。</para>
	///
	/// <para><c>BeforeHandDraw</c> 每个玩家回合（含额外回合）各派发一次，且早于抽牌，
	/// 正是「上一回合的锁失效、这一回合的牌可以正常洗入抽牌堆」的分界点。</para>
	/// </remarks>
	public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
	{
		if (player == Owner)
		{
			RestLockedThisTurn = false;
		}

		return base.BeforeHandDraw(player, choiceContext, combatState);
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
	/// 供 <see cref="PresentationTier"/> 做「稀有度 + 费用取更重者」的默认档位判定。
	/// </summary>
	protected int CanonicalCostValue { get; }
}
