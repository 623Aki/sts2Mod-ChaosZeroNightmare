using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CznCore.Cards;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// <para>关键字「链接」「终结」「唯一」是纯标记（行为写在出牌逻辑 / 加牌入口里），
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

	/// <summary>本牌带的自定义关键字（默认由接口推导，见 <see cref="HeidemarieCardMechanics"/>）。</summary>
	protected virtual IEnumerable<CardMechanicDefinition> CardMechanics => HeidemarieCardMechanics.For(this);

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
	// 关键字行为：统一钩子分派
	// ──────────────────────────────────────────────────────────────────────────

	/// <inheritdoc/>
	/// <remarks>关键字「感应」：抽到本牌后执行。</remarks>
	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card == this && this is ISenseCard sense)
		{
			await sense.OnSense(choiceContext);
		}

		await base.AfterCardDrawn(choiceContext, card, fromHandDraw);
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
		if (card == this && this is IRestCard rest)
		{
			RestLockedThisTurn = true;
			await rest.OnRest(choiceContext);
		}

		await base.AfterCardDiscarded(choiceContext, card);
	}

	/// <inheritdoc/>
	/// <remarks>关键字「回收」：移入墓地时返回手牌并减 1 层。</remarks>
	public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
	{
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
	}
}
